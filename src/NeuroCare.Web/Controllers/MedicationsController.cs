using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Web.Helpers;

namespace NeuroCare.Web.Controllers;

[Authorize(Roles = Roles.Doctor + "," + Roles.Patient)]
public class MedicationsController(IMedicationService medications, IClinicalContext context, IClock clock) : Controller
{
    public async Task<IActionResult> Index(Guid? patientId, CancellationToken ct)
    {
        var items = await medications.ListAsync(patientId, ct);
        await SetHeaderAsync(patientId, ct);
        return View(items);
    } 

    [HttpGet, Authorize(Roles = Roles.Doctor)]
    public async Task<IActionResult> Create(Guid patientId, CancellationToken ct)
    {
        await SetHeaderAsync(patientId, ct);
        ViewData["PatientId"] = patientId;
        return View(new SaveMedicationDto { StartDate = clock.ToLocal(clock.UtcNow).Date });
    }

    [HttpPost, Authorize(Roles = Roles.Doctor)]
    public async Task<IActionResult> Create(Guid patientId, SaveMedicationDto dto, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await medications.CreateAsync(patientId, dto, ct);
                TempData["Success"] = "Medicamento registrado.";
                return RedirectToAction(nameof(Index), new { patientId });
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        await SetHeaderAsync(patientId, ct);
        ViewData["PatientId"] = patientId;
        return View(dto);
    }

    [HttpGet, Authorize(Roles = Roles.Doctor)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var model = await medications.GetForEditAsync(id, ct);
        if (model.Finished)
        {
            TempData["Error"] = "Medicamento finalizado não pode ser editado.";
            return RedirectToAction(nameof(Index), new { patientId = model.PatientId });
        }
        await SetHeaderAsync(model.PatientId, ct);
        ViewData["MedicationId"] = id;
        ViewData["PatientId"] = model.PatientId;
        return View(model.Form);
    }

    [HttpPost, Authorize(Roles = Roles.Doctor)]
    public async Task<IActionResult> Edit(Guid id, SaveMedicationDto dto, CancellationToken ct)
    {
        var model = await medications.GetForEditAsync(id, ct); // obtém o paciente
        if (ModelState.IsValid)
        {
            try
            {
                await medications.UpdateAsync(id, dto, ct);
                TempData["Success"] = "Medicamento atualizado.";
                return RedirectToAction(nameof(Index), new { patientId = model.PatientId });
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        await SetHeaderAsync(model.PatientId, ct);
        ViewData["MedicationId"] = id;
        ViewData["PatientId"] = model.PatientId;
        return View(dto);
    }

    [HttpPost, Authorize(Roles = Roles.Doctor)]
    public async Task<IActionResult> Transition(Guid id, Guid patientId, MedicationAction action, CancellationToken ct)
    {
        try
        {
            await medications.TransitionAsync(id, action, ct);
            TempData["Success"] = "Medicamento atualizado.";
        }
        catch (RequestValidationException ex)
        {
            TempData["Error"] = ex.Errors.SelectMany(e => e.Value).FirstOrDefault() ?? "Operação inválida.";
        }
        return RedirectToAction(nameof(Index), new { patientId });
    }

    private async Task SetHeaderAsync(Guid? patientId, CancellationToken ct)
    {
        ViewData["Header"] = await context.GetHeaderAsync(patientId, ct);
        ViewData["Tab"] = "medications";
    }
}
