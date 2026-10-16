namespace NeuroCare.Domain;

public class Patient : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ResponsibleDoctorId { get; set; }

    public string FullName { get; set; } = "";
    public string? SocialName { get; set; }
    /// <summary>Somente dígitos.</summary>
    public string Cpf { get; set; } = "";
    public DateTime BirthDate { get; set; }
    public Sex Sex { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? Notes { get; set; }
    public PatientStatus Status { get; set; } = PatientStatus.Active;

    public Organization? Organization { get; set; }
    public Doctor? ResponsibleDoctor { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(SocialName) ? FullName : SocialName!;

    public string MaskedCpf => Cpf.Length == 11 ? $"***.***.{Cpf[6..9]}-{Cpf[9..]}" : "***";

    public int AgeOn(DateTime date)
    {
        var age = date.Year - BirthDate.Year;
        if (BirthDate.Date > date.Date.AddYears(-age)) age--;
        return age;
    }
}
