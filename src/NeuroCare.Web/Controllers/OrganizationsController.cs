using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Web.Helpers;

namespace NeuroCare.Web.Controllers;

[Authorize(Roles = Roles.Administrator)]
public class OrganizationsController(IOrganizationService organizations) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await organizations.ListAsync(ct));

    [HttpGet]
    public IActionResult Create() => View(new CreateOrganizationDto());

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrganizationDto dto, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var result = await organizations.CreateAsync(dto, ct);
                if (result.InviteSent) TempData["Success"] = "Organização criada. Convite enviado ao administrador.";
                else TempData["Error"] = "Organização criada, mas o e-mail do convite não pôde ser enviado. Verifique a configuração de e-mail.";
                return RedirectToAction(nameof(Index));
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        return View(dto);
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(Guid id, bool active, CancellationToken ct)
    {
        await organizations.SetActiveAsync(id, active, ct);
        TempData["Success"] = active ? "Organização reativada." : "Organização desativada; sessões dos usuários serão encerradas.";
        return RedirectToAction(nameof(Index));
    }
}
