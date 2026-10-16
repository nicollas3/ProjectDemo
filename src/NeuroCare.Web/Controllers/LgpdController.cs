using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Web.Helpers;

namespace NeuroCare.Web.Controllers;

[Authorize(Roles = Roles.Patient)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class LgpdController(ILgpdService service) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await service.ListConsentsAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Grant(GrantConsentDto dto, CancellationToken ct)
    {
        try
        {
            await service.GrantConsentAsync(dto, ct);
            TempData["Success"] = "Consentimento registrado.";
        }
        catch (RequestValidationException ex)
        {
            TempData["Error"] = ex.Errors.SelectMany(x => x.Value).FirstOrDefault() ?? "Não foi possível registrar o consentimento.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await service.RevokeConsentAsync(id, ct);
        TempData["Success"] = "Consentimento revogado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var file = await service.ExportMyDataAsync(ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        return File(file.Content, file.ContentType, file.FileName);
    }
}
