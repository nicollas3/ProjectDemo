namespace NeuroCare.Domain;

public enum NoteStatus { Draft = 1, Signed = 2 }
public enum MedicationStatus { Active = 1, Suspended = 2, Finished = 3 }
public enum MedicationAction { Suspend, Resume, Finish }

public enum SymptomType
{
    Tremor = 1, Rigidity = 2, Bradykinesia = 3, Headache = 4, Dizziness = 5, Weakness = 6,
    SpeechChange = 7, VisionChange = 8, Numbness = 9, Memory = 10, Sleep = 11, Other = 99
}

public enum SeizureType { Unknown = 0, Focal = 1, GeneralizedTonicClonic = 2, Absence = 3, Myoclonic = 4, Other = 9 }

/// <summary>Evolução clínica no formato SOAP. Depois de assinada é imutável; correções via adendo.</summary>
public class ClinicalNote : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? Subjective { get; set; }
    public string? Objective { get; set; }
    public string? Assessment { get; set; }
    public string? Plan { get; set; }
    public NoteStatus Status { get; set; } = NoteStatus.Draft;
    public DateTime? SignedAtUtc { get; set; }

    public Patient? Patient { get; set; }
    public Doctor? Doctor { get; set; }
    public List<ClinicalNoteAddendum> Addenda { get; } = new();

    public bool HasContent =>
        !string.IsNullOrWhiteSpace(Subjective) || !string.IsNullOrWhiteSpace(Objective) ||
        !string.IsNullOrWhiteSpace(Assessment) || !string.IsNullOrWhiteSpace(Plan);

    public void EnsureEditable()
    {
        if (Status == NoteStatus.Signed)
            throw new DomainException("Evolução assinada não pode ser alterada. Registre um adendo.");
    }

    public void Sign(DateTime nowUtc)
    {
        if (Status == NoteStatus.Signed) throw new DomainException("Evolução já assinada.");
        if (!HasContent) throw new DomainException("Preencha ao menos uma seção antes de assinar.");
        Status = NoteStatus.Signed;
        SignedAtUtc = nowUtc;
    }
}

public class ClinicalNoteAddendum : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid ClinicalNoteId { get; set; }
    public Guid DoctorId { get; set; }
    public string Text { get; set; } = "";
    public Doctor? Doctor { get; set; }
}

public class Medication : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid PatientId { get; set; }
    public Guid PrescribedByDoctorId { get; set; }
    public string Name { get; set; } = "";
    public string Dosage { get; set; } = "";
    public string Frequency { get; set; } = "";
    public string? Instructions { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public MedicationStatus Status { get; set; } = MedicationStatus.Active;
    public bool ReminderEnabled { get; set; }
    /// <summary>Horários locais separados por vírgula, ex.: 08:00,20:00.</summary>
    public string? ReminderTimes { get; set; }

    public Patient? Patient { get; set; }
    public Doctor? PrescribedBy { get; set; }

    public void Suspend()
    {
        if (Status != MedicationStatus.Active) throw new DomainException("Somente medicamentos em uso podem ser suspensos.");
        Status = MedicationStatus.Suspended;
    }

    public void Resume()
    {
        if (Status != MedicationStatus.Suspended) throw new DomainException("Somente medicamentos suspensos podem ser retomados.");
        Status = MedicationStatus.Active;
    }

    public void Finish(DateTime endDate)
    {
        if (Status == MedicationStatus.Finished) throw new DomainException("Medicamento já finalizado.");
        EndDate = endDate.Date < StartDate.Date ? StartDate.Date : endDate.Date;
        Status = MedicationStatus.Finished;
    }
}

public class SymptomRecord : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid PatientId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public SymptomType Type { get; set; } = SymptomType.Other;
    /// <summary>0 (ausente) a 10 (máxima).</summary>
    public int Intensity { get; set; }
    public string? Notes { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public bool RecordedByPatient { get; set; }
}

public class SeizureEvent : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid PatientId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public int DurationSeconds { get; set; }
    public SeizureType Type { get; set; } = SeizureType.Unknown;
    public bool LossOfConsciousness { get; set; }
    public string? Trigger { get; set; }
    public string? Notes { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public bool RecordedByPatient { get; set; }
}
