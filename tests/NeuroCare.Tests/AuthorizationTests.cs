using Microsoft.EntityFrameworkCore;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Tests;

public class AuthorizationTests
{
    [Fact]
    public async Task Patient_can_view_own_record()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA1UserId));

        var details = await env.Patients.GetAsync(s.PatientA1);

        Assert.Equal("Paciente A1", details.FullName);
    }

    [Fact]
    public async Task Patient_A_cannot_view_patient_B_of_same_organization()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA1UserId));

        await Assert.ThrowsAsync<ForbiddenException>(() => env.Patients.GetAsync(s.PatientA2));
    }

    [Fact]
    public async Task Patient_cannot_list_patients_or_edit()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(Roles.Patient, s.OrgA, s.PatientA1UserId));

        await Assert.ThrowsAsync<ForbiddenException>(() => env.Patients.ListAsync(null, false));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Patients.SetStatusAsync(s.PatientA1, PatientStatus.Inactive));
    }

    [Theory]
    [InlineData(Roles.Researcher)]
    [InlineData(Roles.Administrator)]
    public async Task User_without_clinical_permission_cannot_access_record(string role)
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.With(role, s.OrgA));

        await Assert.ThrowsAsync<ForbiddenException>(() => env.Patients.GetAsync(s.PatientA1));
    }

    [Fact]
    public async Task Anonymous_user_cannot_access_record()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new TestEnv(db, FakeCurrentUser.System());

        await Assert.ThrowsAsync<ForbiddenException>(() => env.Patients.GetAsync(s.PatientA1));
    }

    [Fact]
    public async Task Viewing_a_record_creates_audit_log_without_clinical_data()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        var user = FakeCurrentUser.With(Roles.Doctor, s.OrgA);
        using var env = new TestEnv(db, user);

        await env.Patients.GetAsync(s.PatientA1);

        var log = await env.Db.AuditLogs.SingleAsync(l => l.Action == "Patient.View");
        Assert.Equal(user.UserId, log.UserId);
        Assert.Equal(s.OrgA, log.OrganizationId);
        Assert.Equal(s.PatientA1.ToString(), log.EntityId);
        Assert.Null(log.Details);
    }
}
