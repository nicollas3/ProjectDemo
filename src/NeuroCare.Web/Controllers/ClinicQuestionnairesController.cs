using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Web.Helpers;

namespace NeuroCare.Web.Controllers;

[Authorize(Roles = Roles.Doctor + "," + Roles.ClinicAdmin)]
public class ClinicQuestionnairesController(IClinicQuestionnaireService service) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await service.ListAsync(ct));

    [HttpGet]
    public IActionResult Create() => View("Edit", new ClinicQuestionnaireEditDto(null, NewForm()));

    [HttpPost]
    public async Task<IActionResult> Create(SaveClinicQuestionnaireDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await service.CreateAsync(form, ct);
                TempData["Success"] = "Questionário criado.";
                return RedirectToAction(nameof(Index));
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        EnsureRows(form);
        return View("Edit", new ClinicQuestionnaireEditDto(null, form));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct) => View(await service.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, SaveClinicQuestionnaireDto form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await service.UpdateAsync(id, form, ct);
                TempData["Success"] = "Questionário atualizado.";
                return RedirectToAction(nameof(Index));
            }
            catch (RequestValidationException ex) { ModelState.AddErrors(ex); }
        }
        EnsureRows(form);
        return View(new ClinicQuestionnaireEditDto(id, form));
    }

    private static SaveClinicQuestionnaireDto NewForm() => new()
    {
        Items = [new ClinicQuestionItemInputDto()],
        Bands = [new ScoreBandInputDto { Min = 0, Max = 1, Label = "Pontuação registrada" }]
    };

    private static void EnsureRows(SaveClinicQuestionnaireDto form)
    {
        if (form.Items.Count == 0) form.Items.Add(new ClinicQuestionItemInputDto());
        if (form.Bands.Count == 0) form.Bands.Add(new ScoreBandInputDto());
    }
}
