namespace NeuroCare.Domain;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Toda entidade clínica/operacional pertence a uma organização (tenant).</summary>
public interface ITenantEntity
{
    Guid OrganizationId { get; }
}

public class DomainException(string message) : Exception(message);

public class TenantViolationException() : Exception("Operação entre organizações não permitida.");

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string Doctor = "Doctor";
    public const string Patient = "Patient";
    public const string ClinicAdmin = "ClinicAdmin";
    public const string Researcher = "Researcher";

    public static readonly string[] All = [Administrator, Doctor, Patient, ClinicAdmin, Researcher];
}
