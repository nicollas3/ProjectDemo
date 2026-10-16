using Microsoft.EntityFrameworkCore;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Tests;

public class QuestionnaireCatalogTests
{
    [Fact]
    public void Max_scores_and_bands_are_consistent()
    {
        Assert.Equal(27, QuestionnaireCatalog.Phq9.MaxScore);
        Assert.Equal(21, QuestionnaireCatalog.Gad7.MaxScore);
        Assert.Equal(5, QuestionnaireCatalog.Mrs.MaxScore);
        Assert.Equal(5, QuestionnaireCatalog.HoehnYahr.MaxScore);

        // toda pontuação possível cai em alguma faixa (sem lacunas)
        foreach (var d in QuestionnaireCatalog.All)
            foreach (var option in d.Items.Count == 1 ? d.Items[0].Options.Select(o => o.Value) : Enumerable.Range(0, d.MaxScore + 1))
                Assert.NotEqual("—", d.BandFor(option));
    }

    [Theory]
    [InlineData(0, "Mínima")]
    [InlineData(4, "Mínima")]
    [InlineData(5, "Leve")]
    [InlineData(10, "Moderada")]
    [InlineData(15, "Moderadamente grave")]
    [InlineData(27, "Grave")]
    public void Phq9_bands(int score, string band) => Assert.Equal(band, QuestionnaireCatalog.Phq9.BandFor(score));

    [Fact]
    public void Licensed_instruments_are_not_in_catalog() =>
        Assert.DoesNotContain(QuestionnaireCatalog.All, d =>
            d.Key.Contains("updrs", StringComparison.OrdinalIgnoreCase) || d.Key.Contains("moca", StringComparison.OrdinalIgnoreCase));
}

public class QuestionnaireServiceTests
{
    private static Dictionary<int, int> Phq9(int each, int? item9 = null)
    {
        var d = Enumerable.Range(0, 9).ToDictionary(i => i, _ => each);
        if (item9.HasValue) d[8] = item9.Value;
        return d;
    }

    private static FakeCurrentUser Doctor(Scenario s) => FakeCurrentUser.With(Roles.Doctor, s.OrgA, s.DoctorAUserId);
    private static FakeCurrentUser Patient1(Scenario s) => FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA1UserId);

    [Fact]
    public async Task Phq9_is_scored_and_banded_without_safety_flag_when_item9_is_zero()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, Patient1(s));

        var result = await env.Questionnaires.SubmitAsync(null, "phq9", Phq9(1, item9: 0));

        Assert.Equal(8, result.TotalScore);
        Assert.Equal(27, result.MaxScore);
        Assert.Equal("Leve", result.BandLabel);
        Assert.False(result.SafetyFlag);
        Assert.Equal(9, result.Answers.Count);
    }

    [Fact]
    public async Task Positive_item9_raises_safety_flag_and_doctor_dashboard_alert()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using (var patient = new ClinicalEnv(db, Patient1(s)))
        {
            var r = await patient.Questionnaires.SubmitAsync(null, "phq9", Phq9(0, item9: 1));
            Assert.True(r.SafetyFlag);
        }

        using var doctor = new ClinicalEnv(db, Doctor(s));
        var dash = await doctor.Dashboard.GetAsync();
        var alert = Assert.Single(dash.SafetyAlerts);
        Assert.Equal(s.PatientA1, alert.PatientId);

        // ClinicAdmin não vê alertas clínicos
        using var admin = new ClinicalEnv(db, FakeCurrentUser.With(Roles.ClinicAdmin, s.OrgA));
        Assert.Empty((await admin.Dashboard.GetAsync()).SafetyAlerts);
    }

    [Fact]
    public async Task Incomplete_invalid_or_extra_answers_are_rejected()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, Doctor(s));

        var missing = Phq9(1); missing.Remove(3);
        await Assert.ThrowsAsync<RequestValidationException>(() => env.Questionnaires.SubmitAsync(s.PatientA1, "phq9", missing));
        var invalid = Phq9(1); invalid[2] = 7;
        await Assert.ThrowsAsync<RequestValidationException>(() => env.Questionnaires.SubmitAsync(s.PatientA1, "phq9", invalid));
        var extra = Phq9(1); extra[20] = 1;
        await Assert.ThrowsAsync<RequestValidationException>(() => env.Questionnaires.SubmitAsync(s.PatientA1, "phq9", extra));
        await Assert.ThrowsAsync<NotFoundException>(() => env.Questionnaires.SubmitAsync(s.PatientA1, "inexistente", Phq9(1)));
        Assert.Empty(await env.Base.Db.QuestionnaireResponses.ToListAsync());
    }

    [Fact]
    public async Task Clinician_only_instrument_cannot_be_applied_by_patient_but_doctor_can()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);

        using (var patient = new ClinicalEnv(db, Patient1(s)))
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => patient.Questionnaires.GetFormAsync("mrs"));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                patient.Questionnaires.SubmitAsync(null, "mrs", new Dictionary<int, int> { [0] = 3 }));
            var available = await patient.Questionnaires.ListAvailableAsync();
            Assert.DoesNotContain(available, a => a.ClinicianOnly);
        }

        using var doctor = new ClinicalEnv(db, Doctor(s));
        var result = await doctor.Questionnaires.SubmitAsync(s.PatientA1, "mrs", new Dictionary<int, int> { [0] = 3 });
        Assert.Equal(3, result.TotalScore);
        Assert.Equal("Incapacidade moderada", result.BandLabel);
        Assert.Contains(await doctor.Questionnaires.ListAvailableAsync(), a => a.ClinicianOnly);
    }

    [Fact]
    public async Task Patient_cannot_submit_for_or_read_results_of_another_patient()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        Guid a1ResultId;
        using (var a1 = new ClinicalEnv(db, Patient1(s)))
            a1ResultId = (await a1.Questionnaires.SubmitAsync(s.PatientA2, "gad7", Enumerable.Range(0, 7).ToDictionary(i => i, _ => 1))).Id;

        // a resposta foi gravada para o A1 (alvo é sempre o próprio paciente)
        using var inspect = new ClinicalEnv(db, Doctor(s));
        Assert.Equal(s.PatientA1, (await inspect.Base.Db.QuestionnaireResponses.SingleAsync()).PatientId);

        using var a2 = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA2UserId));
        await Assert.ThrowsAsync<NotFoundException>(() => a2.Questionnaires.GetResultAsync(a1ResultId));
        Assert.Empty(await a2.Questionnaires.ListResponsesAsync(null, null, 365));
    }

    [Fact]
    public async Task Other_organization_and_clinic_admin_cannot_read_results()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        Guid id;
        using (var doc = new ClinicalEnv(db, Doctor(s)))
            id = (await doc.Questionnaires.SubmitAsync(s.PatientA1, "gad7", Enumerable.Range(0, 7).ToDictionary(i => i, _ => 0))).Id;

        using var b = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgB));
        await Assert.ThrowsAsync<NotFoundException>(() => b.Questionnaires.GetResultAsync(id));
        using var admin = new ClinicalEnv(db, FakeCurrentUser.With(Roles.ClinicAdmin, s.OrgA));
        await Assert.ThrowsAsync<ForbiddenException>(() => admin.Questionnaires.ListAvailableAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => admin.Questionnaires.GetResultAsync(id));
    }

    [Fact]
    public async Task Audit_log_does_not_contain_scores_or_answers()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, Patient1(s));

        await env.Questionnaires.SubmitAsync(null, "phq9", Phq9(3));

        var log = await env.Base.Db.AuditLogs.SingleAsync(l => l.Action == "Questionnaire.Submit");
        Assert.Null(log.Details);
    }
}

public class FallServiceTests
{
    private static SaveFallDto Fall() => new()
    { OccurredAtLocal = ClinicalEnv.Before(), Circumstance = FallCircumstance.Bathroom, Injury = true };

    [Fact]
    public async Task Patient_records_fall_for_self_and_it_appears_in_timeline()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA1UserId));

        await env.Falls.CreateAsync(s.PatientA2, Fall()); // alvo ignorado: sempre o próprio paciente

        var fall = Assert.Single(await env.Falls.ListAsync(null, 90));
        Assert.True(fall.RecordedByPatient);
        Assert.True(fall.Injury);
        Assert.Equal(s.PatientA1, (await env.Base.Db.FallEvents.SingleAsync()).PatientId);
        Assert.Contains(await env.Timeline.GetAsync(null), i => i.Kind == TimelineKind.Fall);
    }

    [Fact]
    public async Task Fall_validation_and_access_rules()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);

        using (var doc = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA, s.DoctorAUserId)))
        {
            var future = Fall();
            future.OccurredAtLocal = new DateTime(2026, 4, 1, 8, 0, 0);
            await Assert.ThrowsAsync<RequestValidationException>(() => doc.Falls.CreateAsync(s.PatientA1, future));
            await doc.Falls.CreateAsync(s.PatientA1, Fall());
        }
        using (var b = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgB)))
            await Assert.ThrowsAsync<NotFoundException>(() => b.Falls.ListAsync(s.PatientA1, 90));
        using var admin = new ClinicalEnv(db, FakeCurrentUser.With(Roles.ClinicAdmin, s.OrgA));
        await Assert.ThrowsAsync<ForbiddenException>(() => admin.Falls.CreateAsync(s.PatientA1, Fall()));
    }
}
