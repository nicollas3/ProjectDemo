using System.ComponentModel.DataAnnotations;
using NeuroCare.Domain;

namespace NeuroCare.Application;

public record PatientHeaderDto(Guid Id, string Name, int Age, PatientStatus Status);

// ---------- Evolução SOAP ----------
public class SaveClinicalNoteDto
{
    [Required(ErrorMessage = "Informe data e horário.")] public DateTime OccurredAtLocal { get; set; }
    [StringLength(4000)] public string? Subjective { get; set; }
    [StringLength(4000)] public string? Objective { get; set; }
    [StringLength(4000)] public string? Assessment { get; set; }
    [StringLength(4000)] public string? Plan { get; set; }
}

public class AddendumDto
{
    [Required(ErrorMessage = "Escreva o adendo."), StringLength(2000)] public string Text { get; set; } = "";
}

public record ClinicalNoteEditModel(Guid PatientId, bool Locked, SaveClinicalNoteDto Form);
public record ClinicalNoteListItemDto(Guid Id, DateTime OccurredAtLocal, string DoctorName, NoteStatus Status, string Preview);
public record AddendumViewDto(DateTime CreatedAtLocal, string DoctorName, string Text);
public record ClinicalNoteDetailsDto(
    Guid Id, Guid PatientId, DateTime OccurredAtLocal, string DoctorName, NoteStatus Status, DateTime? SignedAtLocal,
    string? Subjective, string? Objective, string? Assessment, string? Plan,
    IReadOnlyList<AddendumViewDto> Addenda, bool CanEdit);

// ---------- Medicamentos ----------
public class SaveMedicationDto
{
    [Required(ErrorMessage = "Informe o medicamento."), StringLength(200)] public string Name { get; set; } = "";
    [Required(ErrorMessage = "Informe a dose."), StringLength(100)] public string Dosage { get; set; } = "";
    [Required(ErrorMessage = "Informe a frequência."), StringLength(200)] public string Frequency { get; set; } = "";
    [StringLength(1000)] public string? Instructions { get; set; }
    [Required(ErrorMessage = "Informe a data de início.")] public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool ReminderEnabled { get; set; }
    [StringLength(200)] public string? ReminderTimes { get; set; }
}

public record MedicationEditModel(Guid PatientId, bool Finished, SaveMedicationDto Form);
public record MedicationListItemDto(
    Guid Id, string Name, string Dosage, string Frequency, string? Instructions,
    DateTime StartDate, DateTime? EndDate, MedicationStatus Status, string PrescribedBy, bool ReminderEnabled = false, string? ReminderTimes = null);

// ---------- Sintomas ----------
public class SaveSymptomDto
{
    [Required(ErrorMessage = "Informe data e horário.")] public DateTime OccurredAtLocal { get; set; }
    public SymptomType Type { get; set; } = SymptomType.Other;
    [Range(0, 10, ErrorMessage = "A intensidade deve ficar entre 0 e 10.")] public int Intensity { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}

public record SymptomListItemDto(Guid Id, DateTime OccurredAtLocal, SymptomType Type, int Intensity, string? Notes, bool RecordedByPatient);

// ---------- Crises ----------
public class SaveSeizureDto
{
    [Required(ErrorMessage = "Informe data e horário.")] public DateTime OccurredAtLocal { get; set; }
    public SeizureType Type { get; set; } = SeizureType.Unknown;
    [Range(1, 86400, ErrorMessage = "A duração deve ser de 1 a 86400 segundos.")] public int DurationSeconds { get; set; } = 60;
    public bool LossOfConsciousness { get; set; }
    [StringLength(200)] public string? Trigger { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}

public record SeizureListItemDto(
    Guid Id, DateTime OccurredAtLocal, int DurationSeconds, SeizureType Type,
    bool LossOfConsciousness, string? Trigger, string? Notes, bool RecordedByPatient);

// ---------- Linha do tempo ----------
public enum TimelineKind { Appointment, Note, Medication, Symptom, Seizure, Fall, Questionnaire, Document }
public record TimelineItemDto(DateTime WhenLocal, TimelineKind Kind, string Title, string? Detail, Guid? RefId);

// ---------- Quedas ----------
public class SaveFallDto
{
    [Required(ErrorMessage = "Informe data e horário.")] public DateTime OccurredAtLocal { get; set; }
    public FallCircumstance Circumstance { get; set; } = FallCircumstance.Other;
    [StringLength(200)] public string? Location { get; set; }
    public bool Injury { get; set; }
    public bool NeededMedicalCare { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}

public record FallListItemDto(
    Guid Id, DateTime OccurredAtLocal, FallCircumstance Circumstance, string? Location,
    bool Injury, bool NeededMedicalCare, string? Notes, bool RecordedByPatient);

// ---------- Questionários ----------
public record QuestionnaireSummaryDto(string Key, string Title, string Description, bool ClinicianOnly, int ItemCount, bool IsCustom = false);
public record QuestionOptionDto(int Value, string Label);
public record QuestionItemDto(int Index, string Text, IReadOnlyList<QuestionOptionDto> Options);
public record QuestionnaireFormDto(string Key, string Title, string Description, string Prompt, string Disclaimer, IReadOnlyList<QuestionItemDto> Items);
public record QuestionnaireResponseListItemDto(
    Guid Id, string Key, string Title, DateTime AnsweredAtLocal, int TotalScore, int MaxScore,
    string BandLabel, bool SafetyFlag, bool RecordedByPatient);
public record AnswerViewDto(string Question, string Answer, int Value);
public record QuestionnaireResultDto(
    Guid Id, Guid PatientId, string Key, string Title, DateTime AnsweredAtLocal, int TotalScore, int MaxScore,
    string BandLabel, bool SafetyFlag, string Disclaimer, IReadOnlyList<AnswerViewDto> Answers);
public record SafetyAlertDto(Guid ResponseId, Guid PatientId, string PatientName, string QuestionnaireTitle, DateTime AnsweredAtLocal);

// ---------- Documentos ----------
public class UploadDocumentDto
{
    [Required(ErrorMessage = "Informe um título."), StringLength(200)] public string Title { get; set; } = "";
    public DocumentCategory Category { get; set; } = DocumentCategory.Exam;
}

public record DocumentListItemDto(
    Guid Id, string Title, DocumentCategory Category, string FileName, long SizeBytes,
    DateTime UploadedAtLocal, bool UploadedByPatient);

/// <summary>O chamador deve descartar o Stream (o ASP.NET faz isso ao devolver File(stream, ...)).</summary>
public record DocumentDownload(Stream Content, string FileName, string ContentType);

// ---------- Relatório ----------
public record ReportMedicationDto(string Name, string Dosage, string Frequency, MedicationStatus Status, DateTime StartDate);
public record ReportNoteDto(DateTime OccurredAtLocal, string DoctorName, string? Subjective, string? Objective, string? Assessment, string? Plan);
public record ReportSymptomDto(SymptomType Type, int Count, double AverageIntensity, int MaxIntensity);
public record ReportSeizureDto(DateTime WhenLocal, SeizureType Type, int DurationSeconds, bool LossOfConsciousness);
public record ReportFallDto(DateTime WhenLocal, FallCircumstance Circumstance, bool Injury, bool NeededMedicalCare);
public record ReportQuestionnaireDto(string Title, DateTime LastLocal, int Score, int MaxScore, string Band, int? PreviousScore);
public record ReportDocumentDto(string Title, DocumentCategory Category, DateTime UploadedLocal);

public record PatientReportDto(
    string PatientName, int Age, Sex Sex, string MaskedCpf, string? ResponsibleDoctor, string OrganizationName,
    string GeneratedBy, DateTime GeneratedAtLocal, int PeriodDays,
    IReadOnlyList<ReportMedicationDto> Medications, IReadOnlyList<ReportNoteDto> Notes,
    IReadOnlyList<ReportSymptomDto> Symptoms, IReadOnlyList<ReportSeizureDto> Seizures,
    IReadOnlyList<ReportFallDto> Falls, IReadOnlyList<ReportQuestionnaireDto> Questionnaires,
    IReadOnlyList<ReportDocumentDto> Documents);


// ---------- Questionários por clínica ----------
public class SaveClinicQuestionnaireDto
{
    [Required, StringLength(80)] public string Key { get; set; } = "";
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [Required, StringLength(1000)] public string Description { get; set; } = "";
    [Required, StringLength(500)] public string Prompt { get; set; } = "Considere as últimas duas semanas.";
    public bool ClinicianOnly { get; set; }
    public bool Active { get; set; } = true;
    [Required] public List<ClinicQuestionItemInputDto> Items { get; set; } = [];
    public List<ScoreBandInputDto> Bands { get; set; } = [];
    public int? SafetyItemNumber { get; set; }
}
public class ClinicQuestionItemInputDto
{
    [Required, StringLength(500)] public string Text { get; set; } = "";
    [Required, StringLength(1000)] public string Options { get; set; } = "0=Não;1=Sim";
}
public class ScoreBandInputDto
{
    public int Min { get; set; }
    public int Max { get; set; }
    [Required, StringLength(100)] public string Label { get; set; } = "";
}
public record ClinicQuestionnaireListItemDto(Guid Id, string Key, string Title, bool ClinicianOnly, bool Active, int ItemCount);
public record ClinicQuestionnaireEditDto(Guid? Id, SaveClinicQuestionnaireDto Form);

// ---------- LGPD ----------
public class GrantConsentDto
{
    [Required, StringLength(100)] public string PurposeKey { get; set; } = "";
    [Required, StringLength(1000)] public string PurposeText { get; set; } = "";
    [Required, StringLength(50)] public string Version { get; set; } = "1.0";
    [Range(typeof(bool), "true", "true", ErrorMessage = "É necessário confirmar o consentimento.")]
    public bool Accepted { get; set; }
}
public record ConsentDto(Guid Id, string PurposeKey, string PurposeText, string Version, ConsentStatus Status, DateTime GrantedAtLocal, DateTime? RevokedAtLocal);
public record PersonalDataExport(string FileName, byte[] Content, string ContentType);
