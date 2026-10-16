using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IMedicationService
{
    Task<IReadOnlyList<MedicationListItemDto>> ListAsync(Guid? patientId, CancellationToken ct = default);
    Task<MedicationEditModel> GetForEditAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid patientId, SaveMedicationDto dto, CancellationToken ct = default);
    Task UpdateAsync(Guid id, SaveMedicationDto dto, CancellationToken ct = default);
    Task TransitionAsync(Guid id, MedicationAction action, CancellationToken ct = default);
}

/// <summary>Médico prescreve/altera; paciente apenas consulta os próprios medicamentos.</summary>
public class MedicationService(
    ClinicalContext context, IMedicationRepository medications, IUnitOfWork uow,
    AccessGuard guard, IAuditService audit, IClock clock) : IMedicationService
{
    public async Task<IReadOnlyList<MedicationListItemDto>> ListAsync(Guid? patientId, CancellationToken ct = default)
    {
        var patient = await context.ResolveAsync(patientId, ct);
        var list = await medications.ListByPatientAsync(patient.Id, ct);
        await audit.RecordAsync(new AuditEntry("Medication.List", nameof(Patient), patient.Id.ToString()), ct);
        return list.Select(m => new MedicationListItemDto(
            m.Id, m.Name, m.Dosage, m.Frequency, m.Instructions, m.StartDate, m.EndDate, m.Status,
            m.PrescribedBy?.FullName ?? "", m.ReminderEnabled, m.ReminderTimes)).ToList();
    }

    public async Task<MedicationEditModel> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        guard.RequireDoctor();
        var m = await LoadAsync(id, ct);
        return new MedicationEditModel(m.PatientId, m.Status == MedicationStatus.Finished, new SaveMedicationDto
        {
            Name = m.Name, Dosage = m.Dosage, Frequency = m.Frequency, Instructions = m.Instructions,
            StartDate = m.StartDate, EndDate = m.EndDate, ReminderEnabled = m.ReminderEnabled, ReminderTimes = m.ReminderTimes
        });
    }

    public async Task<Guid> CreateAsync(Guid patientId, SaveMedicationDto dto, CancellationToken ct = default)
    {
        var org = guard.RequireDoctor();
        DtoValidator.EnsureValid(dto);
        Validate(dto);
        var patient = await context.ResolveAsync(patientId, ct);
        var doctor = await context.CurrentDoctorAsync(ct);

        var name = dto.Name.Trim();
        if (await medications.HasOpenWithNameAsync(patient.Id, name, null, ct))
            throw new RequestValidationException(nameof(dto.Name), "Já existe um medicamento em uso ou suspenso com este nome.");

        var med = new Medication { OrganizationId = org, PatientId = patient.Id, PrescribedByDoctorId = doctor.Id, Status = MedicationStatus.Active };
        Apply(dto, med);
        await medications.AddAsync(med, ct);
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("Medication.Create", nameof(Medication), med.Id.ToString()), ct);
        return med.Id;
    }

    public async Task UpdateAsync(Guid id, SaveMedicationDto dto, CancellationToken ct = default)
    {
        guard.RequireDoctor();
        DtoValidator.EnsureValid(dto);
        Validate(dto);
        var m = await LoadAsync(id, ct);
        if (m.Status == MedicationStatus.Finished)
            throw new RequestValidationException("", "Medicamento finalizado não pode ser editado.");
        if (await medications.HasOpenWithNameAsync(m.PatientId, dto.Name.Trim(), id, ct))
            throw new RequestValidationException(nameof(dto.Name), "Já existe um medicamento em uso ou suspenso com este nome.");

        Apply(dto, m);
        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry("Medication.Update", nameof(Medication), id.ToString()), ct);
    }

    public async Task TransitionAsync(Guid id, MedicationAction action, CancellationToken ct = default)
    {
        guard.RequireDoctor();
        var m = await LoadAsync(id, ct);
        try
        {
            switch (action)
            {
                case MedicationAction.Suspend: m.Suspend(); break;
                case MedicationAction.Resume: m.Resume(); break;
                case MedicationAction.Finish: m.Finish(clock.ToLocal(clock.UtcNow).Date); break;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }
        catch (DomainException ex) { throw new RequestValidationException("", ex.Message); }

        await uow.SaveChangesAsync(ct);
        await audit.RecordAsync(new AuditEntry($"Medication.{action}", nameof(Medication), id.ToString()), ct);
    }

    private async Task<Medication> LoadAsync(Guid id, CancellationToken ct)
    {
        var org = guard.RequireOrganization();
        var m = await medications.GetByIdAsync(id, ct);
        if (m is null || m.OrganizationId != org) throw new NotFoundException("Medicamento não encontrado.");
        return m;
    }

    private static void Validate(SaveMedicationDto dto)
    {
        if (dto.StartDate.Year < 1900)
            throw new RequestValidationException(nameof(dto.StartDate), "Data de início inválida.");
        if (dto.EndDate.HasValue && dto.EndDate.Value.Date < dto.StartDate.Date)
            throw new RequestValidationException(nameof(dto.EndDate), "A data final não pode ser anterior à inicial.");
        if (dto.ReminderEnabled)
        {
            var times = ParseReminderTimes(dto.ReminderTimes);
            if (times.Count == 0) throw new RequestValidationException(nameof(dto.ReminderTimes), "Informe ao menos um horário no formato HH:mm.");
        }
    }

    private static void Apply(SaveMedicationDto dto, Medication m)
    {
        m.Name = dto.Name.Trim();
        m.Dosage = dto.Dosage.Trim();
        m.Frequency = dto.Frequency.Trim();
        m.Instructions = string.IsNullOrWhiteSpace(dto.Instructions) ? null : dto.Instructions.Trim();
        m.StartDate = dto.StartDate.Date;
        m.EndDate = dto.EndDate?.Date;
        m.ReminderEnabled = dto.ReminderEnabled;
        m.ReminderTimes = dto.ReminderEnabled ? string.Join(",", ParseReminderTimes(dto.ReminderTimes).Select(t => t.ToString(@"hh\:mm"))) : null;
    }

    private static List<TimeSpan> ParseReminderTimes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var result = new List<TimeSpan>();
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TimeSpan.TryParseExact(token, @"hh\:mm", null, out var time))
                throw new RequestValidationException(nameof(SaveMedicationDto.ReminderTimes), "Use horários no formato HH:mm separados por vírgula, ex.: 08:00,20:00.");
            if (!result.Contains(time)) result.Add(time);
        }
        return result.OrderBy(x => x).ToList();
    }
}
