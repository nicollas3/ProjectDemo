using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;
using NeuroCare.Domain;
using NeuroCare.Web.ViewModels;

namespace NeuroCare.Web.Controllers;

[Authorize]
public class HomeController(IDashboardService dashboard, IClock clock) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (User.IsInRole(Roles.Patient)) return RedirectToAction("Index", "PatientPortal");
        if (!User.IsInRole(Roles.Doctor) && !User.IsInRole(Roles.ClinicAdmin)) return View("AdminHome");

        var name = User.FindFirstValue(AppClaims.FullName) ?? "";
        var hour = clock.ToLocal(clock.UtcNow).Hour;
        var period = hour < 12 ? "Bom dia" : hour < 18 ? "Boa tarde" : "Boa noite";
        var title = User.IsInRole(Roles.Doctor) ? "Dr(a). " : "";

        var data = await dashboard.GetAsync(ct);
        return View(new DashboardViewModel($"{period}, {title}{name}", data));
    }

    [AllowAnonymous]
    public new IActionResult NotFound() => View("NotFound");

    [AllowAnonymous]
    public IActionResult Error() => View();
}
