using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Web.Controllers;

[Authorize(Roles = Roles.Doctor)]
public class ReportsController(IReportService reports) : Controller
{
    public async Task<IActionResult> Index(Guid patientId, int days = 90, CancellationToken ct = default)
    {
        var report = await reports.GetPatientSummaryAsync(patientId, days, ct);
        ViewData["PatientId"] = patientId;
        return View(report);
    }
}
