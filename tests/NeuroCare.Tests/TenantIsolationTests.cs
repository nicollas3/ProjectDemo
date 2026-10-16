using Microsoft.EntityFrameworkCore;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task Query_filter_returns_only_current_organization_patients()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        var patients = await env.Db.Patients.ToListAsync();

        Assert.Equal(2, patients.Count);
        Assert.All(patients, p => Assert.Equal(s.OrgA, p.OrganizationId));
    }

    [Fact]
    public async Task Doctor_A_cannot_access_patient_of_organization_B()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        await Assert.ThrowsAsync<NotFoundException>(() => env.Patients.GetAsync(s.PatientB1));
        await Assert.ThrowsAsync<NotFoundException>(() => env.Patients.GetForEditAsync(s.PatientB1));
        await Assert.ThrowsAsync<NotFoundException>(() => env.Patients.SetStatusAsync(s.PatientB1, PatientStatus.Inactive));
    }

    [Fact]
    public async Task User_without_organization_sees_no_clinical_data()
    {
        var db = Guid.NewGuid().ToString();
        await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Administrator, null));

        Assert.Empty(await env.Db.Patients.ToListAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Patients.ListAsync(null, false));
    }

    [Fact]
    public async Task Writing_entity_of_another_organization_is_blocked()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        env.Db.Patients.Add(new Patient { OrganizationId = s.OrgB, FullName = "Intruso", Cpf = "39053344705", BirthDate = new DateTime(1990, 1, 1) });

        await Assert.ThrowsAsync<TenantViolationException>(() => env.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Doctor_from_another_organization_cannot_be_assigned_to_patient()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));

        var dto = new CreatePatientDto
        {
            FullName = "Novo Paciente", Cpf = "39053344705", BirthDate = new DateTime(1990, 1, 1),
            ResponsibleDoctorId = s.DoctorB
        };

        await Assert.ThrowsAsync<RequestValidationException>(() => env.Patients.CreateAsync(dto));
    }

    [Fact]
    public async Task Same_cpf_is_allowed_in_different_organizations_but_not_within_one()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);

        // CPF do paciente B1 (52998224725) pode existir na Org A, pois a unicidade é por organização.
        using var envA = new TestEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));
        var id = await envA.Patients.CreateAsync(new CreatePatientDto
        { FullName = "Homônimo", Cpf = "529.982.247-25", BirthDate = new DateTime(1990, 1, 1) });
        Assert.NotEqual(Guid.Empty, id);

        // Mas duplicar dentro da Org A é rejeitado (CPF do A1).
        await Assert.ThrowsAsync<RequestValidationException>(() => envA.Patients.CreateAsync(new CreatePatientDto
        { FullName = "Duplicado", Cpf = "123.456.789-09", BirthDate = new DateTime(1990, 1, 1) }));
    }
}
