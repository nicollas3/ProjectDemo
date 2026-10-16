using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NeuroCare.Application;

namespace NeuroCare.Infrastructure;

public class IdentityGateway(UserManager<ApplicationUser> users) : IIdentityGateway
{
    public async Task<Guid> CreateUserAsync(NewIdentityUser u, CancellationToken ct = default)
    {
        // Mensagem genérica: não confirma se o e-mail existe em outra organização.
        if (await users.FindByEmailAsync(u.Email) is not null)
            throw new RequestValidationException("Email", "E-mail indisponível para cadastro.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = u.Email, Email = u.Email, EmailConfirmed = false,
            FullName = u.FullName, OrganizationId = u.OrganizationId, Active = true, CreatedAt = DateTime.UtcNow
        };

        var created = await users.CreateAsync(user); // sem senha: o usuário define a sua pelo link do convite
        if (!created.Succeeded)
            throw new RequestValidationException("Email", "Não foi possível criar o usuário com estes dados.");

        var role = await users.AddToRoleAsync(user, u.Role);
        if (!role.Succeeded)
        {
            await users.DeleteAsync(user);
            throw new InvalidOperationException("Falha ao atribuir o perfil ao usuário.");
        }
        return user.Id;
    }

    public async Task<string> GenerateResetTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("Usuário não encontrado.");
        return await users.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<IReadOnlyList<UserSummaryDto>> ListUsersAsync(Guid organizationId, CancellationToken ct = default)
    {
        var list = await users.Users.AsNoTracking()
            .Where(u => u.OrganizationId == organizationId)
            .OrderBy(u => u.FullName).Take(500).ToListAsync(ct);

        var result = new List<UserSummaryDto>(list.Count);
        foreach (var u in list) result.Add(await ToDtoAsync(u));
        return result;
    }

    public async Task<UserSummaryDto?> FindAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is null ? null : await ToDtoAsync(user);
    }

    public async Task SetActiveAsync(Guid userId, bool active, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("Usuário não encontrado.");
        user.Active = active;
        await users.UpdateAsync(user);
        if (!active) await users.UpdateSecurityStampAsync(user); // invalida sessões abertas
    }

    public async Task RevokeSessionsAsync(Guid organizationId, CancellationToken ct = default)
    {
        var list = await users.Users.Where(u => u.OrganizationId == organizationId).ToListAsync(ct);
        foreach (var u in list) await users.UpdateSecurityStampAsync(u);
    }

    public async Task<string?> GetEmailAsync(Guid userId, CancellationToken ct = default)
    {
        var u = await users.FindByIdAsync(userId.ToString());
        return u is { Active: true, EmailConfirmed: true } ? u.Email : null;
    }

    private async Task<UserSummaryDto> ToDtoAsync(ApplicationUser u)
    {
        var roles = await users.GetRolesAsync(u);
        return new UserSummaryDto(u.Id, u.FullName, u.Email ?? "", roles.FirstOrDefault() ?? "", u.Active, u.EmailConfirmed, u.OrganizationId);
    }
}
