using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Web.Helpers;

namespace NeuroCare.Web.Controllers;

[Authorize(Roles = Roles.Doctor)]
public class ClinicalNotesController(IClinicalNoteService notes, IClinicalContext context, IClock clock) : Controller
{
    public async Task<IActionResult> Index(Guid patientId, CancellationToken ct)
    {
        await SetHeaderAsync(patientId, ct);
        return View(await notes.ListAsync(patientId, ct));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var details = await notes.GetAsync(id, ct);
        await SetHeaderAsync(details.PatientId, ct);
        return View(details);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid patientId, CancellationToken ct)
    {
        await SetHeaderAsync(patientId, ct);
        ViewData["PatientId"] = patientId;
        return View(new SaveClinicalNoteDto { OccurredAtLocal = clock.ToLocal(clock.UtcNow) });
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid patientId, SaveClinicalNoteDto dto, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var id = await notes.CreateAsync(patientId, dto, ct);
                TempData["Success"] = "Rascunho salvo. Revise e assine quando estiver pronto.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        await SetHeaderAsync(patientId, ct);
        ViewData["PatientId"] = patientId;
        return View(dto);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var model = await notes.GetForEditAsync(id, ct);
        if (model.Locked)
        {
            TempData["Error"] = "Evolução assinada não pode ser alterada. Registre um adendo.";
            return RedirectToAction(nameof(Details), new { id });
        }
        await SetHeaderAsync(model.PatientId, ct);
        ViewData["NoteId"] = id;
        return View(model.Form);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, SaveClinicalNoteDto dto, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await notes.UpdateAsync(id, dto, ct);
                TempData["Success"] = "Rascunho atualizado.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        var model = await notes.GetForEditAsync(id, ct); // apenas para obter o paciente
        await SetHeaderAsync(model.PatientId, ct);
        ViewData["NoteId"] = id;
        return View(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Sign(Guid id, CancellationToken ct)
    {
        try
        {
            await notes.SignAsync(id, ct);
            TempData["Success"] = "Evolução assinada. Ela não poderá mais ser editada.";
        }
        catch (RequestValidationException ex) { TempData["Error"] = FirstError(ex); }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Addendum(Guid id, AddendumDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) TempData["Error"] = "Escreva o texto do adendo (máx. 2000 caracteres).";
        else
        {
            try
            {
                await notes.AddAddendumAsync(id, dto, ct);
                TempData["Success"] = "Adendo registrado.";
            }
            catch (RequestValidationException ex) { TempData["Error"] = FirstError(ex); }
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task SetHeaderAsync(Guid patientId, CancellationToken ct)
    {
        ViewData["Header"] = await context.GetHeaderAsync(patientId, ct);
        ViewData["Tab"] = "notes";
    }

    private static string FirstError(RequestValidationException ex) =>
        ex.Errors.SelectMany(e => e.Value).FirstOrDefault() ?? "Operação inválida.";
}
