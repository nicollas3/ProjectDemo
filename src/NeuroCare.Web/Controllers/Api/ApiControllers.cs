using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuroCare.Application;

namespace NeuroCare.Web.Controllers.Api;

/// <summary>API somente leitura (Etapa 1), sempre com DTOs. Autenticação por cookie; JWT planejado para o app mobile.</summary>
[ApiController, Route("api/patients"), Authorize(Policy = "ClinicalStaff")]
public class PatientsApiController(IPatientService patients) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PatientListItemDto>>> List([FromQuery] string? term, CancellationToken ct) =>
        Ok(await patients.ListAsync(term, false, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PatientDetailsDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await patients.GetAsync(id, ct));
}

[ApiController, Route("api/appointments"), Authorize]
public class AppointmentsApiController(IAppointmentService appointments) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppointmentListItemDto>>> List(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? doctorId, CancellationToken ct) =>
        Ok(await appointments.ListAsync(from, to, doctorId, ct));
}
