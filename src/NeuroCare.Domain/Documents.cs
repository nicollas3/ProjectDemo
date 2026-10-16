namespace NeuroCare.Domain;

public enum DocumentCategory { Exam = 1, Report = 2, Prescription = 3, Imaging = 4, Other = 9 }

/// <summary>Metadados do documento. O arquivo fica no armazenamento (fora de wwwroot), referenciado por StorageKey.</summary>
public class PatientDocument : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid PatientId { get; set; }
    public string Title { get; set; } = "";
    public DocumentCategory Category { get; set; } = DocumentCategory.Other;
    public string OriginalFileName { get; set; } = "";
    /// <summary>Detectado pelo conteúdo do arquivo (nunca pelo cabeçalho enviado pelo cliente).</summary>
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public Guid? UploadedByUserId { get; set; }
    public bool UploadedByPatient { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public Patient? Patient { get; set; }
}
