using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using NeuroCare.Application;
using NeuroCare.Infrastructure;
using NeuroCare.Web.ViewModels;

namespace NeuroCare.Web.Controllers;

public class AccountController(
    UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    IAuditService audit, IOrganizationRepository organizations, IEmailSender emailSender,
    ILinkBuilder links, ILogger<AccountController> logger) : Controller
{
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost, AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken ct = default)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var user = await users.FindByEmailAsync(model.Email);
        var orgInactive = user?.OrganizationId is Guid orgId && !await organizations.IsActiveAsync(orgId, ct);
        if (user is null || !user.Active || orgInactive)
        {
            logger.LogWarning("Falha de login (usuário inexistente, inativo ou organização inativa)");
            ModelState.AddModelError("", "E-mail ou senha inválidos.");
            return View(model);
        }

        var result = await signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            await audit.RecordAsync(new AuditEntry("Auth.Login", "User", user.Id.ToString(), null, user.Id, user.OrganizationId), ct);
            return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction("Index", "Home");
        }

        await audit.RecordAsync(new AuditEntry("Auth.LoginFailed", "User", user.Id.ToString(),
            result.IsLockedOut ? "LockedOut" : result.IsNotAllowed ? "NotAllowed" : "InvalidPassword", user.Id, user.OrganizationId), ct);

        ModelState.AddModelError("", result.IsLockedOut
            ? "Conta temporariamente bloqueada por tentativas inválidas. Tente novamente mais tarde."
            : result.IsNotAllowed ? "Conta ainda não ativada. Use o link do convite ou \"Esqueci minha senha\"." : "E-mail ou senha inválidos.");
        return View(model);
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await audit.RecordAsync(new AuditEntry("Auth.Logout", "User"), ct);
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // ---------- Esqueci minha senha ----------
    [HttpGet, AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost, AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await users.FindByEmailAsync(model.Email);
        if (user is not null && user.Active && user.Email is not null)
        {
            try
            {
                var token = await users.GeneratePasswordResetTokenAsync(user);
                var (subject, body) = EmailTemplates.PasswordReset(user.FullName, links.ResetPassword(user.Id, token));
                await emailSender.SendAsync(user.Email, subject, body, ct);
                await audit.RecordAsync(new AuditEntry("Auth.PasswordResetRequested", "User", user.Id.ToString(), null, user.Id, user.OrganizationId), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha ao enviar e-mail de redefinição de senha");
            }
        }
        // Resposta idêntica exista o e-mail ou não (evita enumeração de usuários).
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult ForgotPasswordConfirmation() => View();

    // ---------- Redefinir senha (também usado pelo convite) ----------
    [HttpGet, AllowAnonymous]
    public IActionResult ResetPassword(Guid userId, string? token) =>
        string.IsNullOrWhiteSpace(token) || userId == Guid.Empty
            ? RedirectToAction(nameof(Login))
            : View(new ResetPasswordViewModel { UserId = userId, Token = token });

    [HttpPost, AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        string decoded;
        try { decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token)); }
        catch (FormatException) { return InvalidLink(model); }

        var user = await users.FindByIdAsync(model.UserId.ToString());
        if (user is null || !user.Active) return InvalidLink(model);

        var result = await users.ResetPasswordAsync(user, decoded, model.NewPassword);
        if (!result.Succeeded)
        {
            var policy = result.Errors.Where(e => e.Code != "InvalidToken").ToList();
            if (policy.Count == 0) return InvalidLink(model);
            foreach (var e in policy) ModelState.AddModelError("", e.Description);
            return View(model);
        }

        // O token chegou ao e-mail do usuário: isso comprova a posse do e-mail.
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await users.UpdateAsync(user);
        }

        await audit.RecordAsync(new AuditEntry("Auth.PasswordReset", "User", user.Id.ToString(), null, user.Id, user.OrganizationId), ct);
        return RedirectToAction(nameof(ResetPasswordConfirmation));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult ResetPasswordConfirmation() => View();

    private IActionResult InvalidLink(ResetPasswordViewModel model)
    {
        ModelState.AddModelError("", "Link inválido ou expirado. Solicite um novo em \"Esqueci minha senha\".");
        return View(model);
    }

    // ---------- Alterar senha ----------
    [HttpGet, Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost, Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();

        var result = await users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(model);
        }

        await signIn.RefreshSignInAsync(user);
        await audit.RecordAsync(new AuditEntry("Auth.PasswordChanged", "User", user.Id.ToString()), ct);
        TempData["Success"] = "Senha alterada com sucesso.";
        return RedirectToAction("Index", "Home");
    }
}
