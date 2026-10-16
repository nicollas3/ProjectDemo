using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NeuroCare.Application;

namespace NeuroCare.Infrastructure;

public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Null para administradores da plataforma (sem acesso a dados clínicos).</summary>
    public Guid? OrganizationId { get; set; }
    public string FullName { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class AppClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.OrganizationId.HasValue)
            identity.AddClaim(new Claim(AppClaims.OrganizationId, user.OrganizationId.Value.ToString()));
        identity.AddClaim(new Claim(AppClaims.FullName, user.FullName));
        return identity;
    }
}
