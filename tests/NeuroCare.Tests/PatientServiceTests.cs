using Microsoft.EntityFrameworkCore;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Tests;

public class PatientServiceTests
{
    private static CreatePatientDto Valid(string cpf = "390.533.447-05") => new()
    {
        FullName = "  Fulano de Tal ", Cpf = cpf, BirthDate = new DateTime(1985, 5, 5), Sex = Sex.Male
    };

    [Fact]
    public async Task Create_persists_patient_in_current_organization_and_audits_without_cpf()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        var id = await env.Patients.CreateAsync(Valid());

        var saved = await env.Db.Patients.SingleAsync(p => p.Id == id);
        Assert.Equal(s.OrgA, saved.OrganizationId);
        Assert.Equal("Fulano de Tal", saved.FullName);
        Assert.Equal("39053344705", saved.Cpf);

        var log = await env.Db.AuditLogs.SingleAsync(l => l.Action == "Patient.Create");
        Assert.DoesNotContain("39053344705", log.Details ?? "");
    }

    [Fact]
    public async Task Create_rejects_invalid_cpf()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => env.Patients.CreateAsync(Valid("111.111.111-11")));
        Assert.True(ex.Errors.ContainsKey(nameof(CreatePatientDto.Cpf)));
    }

    [Fact]
    public async Task Create_rejects_future_birth_date()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        var dto = Valid();
        dto.BirthDate = new DateTime(2030, 1, 1);

        await Assert.ThrowsAsync<RequestValidationException>(() => env.Patients.CreateAsync(dto));
    }

    [Fact]
    public async Task Details_masks_cpf()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        var details = await env.Patients.GetAsync(s.PatientA1);

        Assert.Equal("***.***.789-09", details.MaskedCpf);
    }
}
