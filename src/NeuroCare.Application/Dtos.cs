using System.ComponentModel.DataAnnotations;
using NeuroCare.Domain;

namespace NeuroCare.Application;

public record LookupItemDto(Guid Id, string Name);

public abstract class PatientInputDto
{
    [Required(ErrorMessage = "Informe o nome completo."), StringLength(200)]
    public string FullName { get; set; } = "";
    [StringLength(200)] public string? SocialName { get; set; }
    [Required(ErrorMessage = "Informe a data de nascimento.")]
    public DateTime BirthDate { get; set; }
    public Sex Sex { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    [EmailAddress(ErrorMessage = "E-mail inválido."), StringLength(200)] public string? Email { get; set; }
    [StringLength(300)] public string? Address { get; set; }
    [StringLength(200)] public string? EmergencyContactName { get; set; }
    [StringLength(30)] public string? EmergencyContactPhone { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    public Guid? ResponsibleDoctorId { get; set; }
}

public class CreatePatientDto : PatientInputDto
{
    [Required(ErrorMessage = "Informe o CPF."), StringLength(14)]
    public string Cpf { get; set; } = "";
}

public class UpdatePatientDto : PatientInputDto { }

public record PatientListItemDto(Guid Id, string FullName, string? SocialName, int Age, string? Phone, string? DoctorName, PatientStatus Status);

public record PatientDetailsDto(
    Guid Id, string FullName, string? SocialName, string MaskedCpf, DateTime BirthDate, int Age, Sex Sex,
    string? Phone, string? Email, string? Address, string? EmergencyContactName, string? EmergencyContactPhone,
    string? Notes, Guid? ResponsibleDoctorId, string? ResponsibleDoctorName, PatientStatus Status,
    DateTime CreatedAt, IReadOnlyList<AppointmentListItemDto> Appointments, bool HasPortalAccess);

public record AppointmentListItemDto(
    Guid Id, Guid PatientId, string PatientName, Guid DoctorId, string DoctorName,
    DateTime StartsAtLocal, int DurationMinutes, AppointmentType Type, AppointmentStatus Status);

public class SaveAppointmentDto
{
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    [Required(ErrorMessage = "Informe data e horário.")] public DateTime StartsAtLocal { get; set; }
    [Range(5, 240, ErrorMessage = "Duração deve ficar entre 5 e 240 minutos.")] public int DurationMinutes { get; set; } = 30;
    public AppointmentType Type { get; set; } = AppointmentType.FollowUp;
    [StringLength(2000)] public string? Notes { get; set; }
}

public record DashboardDto(
    int ActivePatients, int ActiveDoctors, int TodayAppointmentsCount, int UpcomingAppointments,
    int PatientsWithoutFollowUp, int NoShowLast30Days, int CancelledLast30Days,
    IReadOnlyList<AppointmentListItemDto> TodayAppointments,
    IReadOnlyList<PatientListItemDto> RecentPatients,
    IReadOnlyDictionary<AppointmentStatus, int> StatusCountsLast30Days,
    IReadOnlyList<SafetyAlertDto> SafetyAlerts);

public record AuditEntry(
    string Action, string EntityName, string? EntityId = null, string? Details = null,
    Guid? UserId = null, Guid? OrganizationId = null);

public record UserSummaryDto(Guid Id, string FullName, string Email, string Role, bool Active, bool EmailConfirmed, Guid? OrganizationId);

public class CreateStaffUserDto
{
    [Required(ErrorMessage = "Informe o nome completo."), StringLength(200)] public string FullName { get; set; } = "";
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido."), StringLength(200)] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Escolha o perfil.")] public string Role { get; set; } = Roles.Doctor;
    [StringLength(30)] public string? LicenseNumber { get; set; }
    [StringLength(100)] public string? Specialty { get; set; }
}

public record CreatedUserResult(Guid Id, bool InviteSent);

public class CreateOrganizationDto
{
    [Required(ErrorMessage = "Informe o nome da organização."), StringLength(200)] public string Name { get; set; } = "";
    [StringLength(200)] public string? LegalName { get; set; }
    [StringLength(20)] public string? DocumentNumber { get; set; }
    [Required(ErrorMessage = "Informe o nome do administrador."), StringLength(200)] public string AdminFullName { get; set; } = "";
    [Required(ErrorMessage = "Informe o e-mail do administrador."), EmailAddress(ErrorMessage = "E-mail inválido."), StringLength(200)] public string AdminEmail { get; set; } = "";
}

public record OrganizationSummaryDto(Guid Id, string Name, string? LegalName, string? DocumentNumber, bool Active, DateTime CreatedAt);
public record CreatedOrganizationResult(Guid Id, bool InviteSent);
