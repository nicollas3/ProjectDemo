using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Infrastructure;

namespace NeuroCare.Tests;

public sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? IpAddress { get; set; } = "127.0.0.1";
    public HashSet<string> RoleSet { get; } = new();
    public bool IsInRole(string role) => RoleSet.Contains(role);

    public static FakeCurrentUser System() => new();

    public static FakeCurrentUser With(string role, Guid? org, Guid? userId = null)
    {
        var u = new FakeCurrentUser { IsAuthenticated = true, OrganizationId = org, UserId = userId ?? Guid.NewGuid() };
        u.RoleSet.Add(role);
        return u;
    }
}

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 3, 10, 15, 0, 0, DateTimeKind.Utc);
    public DateTime ToLocal(DateTime utc) => utc.AddHours(-3);
    public DateTime ToUtc(DateTime local) => DateTime.SpecifyKind(local.AddHours(3), DateTimeKind.Utc);
}

/// <summary>Cenário: duas organizações, cada uma com médico e pacientes. Usuário de paciente na Org A.</summary>
public record Scenario(
    Guid OrgA, Guid OrgB, Guid DoctorA, Guid DoctorB, Guid DoctorAUserId,
    Guid PatientA1, Guid PatientA2, Guid PatientB1, Guid PatientA1UserId, Guid PatientA2UserId);

public sealed class TestEnv : IDisposable
{
    public NeuroCareDbContext Db { get; }
    public FakeCurrentUser User { get; }
    public FakeClock Clock { get; } = new();
    public IPatientService Patients { get; }
    public IAppointmentService Appointments { get; }

    public TestEnv(string dbName, FakeCurrentUser user)
    {
        User = user;
        var options = new DbContextOptionsBuilder<NeuroCareDbContext>().UseInMemoryDatabase(dbName).Options;
        Db = new NeuroCareDbContext(options, user);

        var guard = new AccessGuard(user);
        var patients = new PatientRepository(Db);
        var doctors = new DoctorRepository(Db);
        var appts = new AppointmentRepository(Db);
        var audit = new AuditService(new AuditLogRepository(Db), user, Clock, Db);

        Patients = new PatientService(patients, doctors, appts, Db, guard, audit, Clock, NullLogger<PatientService>.Instance);
        Appointments = new AppointmentService(appts, patients, doctors, Db, guard, user, audit, Clock, NullLogger<AppointmentService>.Instance);
    }

    public static async Task<Scenario> SeedAsync(string dbName)
    {
        using var env = new TestEnv(dbName, FakeCurrentUser.System()); // sem usuário: guarda de tenant não se aplica
        var db = env.Db;
        var orgA = new Organization { Name = "Org A" };
        var orgB = new Organization { Name = "Org B" };
        db.Organizations.AddRange(orgA, orgB);

        var docAUser = Guid.NewGuid();
        var docA = new Doctor { OrganizationId = orgA.Id, UserId = docAUser, FullName = "Dra. A" };
        var docB = new Doctor { OrganizationId = orgB.Id, UserId = Guid.NewGuid(), FullName = "Dr. B" };
        db.Doctors.AddRange(docA, docB);

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var a1 = new Patient { OrganizationId = orgA.Id, UserId = user1, FullName = "Paciente A1", Cpf = "12345678909", BirthDate = new DateTime(1980, 1, 1) };
        var a2 = new Patient { OrganizationId = orgA.Id, UserId = user2, FullName = "Paciente A2", Cpf = "11144477735", BirthDate = new DateTime(1970, 1, 1) };
        var b1 = new Patient { OrganizationId = orgB.Id, FullName = "Paciente B1", Cpf = "52998224725", BirthDate = new DateTime(1960, 1, 1) };
        db.Patients.AddRange(a1, a2, b1);
        await db.SaveChangesAsync();

        return new Scenario(orgA.Id, orgB.Id, docA.Id, docB.Id, docAUser, a1.Id, a2.Id, b1.Id, user1, user2);
    }

    public void Dispose() => Db.Dispose();
}


public sealed class NoopSecurityNotificationService : ISecurityNotificationService
{
    public Task NotifyQuestionnaireSafetyFlagAsync(Patient patient, QuestionnaireResponse response, string questionnaireTitle, CancellationToken ct = default) => Task.CompletedTask;
}
