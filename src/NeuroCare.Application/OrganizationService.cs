using Microsoft.Extensions.Logging;
using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IOrganizationService
{
    Task<IReadOnlyList<OrganizationSummaryDto>> ListAsync(CancellationToken ct = default);
    Task<CreatedOrganizationResult> CreateAsync(CreateOrganizationDto dto, CancellationToken ct = default);
    Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default);
}

/// <summary>Gestão de organizações: exclusiva do Administrator da plataforma (sem acesso a dados clínicos).</summary>
public class OrganizationService(
    IOrganizationRepository organizations, IIdentityGateway identity, IUnitOfWork uow,
    AccessGuard guard, IAuditService audit, IInviteService invites, ILogger<OrganizationService> logger) : IOrganizationService
{
    public async Task<IReadOnlyList<OrganizationSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        guard.RequireAdministrator();
        return (await organizations.ListAsync(ct))
            .Select(o => new OrganizationSummaryDto(o.Id, o.Name, o.LegalName, o.DocumentNumber, o.Active, o.CreatedAt))
            .ToList();
    }

    public async Task<CreatedOrganizationResult> CreateAsync(CreateOrganizationDto dto, CancellationToken ct = default)
    {
        guard.RequireAdministrator();
        DtoValidator.EnsureValid(dto);

        var org = new Organization
        {
            Name = dto.Name.Trim(),
            LegalName = string.IsNullOrWhiteSpace(dto.LegalName) ? null : dto.LegalName.Trim(),
            DocumentNumber = string.IsNullOrWhiteSpace(dto.DocumentNumber) ? null : dto.DocumentNumber.Trim(),
            Active = true
        };

        // Usuário primeiro: a falha mais provável (e-mail duplicado) não deixa organização órfã.
        var adminEmail = dto.AdminEmail.Trim();
        var adminName = dto.AdminFullName.Trim();
        var adminId = await identity.CreateUserAsync(new NewIdentityUser(adminEmail, adminName, org.Id, Roles.ClinicAdmin), ct);

        await organizations.AddAsync(org, ct);
        await uow.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry("Organization.Create", nameof(Organization), org.Id.ToString(), null, null, org.Id), ct);
        logger.LogInformation("Organização {OrganizationId} criada", org.Id);
        var sent = await invites.TrySendAsync(adminId, adminEmail, adminName, ct);
        return new CreatedOrganizationResult(org.Id, sent);
    }

    public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        guard.RequireAdministrator();
        var org = await organizations.GetByIdAsync(id, ct) ?? throw new NotFoundException("Organização não encontrada.");
        org.Active = active;
        await uow.SaveChangesAsync(ct);
        if (!active) await identity.RevokeSessionsAsync(id, ct); // encerra sessões abertas dos usuários da organização

        await audit.RecordAsync(new AuditEntry(active ? "Organization.Activate" : "Organization.Deactivate",
            nameof(Organization), id.ToString(), null, null, id), ct);
    }
}
