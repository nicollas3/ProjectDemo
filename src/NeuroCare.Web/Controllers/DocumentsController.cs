using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Web.Helpers;

namespace NeuroCare.Web.Controllers;

/// <summary>Médico (com patientId) ou paciente (sempre o próprio). Upload limitado a ~12 MB por requisição (o serviço limita o arquivo).</summary>
[Authorize(Roles = Roles.Doctor + "," + Roles.Patient)]
public class DocumentsController(IDocumentService documents, IClinicalContext context) : Controller
{
    public async Task<IActionResult> Index(Guid? patientId, CancellationToken ct)
    {
        var items = await documents.ListAsync(patientId, ct);
        await SetHeaderAsync(patientId, ct);
        ViewData["PatientId"] = patientId;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Upload(Guid? patientId, CancellationToken ct)
    {
        await SetHeaderAsync(patientId, ct);
        ViewData["PatientId"] = patientId;
        return View(new UploadDocumentDto());
    }

    [HttpPost, RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid? patientId, UploadDocumentDto dto, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            ModelState.AddModelError("File", "Selecione um arquivo.");

        if (ModelState.IsValid)
        {
            try
            {
                await using var stream = file!.OpenReadStream();
                await documents.UploadAsync(patientId, dto, file.FileName, stream, ct);
                TempData["Success"] = "Documento enviado.";
                return RedirectToAction(nameof(Index), new { patientId });
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        await SetHeaderAsync(patientId, ct);
        ViewData["PatientId"] = patientId;
        return View(dto);
    }

    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var d = await documents.OpenAsync(id, ct);
        // Sempre como anexo (nunca inline) e com nosniff global: evita execução de conteúdo no navegador.
        return File(d.Content, d.ContentType, d.FileName);
    }

    [HttpPost, Authorize(Roles = Roles.Doctor)]
    public async Task<IActionResult> Delete(Guid id, Guid patientId, CancellationToken ct)
    {
        await documents.DeleteAsync(id, ct);
        TempData["Success"] = "Documento removido da lista (o arquivo é mantido para fins de retenção).";
        return RedirectToAction(nameof(Index), new { patientId });
    }

    private async Task SetHeaderAsync(Guid? patientId, CancellationToken ct)
    {
        ViewData["Header"] = await context.GetHeaderAsync(patientId, ct);
        ViewData["Tab"] = "documents";
    }
}
