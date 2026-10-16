using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Web.Controllers;

/// <summary>Portal do paciente (Etapa 1: consultas próprias). O paciente só vê seus dados.</summary>
[Authorize(Roles = Roles.Patient)]
public class PatientPortalController(IAppointmentService appointments, IClock clock) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var today = clock.ToLocal(clock.UtcNow).Date;
        return View(await appointments.ListAsync(today, null, null, ct));
    }
}
