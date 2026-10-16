using NeuroCare.Domain;

namespace NeuroCare.Application;

public static class AppClaims
{
    public const string OrganizationId = "neurocare:org";
    public const string FullName = "neurocare:name";
}

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? OrganizationId { get; }
    string? IpAddress { get; }
    bool IsInRole(string role);
}

public interface IClock
{
    DateTime UtcNow { get; }
    DateTime ToLocal(DateTime utc);
    DateTime ToUtc(DateTime local);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Patient?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Patient>> SearchAsync(string? term, bool includeInactive, CancellationToken ct = default);
    Task<bool> CpfExistsAsync(string cpf, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(Patient patient, CancellationToken ct = default);
    Task<int> CountByStatusAsync(PatientStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Patient>> RecentAsync(int take, CancellationToken ct = default);
    Task<int> CountWithoutFollowUpAsync(DateTime sinceUtc, CancellationToken ct = default);
}

public interface IDoctorRepository
{
    Task<Doctor?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Doctor>> ListActiveAsync(CancellationToken ct = default);
    Task<int> CountActiveAsync(CancellationToken ct = default);
    Task<Doctor?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Doctor doctor, CancellationToken ct = default);
}

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> ListAsync(DateTime? fromUtc, DateTime? toUtc, Guid? doctorId, Guid? patientId, CancellationToken ct = default);
    Task<bool> HasConflictAsync(Guid doctorId, DateTime startUtc, DateTime endUtc, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(Appointment appointment, CancellationToken ct = default);
    Task<IReadOnlyDictionary<AppointmentStatus, int>> CountByStatusAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<int> CountUpcomingAsync(DateTime fromUtc, CancellationToken ct = default);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log, CancellationToken ct = default);
}

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Organization>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Organization organization, CancellationToken ct = default);
    Task<bool> IsActiveAsync(Guid id, CancellationToken ct = default);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>Monta links absolutos (implementado na Web, usando App:PublicBaseUrl).</summary>
public interface ILinkBuilder
{
    string ResetPassword(Guid userId, string token);
}

public record NewIdentityUser(string Email, string FullName, Guid? OrganizationId, string Role);

/// <summary>Acesso ao Identity sem expor UserManager à camada Application.</summary>
public interface IIdentityGateway
{
    Task<Guid> CreateUserAsync(NewIdentityUser user, CancellationToken ct = default);
    Task<string> GenerateResetTokenAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserSummaryDto>> ListUsersAsync(Guid organizationId, CancellationToken ct = default);
    Task<UserSummaryDto?> FindAsync(Guid userId, CancellationToken ct = default);
    Task SetActiveAsync(Guid userId, bool active, CancellationToken ct = default);
    Task RevokeSessionsAsync(Guid organizationId, CancellationToken ct = default);
    Task<string?> GetEmailAsync(Guid userId, CancellationToken ct = default);
}
