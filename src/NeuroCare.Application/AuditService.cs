using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IAuditService
{
    Task RecordAsync(AuditEntry entry, CancellationToken ct = default);
}

/// <summary>Registra metadados de auditoria. Nunca grave dados clínicos em Details.</summary>
public class AuditService(IAuditLogRepository repository, ICurrentUser user, IClock clock, IUnitOfWork uow) : IAuditService
{
    public async Task RecordAsync(AuditEntry entry, CancellationToken ct = default)
    {
        var details = entry.Details is { Length: > 500 } ? entry.Details[..500] : entry.Details;
        await repository.AddAsync(new AuditLog
        {
            OccurredAt = clock.UtcNow,
            UserId = entry.UserId ?? user.UserId,
            OrganizationId = entry.OrganizationId ?? user.OrganizationId,
            Action = entry.Action,
            EntityName = entry.EntityName,
            EntityId = entry.EntityId,
            IpAddress = user.IpAddress,
            Details = details
        }, ct);
        await uow.SaveChangesAsync(ct);
    }
}
