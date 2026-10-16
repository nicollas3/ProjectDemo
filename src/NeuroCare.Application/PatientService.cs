using Microsoft.Extensions.Logging;
using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IPatientService
{
    Task<IReadOnlyList<PatientListItemDto>> ListAsync(string? term, bool includeInactive, CancellationToken ct = default);
    Task<PatientDetailsDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<UpdatePatientDto> GetForEditAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreatePatientDto dto, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdatePatientDto dto, CancellationToken ct = default);
    Task SetStatusAsync(Guid id, PatientStatus status, CancellationToken ct = default);
}

public class PatientService(
    IPatientRepository patients, IDoctorRepository doctors, IAppointmentRepository appointments,
    IUnitOfWork uow, AccessGuard guard, IAuditService audit, IClock clock, ILogger<PatientService> logger) : IPatientService
{
    private DateTime TodayLocal => clock.ToLocal(clock.UtcNow).Date;

    public async Task<IReadOnlyList<PatientListItemDto>> ListAsync(string? term, bool includeInactive, CancellationToken ct = default)
    {
        guard.RequireClinicalStaff();
        var list = await patients.SearchAsync(term, includeInactive, ct);
        var today = TodayLocal;
        return list.Select(p => p.ToListDto(today)).ToList();
    }

    public async Task<PatientDetailsDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        guard.RequireOrganization();
        var p = await patients.GetByIdAsync(id, ct) ?? throw new NotFoundException("Paciente não encontrado.");
        guard.EnsureCanAccessPatient(p);

        var appts = await appointments.ListAsync(null, null, null, id, ct);
        await audit.RecordAsync(new AuditEntry("Patient.View", nameof(Patient), id.ToString()), ct);

        return new PatientDetailsDto(
            p.Id, p.FullName, p.SocialName, p.MaskedCpf, p.BirthDate, p.AgeOn(TodayLocal), p.Sex,
            p.Phone, p.Email, p.Address, p.EmergencyContactName, p.EmergencyContactPhone, p.Notes,
            p.ResponsibleDoctorId, p.ResponsibleDoctor?.FullName, p.Status, p.CreatedAt,
            appts.OrderByDescending(a => a.StartsAtUtc).Select(a => a.ToDto(clock)).ToList(),
            p.UserId.HasValue);
    }

    public async Task<UpdatePatientDto> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        guard.RequireClinicalStaff();
        var p = await patients.GetByIdAsync(id, ct) ?? throw new NotFoundException("Paciente não encontrado.");
        guard.EnsureCanAccessPatient(p);
        return new UpdatePatientDto
        {
            FullName = p.FullName, SocialName = p.SocialName, BirthDate = p.BirthDate, Sex = p.Sex,
            Phone = p.Phone, Email = p.Email, Address = p.Address,
            EmergencyContactName = p.EmergencyContactName, EmergencyContactPhone = p.EmergencyContactPhone,
            Notes = p.Notes, ResponsibleDoctorId = p.ResponsibleDoctorId
        };
    }

    public async Task<Guid> CreateAsync(CreatePatientDto dto, CancellationToken ct = default)
    {
        var org = guard.RequireClinicalStaff();
        DtoValidator.EnsureValid(dto);

        var cpf = Cpf.Normalize(dto.Cpf);
        if (!Cpf.IsValid(cpf)) throw new RequestValidationException(nameof(dto.Cpf), "CPF inválido.");
        ValidateCommon(dto);
        if (await patients.CpfExistsAsync(cpf, null, ct))
            throw new RequestValidationException(nameof(dto.Cpf), "Já existe paciente com este CPF nesta organização.");
        await EnsureDoctorAsync(dto.ResponsibleDoctorId, ct);

        var patient = new Patient { OrganizationId = org, Cpf = cpf, Status = PatientStatus.Active };
        Apply(dto, patient);
        await patients.AddAsync(patient, ct);
        await uow.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry("Patient.Create", nameof(Patient), patient.Id.ToString()), ct);
        logger.LogInformation("Patient {PatientId} created in organization {OrganizationId}", patient.Id, org);
        return patient.Id;
    }

    public async Task UpdateAsync(Guid id, UpdatePatientDto dto, CancellationToken ct = default)
    {
        guard.RequireClinicalStaff();
        DtoValidator.EnsureValid(dto);
        ValidateCommon(dto);

        var p = await patients.GetByIdAsync(id, ct) ?? throw new NotFoundException("Paciente não encontrado.");
        guard.EnsureCanAccessPatient(p);
        await EnsureDoctorAsync(dto.ResponsibleDoctorId, ct);

        Apply(dto, p);
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("Patient.Update", nameof(Patient), id.ToString()), ct);
    }

    public async Task SetStatusAsync(Guid id, PatientStatus status, CancellationToken ct = default)
    {
        guard.RequireClinicalStaff();
        var p = await patients.GetByIdAsync(id, ct) ?? throw new NotFoundException("Paciente não encontrado.");
        guard.EnsureCanAccessPatient(p);
        p.Status = status;
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("Patient.StatusChange", nameof(Patient), id.ToString(), $"Status={status}"), ct);
    }

    private void ValidateCommon(PatientInputDto dto)
    {
        if (dto.BirthDate.Year < 1900 || dto.BirthDate.Date > TodayLocal)
            throw new RequestValidationException(nameof(dto.BirthDate), "Data de nascimento inválida.");
    }

    private async Task EnsureDoctorAsync(Guid? doctorId, CancellationToken ct)
    {
        if (doctorId is null) return;
        var d = await doctors.GetByIdAsync(doctorId.Value, ct); // filtro de tenant garante a mesma organização
        if (d is null || !d.Active)
            throw new RequestValidationException(nameof(PatientInputDto.ResponsibleDoctorId), "Médico responsável inválido.");
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static void Apply(PatientInputDto dto, Patient p)
    {
        p.FullName = dto.FullName.Trim();
        p.SocialName = Clean(dto.SocialName);
        p.BirthDate = dto.BirthDate.Date;
        p.Sex = dto.Sex;
        p.Phone = Clean(dto.Phone);
        p.Email = Clean(dto.Email);
        p.Address = Clean(dto.Address);
        p.EmergencyContactName = Clean(dto.EmergencyContactName);
        p.EmergencyContactPhone = Clean(dto.EmergencyContactPhone);
        p.Notes = Clean(dto.Notes);
        p.ResponsibleDoctorId = dto.ResponsibleDoctorId;
    }
}
