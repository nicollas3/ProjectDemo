using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Infrastructure;

namespace NeuroCare.Tests;

public sealed class FakeIdentityGateway : IIdentityGateway
{
    public List<UserSummaryDto> Users { get; } = new();
    public List<Guid> RevokedOrganizations { get; } = new();

    public Task<Guid> CreateUserAsync(NewIdentityUser user, CancellationToken ct = default)
    {
        if (Users.Any(u => u.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase)))
            throw new RequestValidationException("Email", "E-mail indisponível para cadastro.");
        var id = Guid.NewGuid();
        Users.Add(new UserSummaryDto(id, user.FullName, user.Email, user.Role, true, false, user.OrganizationId));
        return Task.FromResult(id);
    }

    public Task<string> GenerateResetTokenAsync(Guid userId, CancellationToken ct = default) => Task.FromResult("token");

    public Task<IReadOnlyList<UserSummaryDto>> ListUsersAsync(Guid organizationId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<UserSummaryDto>>(Users.Where(u => u.OrganizationId == organizationId).ToList());

    public Task<UserSummaryDto?> FindAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));

    public Task SetActiveAsync(Guid userId, bool active, CancellationToken ct = default)
    {
        var i = Users.FindIndex(u => u.Id == userId);
        Users[i] = Users[i] with { Active = active };
        return Task.CompletedTask;
    }

    public Task RevokeSessionsAsync(Guid organizationId, CancellationToken ct = default)
    {
        RevokedOrganizations.Add(organizationId);
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == userId)?.Email);
}

public sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();
    public bool Fail { get; set; }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (Fail) throw new InvalidOperationException("SMTP indisponível");
        Sent.Add((to, subject, htmlBody));
        return Task.CompletedTask;
    }
}

public sealed class FakeLinkBuilder : ILinkBuilder
{
    public string ResetPassword(Guid userId, string token) => $"https://test.local/reset?u={userId}&t={token}";
}

public sealed class AdminEnv : IDisposable
{
    public TestEnv Base { get; }
    public FakeIdentityGateway Identity { get; } = new();
    public FakeEmailSender Email { get; } = new();
    public IUserAdminService Users { get; }
    public IOrganizationService Orgs { get; }

    public AdminEnv(string dbName, FakeCurrentUser user)
    {
        Base = new TestEnv(dbName, user);
        var guard = new AccessGuard(user);
        var audit = new AuditService(new AuditLogRepository(Base.Db), user, Base.Clock, Base.Db);
        var invites = new InviteService(Identity, new FakeLinkBuilder(), Email, NullLogger<InviteService>.Instance);

        Users = new UserAdminService(Identity, new DoctorRepository(Base.Db), new PatientRepository(Base.Db), Base.Db,
            guard, user, audit, invites, NullLogger<UserAdminService>.Instance);
        Orgs = new OrganizationService(new OrganizationRepository(Base.Db), Identity, Base.Db,
            guard, audit, invites, NullLogger<OrganizationService>.Instance);
    }

    public void Dispose() => Base.Dispose();
}

public class OrganizationServiceTests
{
    private static CreateOrganizationDto NewOrg(string email = "gestor@nova.local") => new()
    { Name = "Clínica Nova", AdminFullName = "Gestor Novo", AdminEmail = email };

    [Fact]
    public async Task Administrator_creates_organization_and_invites_clinic_admin()
    {
        var db = Guid.NewGuid().ToString();
        await TestEnv.SeedAsync(db);
        using var env = new AdminEnv(db, FakeCurrentUser.With(Roles.Administrator, null));

        var result = await env.Orgs.CreateAsync(NewOrg());

        Assert.True(result.InviteSent);
        var org = await env.Base.Db.Organizations.SingleAsync(o => o.Id == result.Id);
        Assert.Equal("Clínica Nova", org.Name);
        var admin = Assert.Single(env.Identity.Users);
        Assert.Equal(Roles.ClinicAdmin, admin.Role);
        Assert.Equal(org.Id, admin.OrganizationId);
        Assert.Single(env.Email.Sent);
        Assert.Equal("gestor@nova.local", env.Email.Sent[0].To);
    }

    [Theory]
    [InlineData(Roles.ClinicAdmin)]
    [InlineData(Roles.Doctor)]
    [InlineData(Roles.Patient)]
    public async Task Non_administrators_cannot_manage_organizations(string role)
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new AdminEnv(db, FakeCurrentUser.With(role, s.OrgA));

        await Assert.ThrowsAsync<ForbiddenException>(() => env.Orgs.CreateAsync(NewOrg()));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Orgs.ListAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Orgs.SetActiveAsync(s.OrgA, false));
    }

    [Fact]
    public async Task Duplicate_admin_email_does_not_leave_orphan_organization()
    {
        var db = Guid.NewGuid().ToString();
        await TestEnv.SeedAsync(db);
        using var env = new AdminEnv(db, FakeCurrentUser.With(Roles.Administrator, null));
        await env.Orgs.CreateAsync(NewOrg("igual@nova.local"));
        var before = await env.Base.Db.Organizations.CountAsync();

        await Assert.ThrowsAsync<RequestValidationException>(() => env.Orgs.CreateAsync(NewOrg("igual@nova.local")));

        Assert.Equal(before, await env.Base.Db.Organizations.CountAsync());
    }

    [Fact]
    public async Task Deactivating_organization_revokes_sessions()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new AdminEnv(db, FakeCurrentUser.With(Roles.Administrator, null));

        await env.Orgs.SetActiveAsync(s.OrgA, false);

        Assert.False((await env.Base.Db.Organizations.SingleAsync(o => o.Id == s.OrgA)).Active);
        Assert.Contains(s.OrgA, env.Identity.RevokedOrganizations);
    }
}

public class UserAdminServiceTests
{
    private static CreateStaffUserDto Doctor(string email = "novo.medico@a.local") => new()
    { FullName = "Novo Médico", Email = email, Role = Roles.Doctor, LicenseNumber = "CRM-XX 999", Specialty = "Neurologia" };

    [Fact]
    public async Task ClinicAdmin_creates_doctor_in_own_organization_and_sends_invite()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new AdminEnv(db, FakeCurrentUser.With(Roles.ClinicAdmin, s.OrgA));

        var result = await env.Users.CreateStaffAsync(Doctor());

        Assert.True(result.InviteSent);
        var doctor = await env.Base.Db.Doctors.SingleAsync(d => d.UserId == result.Id);
        Assert.Equal(s.OrgA, doctor.OrganizationId);
        Assert.True(doctor.Active);
        Assert.Equal(s.OrgA, env.Identity.Users.Single().OrganizationId);
        Assert.Single(env.Email.Sent);
    }

    [Fact]
    public async Task Invite_failure_does_not_roll_back_user_creation()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new AdminEnv(db, FakeCurrentUser.With(Roles.ClinicAdmin, s.OrgA));
        env.Email.Fail = true;

        var result = await env.Users.CreateStaffAsync(Doctor());

        Assert.False(result.InviteSent);
        Assert.Single(env.Identity.Users);
    }

    [Theory]
    [InlineData(Roles.Doctor)]
    [InlineData(Roles.Patient)]
    [InlineData(Roles.Administrator)]
    public async Task Only_clinic_admin_can_manage_users(string role)
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new AdminEnv(db, FakeCurrentUser.With(role, role == Roles.Administrator ? null : s.OrgA));

        await Assert.ThrowsAsync<ForbiddenException>(() => env.Users.CreateStaffAsync(Doctor()));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Users.ListAsync());
    }

    [Fact]
    public async Task Cannot_create_platform_administrator_through_staff_form()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new AdminEnv(db, FakeCurrentUser.With(Roles.ClinicAdmin, s.OrgA));
        var dto = Doctor();
        dto.Role = Roles.Administrator;

        await Assert.ThrowsAsync<RequestValidationException>(() => env.Users.CreateStaffAsync(dto));
        Assert.Empty(env.Identity.Users);
    }

    [Fact]
    public async Task ClinicAdmin_cannot_deactivate_user_of_another_organization()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new AdminEnv(db, FakeCurrentUser.With(Roles.ClinicAdmin, s.OrgA));
        var foreign = new UserSummaryDto(Guid.NewGuid(), "Outro", "outro@b.local", Roles.Doctor, true, true, s.OrgB);
        env.Identity.Users.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() => env.Users.SetActiveAsync(foreign.Id, false));
        await Assert.ThrowsAsync<NotFoundException>(() => env.Users.ResendInviteAsync(foreign.Id));
        Assert.True(env.Identity.Users.Single().Active);
    }

    [Fact]
    public async Task ClinicAdmin_cannot_deactivate_self()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        var me = FakeCurrentUser.With(Roles.ClinicAdmin, s.OrgA);
        using var env = new AdminEnv(db, me);
        env.Identity.Users.Add(new UserSummaryDto(me.UserId!.Value, "Eu", "eu@a.local", Roles.ClinicAdmin, true, true, s.OrgA));

        await Assert.ThrowsAsync<RequestValidationException>(() => env.Users.SetActiveAsync(me.UserId.Value, false));
    }

    [Fact]
    public async Task Deactivating_doctor_user_also_deactivates_doctor_record()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new AdminEnv(db, FakeCurrentUser.With(Roles.ClinicAdmin, s.OrgA));
        var created = await env.Users.CreateStaffAsync(Doctor());

        await env.Users.SetActiveAsync(created.Id, false);

        Assert.False((await env.Base.Db.Doctors.SingleAsync(d => d.UserId == created.Id)).Active);
    }

    [Fact]
    public async Task Resend_invite_is_rejected_when_account_already_activated()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new AdminEnv(db, FakeCurrentUser.With(Roles.ClinicAdmin, s.OrgA));
        var active = new UserSummaryDto(Guid.NewGuid(), "Ativo", "ativo@a.local", Roles.Doctor, true, true, s.OrgA);
        env.Identity.Users.Add(active);

        await Assert.ThrowsAsync<RequestValidationException>(() => env.Users.ResendInviteAsync(active.Id));
    }

    [Fact]
    public async Task Doctor_invites_patient_to_portal_and_links_user()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new AdminEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));
        var patientId = await env.Base.Patients.CreateAsync(new CreatePatientDto
        { FullName = "Com E-mail", Cpf = "390.533.447-05", BirthDate = new DateTime(1990, 1, 1), Email = "com.email@a.local" });

        var sent = await env.Users.InvitePatientAsync(patientId);

        Assert.True(sent);
        var patient = await env.Base.Db.Patients.SingleAsync(p => p.Id == patientId);
        Assert.NotNull(patient.UserId);
        Assert.Equal(Roles.Patient, env.Identity.Users.Single().Role);
        await Assert.ThrowsAsync<RequestValidationException>(() => env.Users.InvitePatientAsync(patientId)); // já tem acesso
    }

    [Fact]
    public async Task Patient_portal_invite_requires_email_and_same_organization()
    {
        var db = Guid.NewGuid().ToString();
        var s = await TestEnv.SeedAsync(db);
        using var env = new AdminEnv(db, FakeCurrentUser.With(Roles.Doctor, s.OrgA));
        var noEmail = await env.Base.Patients.CreateAsync(new CreatePatientDto
        { FullName = "Sem E-mail", Cpf = "390.533.447-05", BirthDate = new DateTime(1990, 1, 1) });

        await Assert.ThrowsAsync<RequestValidationException>(() => env.Users.InvitePatientAsync(noEmail));
        await Assert.ThrowsAsync<NotFoundException>(() => env.Users.InvitePatientAsync(s.PatientB1));
    }
}
