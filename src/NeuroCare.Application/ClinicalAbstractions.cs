using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IClinicalNoteRepository
{
    Task<ClinicalNote?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ClinicalNote>> ListByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task AddAsync(ClinicalNote note, CancellationToken ct = default);
    Task AddAddendumAsync(ClinicalNoteAddendum addendum, CancellationToken ct = default);
}

public interface IMedicationRepository
{
    Task<Medication?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Medication>> ListByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<bool> HasOpenWithNameAsync(Guid patientId, string name, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(Medication medication, CancellationToken ct = default);
}

public interface ISymptomRepository
{
    Task<IReadOnlyList<SymptomRecord>> ListByPatientAsync(Guid patientId, DateTime sinceUtc, CancellationToken ct = default);
    Task AddAsync(SymptomRecord record, CancellationToken ct = default);
}

public interface ISeizureRepository
{
    Task<IReadOnlyList<SeizureEvent>> ListByPatientAsync(Guid patientId, DateTime sinceUtc, CancellationToken ct = default);
    Task AddAsync(SeizureEvent seizure, CancellationToken ct = default);
}

public interface IFallRepository
{
    Task<IReadOnlyList<FallEvent>> ListByPatientAsync(Guid patientId, DateTime sinceUtc, CancellationToken ct = default);
    Task AddAsync(FallEvent fall, CancellationToken ct = default);
}

public interface IQuestionnaireResponseRepository
{
    Task<QuestionnaireResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<QuestionnaireResponse>> ListByPatientAsync(Guid patientId, string? key, DateTime sinceUtc, CancellationToken ct = default);
    Task<IReadOnlyList<QuestionnaireResponse>> ListSafetyFlagsAsync(DateTime sinceUtc, int take, CancellationToken ct = default);
    Task AddAsync(QuestionnaireResponse response, CancellationToken ct = default);
}

public interface IDocumentRepository
{
    Task<PatientDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PatientDocument>> ListByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task AddAsync(PatientDocument document, CancellationToken ct = default);
}

/// <summary>Armazenamento de arquivos. A chave é gerada pelo sistema (nunca pelo usuário).</summary>
public interface IFileStorage
{
    long MaxFileBytes { get; }
    Task SaveAsync(string key, byte[] content, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);
}


public interface IClinicQuestionnaireRepository
{
    Task<IReadOnlyList<ClinicQuestionnaire>> ListAsync(bool includeInactive, CancellationToken ct = default);
    Task<ClinicQuestionnaire?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ClinicQuestionnaire?> GetByKeyAsync(string key, bool includeInactive, CancellationToken ct = default);
    Task<bool> KeyExistsAsync(string key, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(ClinicQuestionnaire questionnaire, CancellationToken ct = default);
}

public interface IConsentRepository
{
    Task<IReadOnlyList<ConsentRecord>> ListByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<ConsentRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(ConsentRecord consent, CancellationToken ct = default);
}

public record PatientDataSnapshot(
    Patient Patient,
    IReadOnlyList<Appointment> Appointments,
    IReadOnlyList<ClinicalNote> ClinicalNotes,
    IReadOnlyList<Medication> Medications,
    IReadOnlyList<SymptomRecord> Symptoms,
    IReadOnlyList<SeizureEvent> Seizures,
    IReadOnlyList<FallEvent> Falls,
    IReadOnlyList<QuestionnaireResponse> Questionnaires,
    IReadOnlyList<PatientDocument> Documents,
    IReadOnlyList<ConsentRecord> Consents);

public interface ILgpdExportRepository
{
    Task<PatientDataSnapshot> BuildSnapshotAsync(Guid patientId, CancellationToken ct = default);
}
