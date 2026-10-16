using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct = default);
}

public class DashboardService(
    IPatientRepository patients, IDoctorRepository doctors, IAppointmentRepository appointments,
    IQuestionnaireResponseRepository questionnaires, AccessGuard guard, IClock clock) : IDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        guard.RequireClinicalStaff();

        var nowUtc = clock.UtcNow;
        var todayLocal = clock.ToLocal(nowUtc).Date;
        var dayStart = clock.ToUtc(todayLocal);
        var dayEnd = dayStart.AddDays(1);
        var since30 = nowUtc.AddDays(-30);

        // Sequencial de propósito: DbContext não é thread-safe.
        var today = await appointments.ListAsync(dayStart, dayEnd, null, null, ct);
        var counts = await appointments.CountByStatusAsync(since30, nowUtc, ct);
        var activePatients = await patients.CountByStatusAsync(PatientStatus.Active, ct);
        var activeDoctors = await doctors.CountActiveAsync(ct);
        var upcoming = await appointments.CountUpcomingAsync(nowUtc, ct);
        var withoutFollowUp = await patients.CountWithoutFollowUpAsync(nowUtc.AddDays(-90), ct);
        var recent = await patients.RecentAsync(5, ct);

        // Alertas de segurança de questionários: somente médicos veem (ClinicAdmin não acessa dados clínicos).
        IReadOnlyList<SafetyAlertDto> alerts = [];
        if (guard.IsDoctor)
        {
            var flagged = await questionnaires.ListSafetyFlagsAsync(nowUtc.AddDays(-30), 5, ct);
            alerts = flagged.Select(r => new SafetyAlertDto(r.Id, r.PatientId, r.Patient?.DisplayName ?? "",
                QuestionnaireCatalog.Find(r.QuestionnaireKey)?.Title ?? r.QuestionnaireKey, clock.ToLocal(r.AnsweredAtUtc))).ToList();
        }

        return new DashboardDto(
            activePatients, activeDoctors, today.Count, upcoming, withoutFollowUp,
            counts.GetValueOrDefault(AppointmentStatus.NoShow),
            counts.GetValueOrDefault(AppointmentStatus.Cancelled),
            today.Select(a => a.ToDto(clock)).ToList(),
            recent.Select(p => p.ToListDto(todayLocal)).ToList(),
            counts,
            alerts);
    }
}
