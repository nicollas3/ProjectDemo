using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IFallService
{
    Task<IReadOnlyList<FallListItemDto>> ListAsync(Guid? patientId, int days, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid? patientId, SaveFallDto dto, CancellationToken ct = default);
}

/// <summary>Diário de quedas (médico ou próprio paciente). Apenas registra; não avalia risco.</summary>
public class FallService(
    ClinicalContext context, IFallRepository falls, IUnitOfWork uow, AccessGuard guard,
    ICurrentUser user, IAuditService audit, IClock clock) : IFallService
{
    public async Task<IReadOnlyList<FallListItemDto>> ListAsync(Guid? patientId, int days, CancellationToken ct = default)
    {
        var patient = await context.ResolveAsync(patientId, ct);
        var since = clock.UtcNow.AddDays(-Math.Clamp(days, 1, 3650));
        var list = await falls.ListByPatientAsync(patient.Id, since, ct);
        await audit.RecordAsync(new AuditEntry("Fall.List", nameof(Patient), patient.Id.ToString()), ct);
        return list.OrderByDescending(f => f.OccurredAtUtc)
            .Select(f => new FallListItemDto(f.Id, clock.ToLocal(f.OccurredAtUtc), f.Circumstance, f.Location,
                f.Injury, f.NeededMedicalCare, f.Notes, f.RecordedByPatient))
            .ToList();
    }

    public async Task<Guid> CreateAsync(Guid? patientId, SaveFallDto dto, CancellationToken ct = default)
    {
        var org = guard.RequireOrganization();
        DtoValidator.EnsureValid(dto);
        var patient = await context.ResolveAsync(patientId, ct);
        var occurredUtc = context.ParseOccurredAt(dto.OccurredAtLocal, nameof(dto.OccurredAtLocal));

        var fall = new FallEvent
        {
            OrganizationId = org, PatientId = patient.Id, OccurredAtUtc = occurredUtc, Circumstance = dto.Circumstance,
            Location = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim(),
            Injury = dto.Injury, NeededMedicalCare = dto.NeededMedicalCare,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            RecordedByUserId = user.UserId, RecordedByPatient = guard.IsPatient
        };
        await falls.AddAsync(fall, ct);
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("Fall.Create", nameof(FallEvent), fall.Id.ToString()), ct);
        return fall.Id;
    }
}
