using System.Text.Json;
using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface ILgpdService
{
    Task<IReadOnlyList<ConsentDto>> ListConsentsAsync(CancellationToken ct = default);
    Task<Guid> GrantConsentAsync(GrantConsentDto dto, CancellationToken ct = default);
    Task RevokeConsentAsync(Guid id, CancellationToken ct = default);
    Task<PersonalDataExport> ExportMyDataAsync(CancellationToken ct = default);
}

public class LgpdService(
    ClinicalContext context, IConsentRepository consents, ILgpdExportRepository exportRepository,
    IUnitOfWork uow, AccessGuard guard, ICurrentUser user, IAuditService audit, IClock clock) : ILgpdService
{
    public async Task<IReadOnlyList<ConsentDto>> ListConsentsAsync(CancellationToken ct = default)
    {
        RequirePatient();
        var patient = await context.ResolveAsync(null, ct);
        var list = await consents.ListByPatientAsync(patient.Id, ct);
        return list.OrderByDescending(x => x.GrantedAtUtc).Select(ToDto).ToList();
    }

    public async Task<Guid> GrantConsentAsync(GrantConsentDto dto, CancellationToken ct = default)
    {
        RequirePatient();
        if (!dto.Accepted) throw new RequestValidationException(nameof(dto.Accepted), "É necessário confirmar o consentimento.");
        var purpose = ConsentPurposeCatalog.Find(dto.PurposeKey) ?? throw new RequestValidationException(nameof(dto.PurposeKey), "Finalidade de consentimento inválida.");
        var org = guard.RequireOrganization();
        var patient = await context.ResolveAsync(null, ct);
        var existing = await consents.ListByPatientAsync(patient.Id, ct);
        if (existing.Any(x => x.PurposeKey == purpose.Key && x.Version == purpose.Version && x.Status == ConsentStatus.Granted))
            throw new RequestValidationException(nameof(dto.PurposeKey), "Este consentimento já está ativo.");
        var record = new ConsentRecord
        {
            OrganizationId = org, PatientId = patient.Id, PurposeKey = purpose.Key,
            PurposeText = purpose.Text, Version = purpose.Version, Status = ConsentStatus.Granted,
            GrantedAtUtc = clock.UtcNow, ActorUserId = user.UserId
        };
        await consents.AddAsync(record, ct);
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("LGPD.Consent.Grant", nameof(ConsentRecord), record.Id.ToString()), ct);
        return record.Id;
    }

    public async Task RevokeConsentAsync(Guid id, CancellationToken ct = default)
    {
        RequirePatient();
        var patient = await context.ResolveAsync(null, ct);
        var record = await consents.GetByIdAsync(id, ct);
        if (record is null || record.PatientId != patient.Id) throw new NotFoundException("Consentimento não encontrado.");
        if (record.Status == ConsentStatus.Revoked) return;
        record.Status = ConsentStatus.Revoked;
        record.RevokedAtUtc = clock.UtcNow;
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("LGPD.Consent.Revoke", nameof(ConsentRecord), record.Id.ToString()), ct);
    }

    public async Task<PersonalDataExport> ExportMyDataAsync(CancellationToken ct = default)
    {
        RequirePatient();
        var patient = await context.ResolveAsync(null, ct);
        var snapshot = await exportRepository.BuildSnapshotAsync(patient.Id, ct);
        var payload = new
        {
            exportedAtUtc = clock.UtcNow,
            notice = "Exportação de dados pessoais do titular. O arquivo pode conter dados pessoais sensíveis e deve ser armazenado com segurança.",
            patient = new { snapshot.Patient.FullName, snapshot.Patient.SocialName, snapshot.Patient.Cpf, snapshot.Patient.BirthDate, snapshot.Patient.Sex, snapshot.Patient.Phone, snapshot.Patient.Email, snapshot.Patient.Address, snapshot.Patient.EmergencyContactName, snapshot.Patient.EmergencyContactPhone, snapshot.Patient.Notes, snapshot.Patient.Status },
            appointments = snapshot.Appointments.Select(x => new { x.StartsAtUtc, x.DurationMinutes, x.Type, x.Status, x.Notes }),
            clinicalNotes = snapshot.ClinicalNotes.Select(x => new { x.OccurredAtUtc, x.Subjective, x.Objective, x.Assessment, x.Plan, x.Status, x.SignedAtUtc }),
            medications = snapshot.Medications.Select(x => new { x.Name, x.Dosage, x.Frequency, x.Instructions, x.StartDate, x.EndDate, x.Status }),
            symptoms = snapshot.Symptoms.Select(x => new { x.OccurredAtUtc, x.Type, x.Intensity, x.Notes }),
            seizures = snapshot.Seizures.Select(x => new { x.OccurredAtUtc, x.DurationSeconds, x.Type, x.LossOfConsciousness, x.Trigger, x.Notes }),
            falls = snapshot.Falls.Select(x => new { x.OccurredAtUtc, x.Circumstance, x.Location, x.Injury, x.NeededMedicalCare, x.Notes }),
            questionnaires = snapshot.Questionnaires.Select(x => new { x.QuestionnaireKey, x.AnsweredAtUtc, x.AnswersJson, x.TotalScore, x.BandLabel }),
            documents = snapshot.Documents.Select(x => new { x.Title, x.Category, x.OriginalFileName, x.ContentType, x.SizeBytes, x.Sha256, x.CreatedAt }),
            consents = snapshot.Consents.Select(x => new { x.PurposeKey, x.PurposeText, x.Version, x.Status, x.GrantedAtUtc, x.RevokedAtUtc })
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { WriteIndented = true });
        await audit.RecordAsync(new AuditEntry("LGPD.Export", nameof(Patient), patient.Id.ToString()), ct);
        return new PersonalDataExport($"neurocare-dados-{clock.ToLocal(clock.UtcNow):yyyyMMdd-HHmm}.json", bytes, "application/json");
    }

    private void RequirePatient()
    {
        guard.RequireOrganization();
        if (!guard.IsPatient || user.UserId is null) throw new ForbiddenException();
    }

    private ConsentDto ToDto(ConsentRecord x) => new(x.Id, x.PurposeKey, x.PurposeText, x.Version, x.Status,
        clock.ToLocal(x.GrantedAtUtc), x.RevokedAtUtc.HasValue ? clock.ToLocal(x.RevokedAtUtc.Value) : null);
}
