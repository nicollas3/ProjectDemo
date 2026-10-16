using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Web.Helpers;

namespace NeuroCare.Web.Controllers;

/// <summary>Médico (com patientId) ou paciente (sempre o próprio — patientId é ignorado pelo serviço).</summary>
[Authorize(Roles = Roles.Doctor + "," + Roles.Patient)]
public class SymptomsController(ISymptomService symptoms, IClinicalContext context, IClock clock) : Controller
{
    public async Task<IActionResult> Index(Guid? patientId, int days = 90, CancellationToken ct = default)
    {
        var items = await symptoms.ListAsync(patientId, days, ct);
        await SetHeaderAsync(patientId, ct);
        ViewData["Days"] = days;
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? patientId, CancellationToken ct)
    {
        await SetHeaderAsync(patientId, ct);
        ViewData["PatientId"] = patientId;
        return View(new SaveSymptomDto { OccurredAtLocal = clock.ToLocal(clock.UtcNow) });
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid? patientId, SaveSymptomDto dto, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await symptoms.CreateAsync(patientId, dto, ct);
                TempData["Success"] = "Sintoma registrado.";
                return RedirectToAction(nameof(Index), new { patientId });
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        await SetHeaderAsync(patientId, ct);
        ViewData["PatientId"] = patientId;
        return View(dto);
    }

    private async Task SetHeaderAsync(Guid? patientId, CancellationToken ct)
    {
        ViewData["Header"] = await context.GetHeaderAsync(patientId, ct);
        ViewData["Tab"] = "symptoms";
    }
}
