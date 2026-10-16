using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Web.Controllers;

[Authorize(Roles = Roles.Doctor + "," + Roles.Patient)]
public class TimelineController(ITimelineService timeline, IClinicalContext context) : Controller
{
    public async Task<IActionResult> Index(Guid? patientId, CancellationToken ct)
    {
        var items = await timeline.GetAsync(patientId, ct);
        ViewData["Header"] = await context.GetHeaderAsync(patientId, ct);
        ViewData["Tab"] = "timeline";
        return View(items);
    }
}
