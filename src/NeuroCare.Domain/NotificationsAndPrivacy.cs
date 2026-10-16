namespace NeuroCare.Domain;

public enum NotificationKind { SecurityAlert = 1, AppointmentReminder = 2, MedicationReminder = 3 }
public enum NotificationStatus { Pending = 1, Sent = 2, Failed = 3 }

/// <summary>Registro idempotente de notificações enviadas. Não armazena conteúdo clínico.</summary>
public class NotificationDispatch : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public NotificationKind Kind { get; set; }
    public Guid ReferenceId { get; set; }
    public Guid RecipientUserId { get; set; }
    public string OccurrenceKey { get; set; } = "";
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public DateTime? SentAtUtc { get; set; }
    public string? ErrorCode { get; set; }
}

public enum ConsentStatus { Granted = 1, Revoked = 2 }

/// <summary>Consentimento versionado para finalidade específica. Base legal não é inferida pelo sistema.</summary>
public class ConsentRecord : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid PatientId { get; set; }
    public string PurposeKey { get; set; } = "";
    public string PurposeText { get; set; } = "";
    public string Version { get; set; } = "";
    public ConsentStatus Status { get; set; } = ConsentStatus.Granted;
    public DateTime GrantedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? ActorUserId { get; set; }
    public Patient? Patient { get; set; }
}

/// <summary>Questionário configurável por organização; definição armazenada em JSON.</summary>
public class ClinicQuestionnaire : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Prompt { get; set; } = "";
    public bool ClinicianOnly { get; set; }
    public bool Active { get; set; } = true;
    public string QuestionsJson { get; set; } = "[]";
    public string BandsJson { get; set; } = "[]";
    public int? SafetyItemIndex { get; set; }
}
