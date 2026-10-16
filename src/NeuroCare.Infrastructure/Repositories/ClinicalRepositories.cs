using Microsoft.EntityFrameworkCore;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Infrastructure;

public class ClinicalNoteRepository(NeuroCareDbContext db) : IClinicalNoteRepository
{
    public Task<ClinicalNote?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ClinicalNotes
            .Include(n => n.Doctor)
            .Include(n => n.Addenda).ThenInclude(a => a.Doctor)
            .FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<ClinicalNote>> ListByPatientAsync(Guid patientId, CancellationToken ct = default) =>
        await db.ClinicalNotes.AsNoTracking().Include(n => n.Doctor)
            .Where(n => n.PatientId == patientId)
            .OrderByDescending(n => n.OccurredAtUtc).Take(500).ToListAsync(ct);

    public async Task AddAsync(ClinicalNote note, CancellationToken ct = default) =>
        await db.ClinicalNotes.AddAsync(note, ct);

    public async Task AddAddendumAsync(ClinicalNoteAddendum addendum, CancellationToken ct = default) =>
        await db.ClinicalNoteAddenda.AddAsync(addendum, ct);
}

public class MedicationRepository(NeuroCareDbContext db) : IMedicationRepository
{
    public Task<Medication?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Medications.Include(m => m.PrescribedBy).FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<IReadOnlyList<Medication>> ListByPatientAsync(Guid patientId, CancellationToken ct = default) =>
        await db.Medications.AsNoTracking().Include(m => m.PrescribedBy)
            .Where(m => m.PatientId == patientId)
            .OrderBy(m => m.Status).ThenBy(m => m.Name).Take(500).ToListAsync(ct);

    public Task<bool> HasOpenWithNameAsync(Guid patientId, string name, Guid? excludeId, CancellationToken ct = default)
    {
        var lower = name.ToLower();
        return db.Medications.AnyAsync(m => m.PatientId == patientId
            && m.Status != MedicationStatus.Finished
            && m.Name.ToLower() == lower
            && (excludeId == null || m.Id != excludeId.Value), ct);
    }

    public async Task AddAsync(Medication medication, CancellationToken ct = default) =>
        await db.Medications.AddAsync(medication, ct);
}

public class SymptomRepository(NeuroCareDbContext db) : ISymptomRepository
{
    public async Task<IReadOnlyList<SymptomRecord>> ListByPatientAsync(Guid patientId, DateTime sinceUtc, CancellationToken ct = default) =>
        await db.SymptomRecords.AsNoTracking()
            .Where(s => s.PatientId == patientId && s.OccurredAtUtc >= sinceUtc)
            .OrderByDescending(s => s.OccurredAtUtc).Take(1000).ToListAsync(ct);

    public async Task AddAsync(SymptomRecord record, CancellationToken ct = default) =>
        await db.SymptomRecords.AddAsync(record, ct);
}

public class SeizureRepository(NeuroCareDbContext db) : ISeizureRepository
{
    public async Task<IReadOnlyList<SeizureEvent>> ListByPatientAsync(Guid patientId, DateTime sinceUtc, CancellationToken ct = default) =>
        await db.SeizureEvents.AsNoTracking()
            .Where(s => s.PatientId == patientId && s.OccurredAtUtc >= sinceUtc)
            .OrderByDescending(s => s.OccurredAtUtc).Take(1000).ToListAsync(ct);

    public async Task AddAsync(SeizureEvent seizure, CancellationToken ct = default) =>
        await db.SeizureEvents.AddAsync(seizure, ct);
}

public class FallRepository(NeuroCareDbContext db) : IFallRepository
{
    public async Task<IReadOnlyList<FallEvent>> ListByPatientAsync(Guid patientId, DateTime sinceUtc, CancellationToken ct = default) =>
        await db.FallEvents.AsNoTracking()
            .Where(f => f.PatientId == patientId && f.OccurredAtUtc >= sinceUtc)
            .OrderByDescending(f => f.OccurredAtUtc).Take(1000).ToListAsync(ct);

    public async Task AddAsync(FallEvent fall, CancellationToken ct = default) =>
        await db.FallEvents.AddAsync(fall, ct);
}

public class QuestionnaireResponseRepository(NeuroCareDbContext db) : IQuestionnaireResponseRepository
{
    public Task<QuestionnaireResponse?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.QuestionnaireResponses.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<QuestionnaireResponse>> ListByPatientAsync(Guid patientId, string? key, DateTime sinceUtc, CancellationToken ct = default)
    {
        IQueryable<QuestionnaireResponse> q = db.QuestionnaireResponses.AsNoTracking()
            .Where(r => r.PatientId == patientId && r.AnsweredAtUtc >= sinceUtc);
        if (!string.IsNullOrWhiteSpace(key)) q = q.Where(r => r.QuestionnaireKey == key);
        return await q.OrderByDescending(r => r.AnsweredAtUtc).Take(1000).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<QuestionnaireResponse>> ListSafetyFlagsAsync(DateTime sinceUtc, int take, CancellationToken ct = default) =>
        await db.QuestionnaireResponses.AsNoTracking().Include(r => r.Patient)
            .Where(r => r.SafetyFlag && r.AnsweredAtUtc >= sinceUtc)
            .OrderByDescending(r => r.AnsweredAtUtc).Take(take).ToListAsync(ct);

    public async Task AddAsync(QuestionnaireResponse response, CancellationToken ct = default) =>
        await db.QuestionnaireResponses.AddAsync(response, ct);
}

public class DocumentRepository(NeuroCareDbContext db) : IDocumentRepository
{
    public Task<PatientDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.PatientDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<PatientDocument>> ListByPatientAsync(Guid patientId, CancellationToken ct = default) =>
        await db.PatientDocuments.AsNoTracking()
            .Where(d => d.PatientId == patientId && d.DeletedAtUtc == null)
            .OrderByDescending(d => d.CreatedAt).Take(500).ToListAsync(ct);

    public async Task AddAsync(PatientDocument document, CancellationToken ct = default) =>
        await db.PatientDocuments.AddAsync(document, ct);
}

public class ClinicQuestionnaireRepository(NeuroCareDbContext db) : IClinicQuestionnaireRepository
{
    public async Task<IReadOnlyList<ClinicQuestionnaire>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        IQueryable<ClinicQuestionnaire> q = db.ClinicQuestionnaires.AsNoTracking();
        if (!includeInactive) q = q.Where(x => x.Active);
        return await q.OrderBy(x => x.Title).Take(500).ToListAsync(ct);
    }

    public Task<ClinicQuestionnaire?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ClinicQuestionnaires.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<ClinicQuestionnaire?> GetByKeyAsync(string key, bool includeInactive, CancellationToken ct = default)
    {
        IQueryable<ClinicQuestionnaire> q = db.ClinicQuestionnaires;
        if (!includeInactive) q = q.Where(x => x.Active);
        return q.FirstOrDefaultAsync(x => x.Key == key, ct);
    }

    public Task<bool> KeyExistsAsync(string key, Guid? excludeId, CancellationToken ct = default) =>
        db.ClinicQuestionnaires.AnyAsync(x => x.Key == key && (!excludeId.HasValue || x.Id != excludeId.Value), ct);

    public async Task AddAsync(ClinicQuestionnaire questionnaire, CancellationToken ct = default) =>
        await db.ClinicQuestionnaires.AddAsync(questionnaire, ct);
}

public class ConsentRepository(NeuroCareDbContext db) : IConsentRepository
{
    public async Task<IReadOnlyList<ConsentRecord>> ListByPatientAsync(Guid patientId, CancellationToken ct = default) =>
        await db.ConsentRecords.AsNoTracking().Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.GrantedAtUtc).Take(500).ToListAsync(ct);

    public Task<ConsentRecord?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ConsentRecords.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(ConsentRecord consent, CancellationToken ct = default) =>
        await db.ConsentRecords.AddAsync(consent, ct);
}

public class LgpdExportRepository(NeuroCareDbContext db) : ILgpdExportRepository
{
    public async Task<PatientDataSnapshot> BuildSnapshotAsync(Guid patientId, CancellationToken ct = default)
    {
        var patient = await db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == patientId, ct)
            ?? throw new NotFoundException("Paciente não encontrado.");
        var appointments = await db.Appointments.AsNoTracking().Where(x => x.PatientId == patientId).OrderBy(x => x.StartsAtUtc).ToListAsync(ct);
        var notes = await db.ClinicalNotes.AsNoTracking().Where(x => x.PatientId == patientId).OrderBy(x => x.OccurredAtUtc).ToListAsync(ct);
        var meds = await db.Medications.AsNoTracking().Where(x => x.PatientId == patientId).OrderBy(x => x.StartDate).ToListAsync(ct);
        var symptoms = await db.SymptomRecords.AsNoTracking().Where(x => x.PatientId == patientId).OrderBy(x => x.OccurredAtUtc).ToListAsync(ct);
        var seizures = await db.SeizureEvents.AsNoTracking().Where(x => x.PatientId == patientId).OrderBy(x => x.OccurredAtUtc).ToListAsync(ct);
        var falls = await db.FallEvents.AsNoTracking().Where(x => x.PatientId == patientId).OrderBy(x => x.OccurredAtUtc).ToListAsync(ct);
        var questionnaires = await db.QuestionnaireResponses.AsNoTracking().Where(x => x.PatientId == patientId).OrderBy(x => x.AnsweredAtUtc).ToListAsync(ct);
        var documents = await db.PatientDocuments.AsNoTracking().Where(x => x.PatientId == patientId && x.DeletedAtUtc == null).OrderBy(x => x.CreatedAt).ToListAsync(ct);
        var consents = await db.ConsentRecords.AsNoTracking().Where(x => x.PatientId == patientId).OrderBy(x => x.GrantedAtUtc).ToListAsync(ct);
        return new PatientDataSnapshot(patient, appointments, notes, meds, symptoms, seizures, falls, questionnaires, documents, consents);
    }
}
