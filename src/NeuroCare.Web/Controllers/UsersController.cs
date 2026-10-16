using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Web.Helpers;

namespace NeuroCare.Web.Controllers;

[Authorize(Roles = Roles.ClinicAdmin)]
public class UsersController(IUserAdminService users) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await users.ListAsync(ct));

    [HttpGet]
    public IActionResult Create() => View(new CreateStaffUserDto());

    [HttpPost]
    public async Task<IActionResult> Create(CreateStaffUserDto dto, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var result = await users.CreateStaffAsync(dto, ct);
                if (result.InviteSent) TempData["Success"] = "Usuário criado. Convite enviado por e-mail.";
                else TempData["Error"] = "Usuário criado, mas o e-mail não pôde ser enviado. Use \"Reenviar convite\" após corrigir a configuração.";
                return RedirectToAction(nameof(Index));
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        return View(dto);
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(Guid id, bool active, CancellationToken ct)
    {
        try
        {
            await users.SetActiveAsync(id, active, ct);
            TempData["Success"] = active ? "Usuário reativado." : "Usuário desativado.";
        }
        catch (RequestValidationException ex)
        {
            TempData["Error"] = ex.Errors.SelectMany(e => e.Value).FirstOrDefault() ?? "Operação inválida.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ResendInvite(Guid id, CancellationToken ct)
    {
        try
        {
            var sent = await users.ResendInviteAsync(id, ct);
            if (sent) TempData["Success"] = "Convite reenviado.";
            else TempData["Error"] = "Não foi possível enviar o e-mail. Verifique a configuração de e-mail.";
        }
        catch (RequestValidationException ex)
        {
            TempData["Error"] = ex.Errors.SelectMany(e => e.Value).FirstOrDefault() ?? "Operação inválida.";
        }
        return RedirectToAction(nameof(Index));
    }
}
