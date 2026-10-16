using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Web.Helpers;
using NeuroCare.Web.ViewModels;

namespace NeuroCare.Web.Controllers;

[Authorize(Policy = "ClinicalStaff")]
public class AppointmentsController(IAppointmentService appointments, ILookupService lookups, IClock clock) : Controller
{
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var today = clock.ToLocal(clock.UtcNow).Date;
        var f = (from ?? today).Date;
        var t = (to ?? today.AddDays(7)).Date;
        var items = await appointments.ListAsync(f, t.AddDays(1), null, ct);
        return View(new AppointmentsIndexViewModel(items, f, t));
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? patientId, CancellationToken ct)
    {
        var start = clock.ToLocal(clock.UtcNow).Date.AddDays(1).AddHours(9);
        var dto = new SaveAppointmentDto { StartsAtLocal = start, PatientId = patientId ?? Guid.Empty };
        await LoadListsAsync(dto, ct);
        return View(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveAppointmentDto dto, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await appointments.CreateAsync(dto, ct);
                TempData["Success"] = "Consulta agendada.";
                return RedirectToAction(nameof(Index), new { from = dto.StartsAtLocal.Date, to = dto.StartsAtLocal.Date.AddDays(6) });
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        await LoadListsAsync(dto, ct);
        return View(dto);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var dto = await appointments.GetForEditAsync(id, ct);
        await LoadListsAsync(dto, ct);
        ViewData["AppointmentId"] = id;
        return View(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, SaveAppointmentDto dto, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await appointments.UpdateAsync(id, dto, ct);
                TempData["Success"] = "Consulta atualizada.";
                return RedirectToAction(nameof(Index));
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        await LoadListsAsync(dto, ct);
        ViewData["AppointmentId"] = id;
        return View(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Transition(Guid id, AppointmentAction action, CancellationToken ct)
    {
        try
        {
            await appointments.TransitionAsync(id, action, ct);
            TempData["Success"] = "Consulta atualizada.";
        }
        catch (RequestValidationException ex)
        {
            TempData["Error"] = ex.Errors.SelectMany(e => e.Value).FirstOrDefault() ?? "Operação inválida.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadListsAsync(SaveAppointmentDto dto, CancellationToken ct)
    {
        ViewData["Patients"] = new SelectList(await lookups.ActivePatientsAsync(ct), "Id", "Name", dto.PatientId);
        ViewData["Doctors"] = new SelectList(await lookups.DoctorsAsync(ct), "Id", "Name", dto.DoctorId);
    }
}
