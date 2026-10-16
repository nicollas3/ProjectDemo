using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Web.Helpers;
using NeuroCare.Web.ViewModels;

namespace NeuroCare.Web.Controllers;

/// <summary>Médico (com patientId) ou paciente (sempre o próprio). Instrumentos "ClinicianOnly" só o médico aplica.</summary>
[Authorize(Roles = Roles.Doctor + "," + Roles.Patient)]
public class QuestionnairesController(IQuestionnaireService questionnaires, IClinicalContext context) : Controller
{
    public async Task<IActionResult> Index(Guid? patientId, CancellationToken ct)
    {
        var available = await questionnaires.ListAvailableAsync(ct);
        var recent = await questionnaires.ListResponsesAsync(patientId, null, 365, ct);
        await SetHeaderAsync(patientId, ct);
        ViewData["PatientId"] = patientId;
        return View(new QuestionnairesIndexViewModel(available, recent.Take(10).ToList()));
    }

    [HttpGet]
    public async Task<IActionResult> Fill(string key, Guid? patientId, CancellationToken ct)
    {
        var form = await questionnaires.GetFormAsync(key, ct);
        await SetHeaderAsync(patientId, ct);
        ViewData["PatientId"] = patientId;
        return View(form);
    }

    [HttpPost]
    public async Task<IActionResult> Fill(string key, Guid? patientId, QuestionnaireAnswersInput input, CancellationToken ct)
    {
        try
        {
            var result = await questionnaires.SubmitAsync(patientId, key, input.Answers, ct);
            return RedirectToAction(nameof(Result), new { id = result.Id });
        }
        catch (RequestValidationException ex)
        {
            ModelState.AddErrors(ex);
        }

        var form = await questionnaires.GetFormAsync(key, ct);
        await SetHeaderAsync(patientId, ct);
        ViewData["PatientId"] = patientId;
        ViewData["Selected"] = input.Answers;
        return View(form);
    }

    public async Task<IActionResult> Result(Guid id, CancellationToken ct)
    {
        var result = await questionnaires.GetResultAsync(id, ct);
        await SetHeaderAsync(result.PatientId, ct);
        return View(result);
    }

    public async Task<IActionResult> History(string key, Guid? patientId, int days = 365, CancellationToken ct = default)
    {
        var items = await questionnaires.ListResponsesAsync(patientId, key, days, ct);
        await SetHeaderAsync(patientId, ct);
        ViewData["PatientId"] = patientId;
        ViewData["Key"] = key;
        return View(items);
    }

    private async Task SetHeaderAsync(Guid? patientId, CancellationToken ct)
    {
        ViewData["Header"] = await context.GetHeaderAsync(patientId, ct);
        ViewData["Tab"] = "questionnaires";
    }
}
