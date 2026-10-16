using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface ISymptomService
{
    Task<IReadOnlyList<SymptomListItemDto>> ListAsync(Guid? patientId, int days, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid? patientId, SaveSymptomDto dto, CancellationToken ct = default);
}

/// <summary>Diário de sintomas: registrado pelo médico ou pelo próprio paciente. Não há exclusão (registro clínico).</summary>
public class SymptomService(
    ClinicalContext context, ISymptomRepository symptoms, IUnitOfWork uow, AccessGuard guard,
    ICurrentUser user, IAuditService audit, IClock clock) : ISymptomService
{
    public async Task<IReadOnlyList<SymptomListItemDto>> ListAsync(Guid? patientId, int days, CancellationToken ct = default)
    {
        var patient = await context.ResolveAsync(patientId, ct);
        var since = clock.UtcNow.AddDays(-Math.Clamp(days, 1, 3650));
        var list = await symptoms.ListByPatientAsync(patient.Id, since, ct);
        await audit.RecordAsync(new AuditEntry("Symptom.List", nameof(Patient), patient.Id.ToString()), ct);
        return list.OrderByDescending(s => s.OccurredAtUtc)
            .Select(s => new SymptomListItemDto(s.Id, clock.ToLocal(s.OccurredAtUtc), s.Type, s.Intensity, s.Notes, s.RecordedByPatient))
            .ToList();
    }

    public async Task<Guid> CreateAsync(Guid? patientId, SaveSymptomDto dto, CancellationToken ct = default)
    {
        var org = guard.RequireOrganization();
        DtoValidator.EnsureValid(dto);
        var patient = await context.ResolveAsync(patientId, ct);
        var occurredUtc = context.ParseOccurredAt(dto.OccurredAtLocal, nameof(dto.OccurredAtLocal));

        var record = new SymptomRecord
        {
            OrganizationId = org, PatientId = patient.Id, OccurredAtUtc = occurredUtc, Type = dto.Type,
            Intensity = dto.Intensity, Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            RecordedByUserId = user.UserId, RecordedByPatient = guard.IsPatient
        };
        await symptoms.AddAsync(record, ct);
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("Symptom.Create", nameof(SymptomRecord), record.Id.ToString()), ct);
        return record.Id;
    }
}
