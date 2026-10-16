using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface ISeizureService
{
    Task<IReadOnlyList<SeizureListItemDto>> ListAsync(Guid? patientId, int days, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid? patientId, SaveSeizureDto dto, CancellationToken ct = default);
}

/// <summary>Diário de crises. O sistema apenas registra e exibe: não interpreta nem sugere diagnóstico.</summary>
public class SeizureService(
    ClinicalContext context, ISeizureRepository seizures, IUnitOfWork uow, AccessGuard guard,
    ICurrentUser user, IAuditService audit, IClock clock) : ISeizureService
{
    public async Task<IReadOnlyList<SeizureListItemDto>> ListAsync(Guid? patientId, int days, CancellationToken ct = default)
    {
        var patient = await context.ResolveAsync(patientId, ct);
        var since = clock.UtcNow.AddDays(-Math.Clamp(days, 1, 3650));
        var list = await seizures.ListByPatientAsync(patient.Id, since, ct);
        await audit.RecordAsync(new AuditEntry("Seizure.List", nameof(Patient), patient.Id.ToString()), ct);
        return list.OrderByDescending(s => s.OccurredAtUtc)
            .Select(s => new SeizureListItemDto(s.Id, clock.ToLocal(s.OccurredAtUtc), s.DurationSeconds, s.Type,
                s.LossOfConsciousness, s.Trigger, s.Notes, s.RecordedByPatient))
            .ToList();
    }

    public async Task<Guid> CreateAsync(Guid? patientId, SaveSeizureDto dto, CancellationToken ct = default)
    {
        var org = guard.RequireOrganization();
        DtoValidator.EnsureValid(dto);
        var patient = await context.ResolveAsync(patientId, ct);
        var occurredUtc = context.ParseOccurredAt(dto.OccurredAtLocal, nameof(dto.OccurredAtLocal));

        var seizure = new SeizureEvent
        {
            OrganizationId = org, PatientId = patient.Id, OccurredAtUtc = occurredUtc, Type = dto.Type,
            DurationSeconds = dto.DurationSeconds, LossOfConsciousness = dto.LossOfConsciousness,
            Trigger = string.IsNullOrWhiteSpace(dto.Trigger) ? null : dto.Trigger.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            RecordedByUserId = user.UserId, RecordedByPatient = guard.IsPatient
        };
        await seizures.AddAsync(seizure, ct);
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("Seizure.Create", nameof(SeizureEvent), seizure.Id.ToString()), ct);
        return seizure.Id;
    }
}
