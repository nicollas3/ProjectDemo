namespace NeuroCare.Domain;

public class Appointment : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public AppointmentType Type { get; set; } = AppointmentType.FollowUp;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public string? Notes { get; set; }

    public Patient? Patient { get; set; }
    public Doctor? Doctor { get; set; }

    public DateTime EndsAtUtc => StartsAtUtc.AddMinutes(DurationMinutes);
    public bool IsOpen => Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed;

    public void Confirm()
    {
        if (Status != AppointmentStatus.Scheduled)
            throw new DomainException("Somente consultas agendadas podem ser confirmadas.");
        Status = AppointmentStatus.Confirmed;
    }

    public void Cancel()
    {
        if (!IsOpen) throw new DomainException("Esta consulta não pode mais ser cancelada.");
        Status = AppointmentStatus.Cancelled;
    }

    public void Complete()
    {
        if (!IsOpen) throw new DomainException("Somente consultas abertas podem ser concluídas.");
        Status = AppointmentStatus.Completed;
    }

    public void MarkNoShow()
    {
        if (!IsOpen) throw new DomainException("Somente consultas abertas podem ser marcadas como falta.");
        Status = AppointmentStatus.NoShow;
    }
}
