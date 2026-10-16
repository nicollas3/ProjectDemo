using NeuroCare.Domain;

namespace NeuroCare.Application;

internal static class Mappers
{
    public static AppointmentListItemDto ToDto(this Appointment a, IClock clock) =>
        new(a.Id, a.PatientId, a.Patient?.DisplayName ?? "", a.DoctorId, a.Doctor?.FullName ?? "",
            clock.ToLocal(a.StartsAtUtc), a.DurationMinutes, a.Type, a.Status);

    public static PatientListItemDto ToListDto(this Patient p, DateTime todayLocal) =>
        new(p.Id, p.FullName, p.SocialName, p.AgeOn(todayLocal), p.Phone, p.ResponsibleDoctor?.FullName, p.Status);
}
