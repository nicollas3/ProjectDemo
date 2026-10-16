using Microsoft.Extensions.Logging;
using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IAppointmentService
{
    Task<IReadOnlyList<AppointmentListItemDto>> ListAsync(DateTime? fromLocal, DateTime? toLocal, Guid? doctorId, CancellationToken ct = default);
    Task<SaveAppointmentDto> GetForEditAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(SaveAppointmentDto dto, CancellationToken ct = default);
    Task UpdateAsync(Guid id, SaveAppointmentDto dto, CancellationToken ct = default);
    Task TransitionAsync(Guid id, AppointmentAction action, CancellationToken ct = default);
}

public class AppointmentService(
    IAppointmentRepository appointments, IPatientRepository patients, IDoctorRepository doctors,
    IUnitOfWork uow, AccessGuard guard, ICurrentUser user, IAuditService audit, IClock clock,
    ILogger<AppointmentService> logger) : IAppointmentService
{
    public async Task<IReadOnlyList<AppointmentListItemDto>> ListAsync(
        DateTime? fromLocal, DateTime? toLocal, Guid? doctorId, CancellationToken ct = default)
    {
        guard.RequireOrganization();
        Guid? patientId = null;

        if (!guard.IsClinicalStaff)
        {
            // Paciente só enxerga as próprias consultas.
            if (!guard.IsPatient || user.UserId is null) throw new ForbiddenException();
            var self = await patients.GetByUserIdAsync(user.UserId.Value, ct) ?? throw new ForbiddenException();
            patientId = self.Id;
            doctorId = null;
        }

        var list = await appointments.ListAsync(
            fromLocal.HasValue ? clock.ToUtc(fromLocal.Value) : null,
            toLocal.HasValue ? clock.ToUtc(toLocal.Value) : null,
            doctorId, patientId, ct);
        return list.Select(a => a.ToDto(clock)).ToList();
    }

    public async Task<SaveAppointmentDto> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        guard.RequireClinicalStaff();
        var a = await appointments.GetByIdAsync(id, ct) ?? throw new NotFoundException("Consulta não encontrada.");
        return new SaveAppointmentDto
        {
            PatientId = a.PatientId, DoctorId = a.DoctorId, StartsAtLocal = clock.ToLocal(a.StartsAtUtc),
            DurationMinutes = a.DurationMinutes, Type = a.Type, Notes = a.Notes
        };
    }

    public async Task<Guid> CreateAsync(SaveAppointmentDto dto, CancellationToken ct = default)
    {
        var org = guard.RequireClinicalStaff();
        DtoValidator.EnsureValid(dto);
        var startUtc = await ValidateAsync(dto, null, requireFuture: true, ct);

        var appointment = new Appointment
        {
            OrganizationId = org, PatientId = dto.PatientId, DoctorId = dto.DoctorId,
            StartsAtUtc = startUtc, DurationMinutes = dto.DurationMinutes, Type = dto.Type,
            Status = AppointmentStatus.Scheduled, Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()
        };
        await appointments.AddAsync(appointment, ct);
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("Appointment.Create", nameof(Appointment), appointment.Id.ToString()), ct);
        logger.LogInformation("Appointment {AppointmentId} created in organization {OrganizationId}", appointment.Id, org);
        return appointment.Id;
    }

    public async Task UpdateAsync(Guid id, SaveAppointmentDto dto, CancellationToken ct = default)
    {
        guard.RequireClinicalStaff();
        DtoValidator.EnsureValid(dto);
        var a = await appointments.GetByIdAsync(id, ct) ?? throw new NotFoundException("Consulta não encontrada.");
        if (!a.IsOpen) throw new RequestValidationException("", "Somente consultas abertas podem ser editadas.");

        var newStartUtc = clock.ToUtc(dto.StartsAtLocal);
        var startUtc = await ValidateAsync(dto, id, requireFuture: newStartUtc != a.StartsAtUtc, ct);

        a.PatientId = dto.PatientId;
        a.DoctorId = dto.DoctorId;
        a.StartsAtUtc = startUtc;
        a.DurationMinutes = dto.DurationMinutes;
        a.Type = dto.Type;
        a.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("Appointment.Update", nameof(Appointment), id.ToString()), ct);
    }

    public async Task TransitionAsync(Guid id, AppointmentAction action, CancellationToken ct = default)
    {
        guard.RequireClinicalStaff();
        var a = await appointments.GetByIdAsync(id, ct) ?? throw new NotFoundException("Consulta não encontrada.");
        try
        {
            switch (action)
            {
                case AppointmentAction.Confirm: a.Confirm(); break;
                case AppointmentAction.Cancel: a.Cancel(); break;
                case AppointmentAction.Complete: a.Complete(); break;
                case AppointmentAction.NoShow: a.MarkNoShow(); break;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }
        catch (DomainException ex)
        {
            throw new RequestValidationException("", ex.Message);
        }
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry($"Appointment.{action}", nameof(Appointment), id.ToString()), ct);
    }

    private async Task<DateTime> ValidateAsync(SaveAppointmentDto dto, Guid? excludeId, bool requireFuture, CancellationToken ct)
    {
        var patient = await patients.GetByIdAsync(dto.PatientId, ct);
        if (patient is null || patient.Status != PatientStatus.Active)
            throw new RequestValidationException(nameof(dto.PatientId), "Paciente inválido ou inativo.");

        var doctor = await doctors.GetByIdAsync(dto.DoctorId, ct);
        if (doctor is null || !doctor.Active)
            throw new RequestValidationException(nameof(dto.DoctorId), "Médico inválido ou inativo.");

        var startUtc = clock.ToUtc(dto.StartsAtLocal);
        if (requireFuture && startUtc <= clock.UtcNow)
            throw new RequestValidationException(nameof(dto.StartsAtLocal), "Data e horário devem estar no futuro.");

        var endUtc = startUtc.AddMinutes(dto.DurationMinutes);
        if (await appointments.HasConflictAsync(dto.DoctorId, startUtc, endUtc, excludeId, ct))
            throw new RequestValidationException(nameof(dto.StartsAtLocal), "O médico já possui consulta neste horário.");

        return startUtc;
    }
}
