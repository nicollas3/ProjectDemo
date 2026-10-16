namespace NeuroCare.Domain;

public class Doctor : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    /// <summary>Id do usuário (Identity) associado.</summary>
    public Guid UserId { get; set; }
    public string FullName { get; set; } = "";
    public string? LicenseNumber { get; set; }
    public string? Specialty { get; set; }
    public bool Active { get; set; } = true;
}
