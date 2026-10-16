using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Infrastructure;

namespace NeuroCare.Tests;

public sealed class ClinicalEnv : IDisposable
{
    public TestEnv Base { get; }
    public IClinicalNoteService Notes { get; }
    public IMedicationService Medications { get; }
    public ISymptomService Symptoms { get; }
    public ISeizureService Seizures { get; }
    public ITimelineService Timeline { get; }
    public IFallService Falls { get; }
    public IQuestionnaireService Questionnaires { get; }
    public IDashboardService Dashboard { get; }
    public IDocumentService Documents { get; }
    public IReportService Reports { get; }

    public ClinicalEnv(string dbName, FakeCurrentUser user, FakeFileStorage? storage = null)
    {
        Base = new TestEnv(dbName, user);
        var db = Base.Db;
        var guard = new AccessGuard(user);
        var patients = new PatientRepository(db);
        var doctors = new DoctorRepository(db);
        var appts = new AppointmentRepository(db);
        var noteRepo = new ClinicalNoteRepository(db);
        var medRepo = new MedicationRepository(db);
        var symRepo = new SymptomRepository(db);
        var szRepo = new SeizureRepository(db);
        var fallRepo = new FallRepository(db);
        var qRepo = new QuestionnaireResponseRepository(db);
        var docRepo = new DocumentRepository(db);
        var audit = new AuditService(new AuditLogRepository(db), user, Base.Clock, db);
        var ctx = new ClinicalContext(patients, doctors, guard, user, Base.Clock);

        Notes = new ClinicalNoteService(ctx, noteRepo, db, guard, audit, Base.Clock, NullLogger<ClinicalNoteService>.Instance);
        Medications = new MedicationService(ctx, medRepo, db, guard, audit, Base.Clock);
        Symptoms = new SymptomService(ctx, symRepo, db, guard, user, audit, Base.Clock);
        Seizures = new SeizureService(ctx, szRepo, db, guard, user, audit, Base.Clock);
        Timeline = new TimelineService(ctx, appts, noteRepo, medRepo, symRepo, szRepo, fallRepo, qRepo, docRepo, guard, audit, Base.Clock);
        Falls = new FallService(ctx, fallRepo, db, guard, user, audit, Base.Clock);
        var definitionProvider = new QuestionnaireDefinitionProvider(new ClinicQuestionnaireRepository(db), guard);
        Questionnaires = new QuestionnaireService(ctx, qRepo, db, guard, user, audit, Base.Clock, definitionProvider, new NoopSecurityNotificationService());
        Dashboard = new DashboardService(patients, doctors, appts, qRepo, guard, Base.Clock);
        Documents = new DocumentService(ctx, docRepo, storage ?? new FakeFileStorage(), db, guard, user, audit, Base.Clock);
        Reports = new ReportService(ctx, noteRepo, medRepo, symRepo, szRepo, fallRepo, qRepo, docRepo,
            new OrganizationRepository(db), guard, audit, Base.Clock);
    }

    public void Dispose() => Base.Dispose();

    // "Agora" do FakeClock = 10/03/2026 12:00 (local).
    public static DateTime Before(int hours = 1) => new DateTime(2026, 3, 10, 12, 0, 0).AddHours(-hours);

    public static SaveClinicalNoteDto Note(string? s = "Paciente relata tremor") => new()
    { OccurredAtLocal = Before(), Subjective = s, Assessment = "Avaliação do médico" };
}

public class ClinicalNoteTests
{
    private static FakeCurrentUser Author(Scenario s) => FakeCurrentUser.With(Roles.Doctor, s.OrgA, s.DoctorAUserId);

    [Fact]
    public async Task Note_is_created_as_draft_signed_and_then_locked()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, Author(s));

        var id = await env.Notes.CreateAsync(s.PatientA1, ClinicalEnv.Note());
        var draft = await env.Notes.GetAsync(id);
        Assert.Equal(NoteStatus.Draft, draft.Status);
        Assert.True(draft.CanEdit);

        await env.Notes.UpdateAsync(id, ClinicalEnv.Note("Texto revisado"));
        await env.Notes.SignAsync(id);

        var signed = await env.Notes.GetAsync(id);
        Assert.Equal(NoteStatus.Signed, signed.Status);
        Assert.NotNull(signed.SignedAtLocal);
        Assert.False(signed.CanEdit);
        Assert.Equal("Texto revisado", signed.Subjective);

        await Assert.ThrowsAsync<RequestValidationException>(() => env.Notes.UpdateAsync(id, ClinicalEnv.Note("Alteração indevida")));
        await Assert.ThrowsAsync<RequestValidationException>(() => env.Notes.SignAsync(id));
    }

    [Fact]
    public async Task Addendum_is_allowed_only_after_signing()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, Author(s));
        var id = await env.Notes.CreateAsync(s.PatientA1, ClinicalEnv.Note());

        await Assert.ThrowsAsync<RequestValidationException>(() => env.Notes.AddAddendumAsync(id, new AddendumDto { Text = "Complemento" }));

        await env.Notes.SignAsync(id);
        await env.Notes.AddAddendumAsync(id, new AddendumDto { Text = "Complemento" });

        var details = await env.Notes.GetAsync(id);
        var addendum = Assert.Single(details.Addenda);
        Assert.Equal("Complemento", addendum.Text);
    }

    [Fact]
    public async Task Empty_note_and_future_date_are_rejected()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, Author(s));

        await Assert.ThrowsAsync<RequestValidationException>(() => env.Notes.CreateAsync(s.PatientA1,
            new SaveClinicalNoteDto { OccurredAtLocal = ClinicalEnv.Before() }));
        var future = ClinicalEnv.Note();
        future.OccurredAtLocal = new DateTime(2026, 3, 20, 9, 0, 0);
        await Assert.ThrowsAsync<RequestValidationException>(() => env.Notes.CreateAsync(s.PatientA1, future));
    }

    [Fact]
    public async Task Only_author_can_edit_or_sign_but_other_doctor_can_read_and_add_addendum()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        Guid noteId;
        using (var author = new ClinicalEnv(db, Author(s)))
        {
            noteId = await author.Notes.CreateAsync(s.PatientA1, ClinicalEnv.Note());
            await author.Notes.SignAsync(noteId);
        }

        var otherUser = Guid.NewGuid();
        using (var sys = new TestEnv(db, FakeCurrentUser.System()))
        {
            sys.Db.Doctors.Add(new Doctor { OrganizationId = s.OrgA, UserId = otherUser, FullName = "Dr. Colega" });
            await sys.Db.SaveChangesAsync();
        }

        using var env = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA, otherUser));
        Assert.Equal(NoteStatus.Signed, (await env.Notes.GetAsync(noteId)).Status);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Notes.UpdateAsync(noteId, ClinicalEnv.Note("x")));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Notes.SignAsync(noteId));
        await env.Notes.AddAddendumAsync(noteId, new AddendumDto { Text = "Comentário do colega" });
    }

    [Theory]
    [InlineData(Roles.ClinicAdmin)]
    [InlineData(Roles.Patient)]
    [InlineData(Roles.Researcher)]
    public async Task Non_doctors_cannot_read_or_write_clinical_notes(string role)
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, FakeCurrentUser.With(role, s.OrgA, role == Roles.Patient ? s.PatientA1UserId : null));

        await Assert.ThrowsAsync<ForbiddenException>(() => env.Notes.ListAsync(s.PatientA1));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Notes.CreateAsync(s.PatientA1, ClinicalEnv.Note()));
    }

    [Fact]
    public async Task Doctor_of_another_organization_cannot_reach_note_or_patient()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        Guid noteId;
        using (var a = new ClinicalEnv(db, Author(s)))
            noteId = await a.Notes.CreateAsync(s.PatientA1, ClinicalEnv.Note());

        using var b = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgB));
        await Assert.ThrowsAsync<NotFoundException>(() => b.Notes.GetAsync(noteId));
        await Assert.ThrowsAsync<NotFoundException>(() => b.Notes.ListAsync(s.PatientA1));
    }

    [Fact]
    public async Task Audit_log_never_contains_clinical_text()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, Author(s));

        var id = await env.Notes.CreateAsync(s.PatientA1, ClinicalEnv.Note("TEXTO-CLINICO-SIGILOSO"));
        await env.Notes.GetAsync(id);

        var logs = await env.Base.Db.AuditLogs.ToListAsync();
        Assert.Contains(logs, l => l.Action == "ClinicalNote.Create" && l.EntityId == id.ToString());
        Assert.DoesNotContain(logs, l => (l.Details ?? "").Contains("SIGILOSO"));
    }
}

public class MedicationTests
{
    private static FakeCurrentUser Doctor(Scenario s) => FakeCurrentUser.With(Roles.Doctor, s.OrgA, s.DoctorAUserId);

    private static SaveMedicationDto Med(string name = "Levodopa") => new()
    { Name = name, Dosage = "100 mg", Frequency = "3x ao dia", StartDate = new DateTime(2026, 3, 1) };

    [Fact]
    public async Task Doctor_prescribes_and_manages_medication_lifecycle()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, Doctor(s));

        var id = await env.Medications.CreateAsync(s.PatientA1, Med());
        await env.Medications.TransitionAsync(id, MedicationAction.Suspend);
        await env.Medications.TransitionAsync(id, MedicationAction.Resume);
        await env.Medications.TransitionAsync(id, MedicationAction.Finish);

        var item = Assert.Single(await env.Medications.ListAsync(s.PatientA1));
        Assert.Equal(MedicationStatus.Finished, item.Status);
        Assert.NotNull(item.EndDate);
        await Assert.ThrowsAsync<RequestValidationException>(() => env.Medications.UpdateAsync(id, Med("Outro")));
        await Assert.ThrowsAsync<RequestValidationException>(() => env.Medications.TransitionAsync(id, MedicationAction.Suspend));
    }

    [Fact]
    public async Task Duplicate_open_medication_and_invalid_dates_are_rejected()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, Doctor(s));
        await env.Medications.CreateAsync(s.PatientA1, Med("Levodopa"));

        await Assert.ThrowsAsync<RequestValidationException>(() => env.Medications.CreateAsync(s.PatientA1, Med("levodopa")));
        var bad = Med("Pramipexol");
        bad.EndDate = new DateTime(2026, 2, 1);
        await Assert.ThrowsAsync<RequestValidationException>(() => env.Medications.CreateAsync(s.PatientA1, bad));
        // mesmo nome é permitido para outro paciente
        await env.Medications.CreateAsync(s.PatientA2, Med("Levodopa"));
    }

    [Fact]
    public async Task Patient_sees_only_own_medications_even_when_passing_another_patient_id()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using (var doc = new ClinicalEnv(db, Doctor(s)))
        {
            await doc.Medications.CreateAsync(s.PatientA1, Med("Levodopa"));
            await doc.Medications.CreateAsync(s.PatientA2, Med("Carbamazepina"));
        }

        using var env = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA1UserId));
        var list = await env.Medications.ListAsync(s.PatientA2); // tentativa de ver o outro paciente

        var item = Assert.Single(list);
        Assert.Equal("Levodopa", item.Name);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Medications.CreateAsync(s.PatientA1, Med("Nova")));
    }

    [Fact]
    public async Task Clinic_admin_cannot_access_medications()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, FakeCurrentUser.With(Roles.ClinicAdmin, s.OrgA));

        await Assert.ThrowsAsync<ForbiddenException>(() => env.Medications.ListAsync(s.PatientA1));
    }
}

public class SymptomAndSeizureTests
{
    private static SaveSymptomDto Symptom(int intensity = 5) => new()
    { OccurredAtLocal = ClinicalEnv.Before(), Type = SymptomType.Tremor, Intensity = intensity };

    private static SaveSeizureDto Seizure(int seconds = 90) => new()
    { OccurredAtLocal = ClinicalEnv.Before(), Type = SeizureType.Focal, DurationSeconds = seconds };

    [Fact]
    public async Task Patient_records_symptom_only_for_self_ignoring_other_patient_id()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA1UserId));

        await env.Symptoms.CreateAsync(s.PatientA2, Symptom()); // tenta gravar no outro paciente

        var all = await env.Base.Db.SymptomRecords.ToListAsync();
        var record = Assert.Single(all);
        Assert.Equal(s.PatientA1, record.PatientId);
        Assert.True(record.RecordedByPatient);
    }

    [Fact]
    public async Task Symptom_validation_rules()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA, s.DoctorAUserId));

        await Assert.ThrowsAsync<RequestValidationException>(() => env.Symptoms.CreateAsync(s.PatientA1, Symptom(11)));
        var future = Symptom();
        future.OccurredAtLocal = new DateTime(2026, 3, 30, 8, 0, 0);
        await Assert.ThrowsAsync<RequestValidationException>(() => env.Symptoms.CreateAsync(s.PatientA1, future));
        await Assert.ThrowsAsync<NotFoundException>(() => env.Symptoms.CreateAsync(null, Symptom())); // médico precisa informar o paciente
    }

    [Fact]
    public async Task Other_organization_and_clinic_admin_cannot_record_or_read()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);

        using (var b = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgB)))
        {
            await Assert.ThrowsAsync<NotFoundException>(() => b.Symptoms.CreateAsync(s.PatientA1, Symptom()));
            await Assert.ThrowsAsync<NotFoundException>(() => b.Seizures.ListAsync(s.PatientA1, 90));
        }
        using var admin = new ClinicalEnv(db, FakeCurrentUser.With(Roles.ClinicAdmin, s.OrgA));
        await Assert.ThrowsAsync<ForbiddenException>(() => admin.Symptoms.ListAsync(s.PatientA1, 90));
        await Assert.ThrowsAsync<ForbiddenException>(() => admin.Seizures.CreateAsync(s.PatientA1, Seizure()));
    }

    [Fact]
    public async Task Seizure_is_recorded_listed_and_validated()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA1UserId));

        await env.Seizures.CreateAsync(null, Seizure(120));
        await Assert.ThrowsAsync<RequestValidationException>(() => env.Seizures.CreateAsync(null, Seizure(0)));

        var item = Assert.Single(await env.Seizures.ListAsync(null, 90));
        Assert.Equal(120, item.DurationSeconds);
        Assert.True(item.RecordedByPatient);
    }
}

public class TimelineTests
{
    [Fact]
    public async Task Doctor_sees_notes_but_patient_does_not()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using (var doc = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA, s.DoctorAUserId)))
        {
            await doc.Notes.CreateAsync(s.PatientA1, ClinicalEnv.Note());
            await doc.Symptoms.CreateAsync(s.PatientA1, new SaveSymptomDto { OccurredAtLocal = ClinicalEnv.Before(), Type = SymptomType.Headache, Intensity = 3 });
            var docItems = await doc.Timeline.GetAsync(s.PatientA1);
            Assert.Contains(docItems, i => i.Kind == TimelineKind.Note);
            Assert.Contains(docItems, i => i.Kind == TimelineKind.Symptom);
        }

        using var patient = new ClinicalEnv(db, FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA1UserId));
        var items = await patient.Timeline.GetAsync(null);
        Assert.DoesNotContain(items, i => i.Kind == TimelineKind.Note);
        Assert.Contains(items, i => i.Kind == TimelineKind.Symptom);
    }
}
