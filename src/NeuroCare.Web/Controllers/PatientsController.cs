using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Web.Helpers;

namespace NeuroCare.Web.Controllers;

[Authorize(Policy = "ClinicalStaff")]
public class PatientsController(IPatientService patients, ILookupService lookups, IUserAdminService userAdmin) : Controller
{
    public async Task<IActionResult> Index(string? term, bool includeInactive = false, CancellationToken ct = default)
    {
        ViewData["Term"] = term;
        ViewData["IncludeInactive"] = includeInactive;
        return View(await patients.ListAsync(term, includeInactive, ct));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct) =>
        View(await patients.GetAsync(id, ct));

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadDoctorsAsync(null, ct);
        return View(new CreatePatientDto { BirthDate = DateTime.Today.AddYears(-50) });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePatientDto dto, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var id = await patients.CreateAsync(dto, ct);
                TempData["Success"] = "Paciente cadastrado.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        await LoadDoctorsAsync(dto.ResponsibleDoctorId, ct);
        return View(dto);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var dto = await patients.GetForEditAsync(id, ct);
        await LoadDoctorsAsync(dto.ResponsibleDoctorId, ct);
        ViewData["PatientId"] = id;
        return View(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, UpdatePatientDto dto, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await patients.UpdateAsync(id, dto, ct);
                TempData["Success"] = "Dados atualizados.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        await LoadDoctorsAsync(dto.ResponsibleDoctorId, ct);
        ViewData["PatientId"] = id;
        return View(dto);
    }

    [HttpPost]
    public async Task<IActionResult> SetStatus(Guid id, PatientStatus status, CancellationToken ct)
    {
        await patients.SetStatusAsync(id, status, ct);
        TempData["Success"] = status == PatientStatus.Active ? "Paciente reativado." : "Paciente desativado.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> InvitePortal(Guid id, CancellationToken ct)
    {
        try
        {
            var sent = await userAdmin.InvitePatientAsync(id, ct);
            if (sent) TempData["Success"] = "Convite enviado por e-mail.";
            else TempData["Error"] = "Acesso criado, mas o e-mail não pôde ser enviado. Verifique a configuração de e-mail.";
        }
        catch (RequestValidationException ex)
        {
            TempData["Error"] = ex.Errors.SelectMany(e => e.Value).FirstOrDefault() ?? "Operação inválida.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task LoadDoctorsAsync(Guid? selected, CancellationToken ct) =>
        ViewData["Doctors"] = new SelectList(await lookups.DoctorsAsync(ct), "Id", "Name", selected);
}
