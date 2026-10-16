using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Infrastructure;

/// <summary>
/// Processa lembretes sem depender de uma sessão web. Consultas ignoram filtros globais de tenant,
/// mas cada envio preserva OrganizationId e nunca mistura dados entre organizações.
/// </summary>
public class ReminderWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<ReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Notifications:Enabled", true)) return;
        var interval = TimeSpan.FromSeconds(Math.Clamp(config.GetValue("Notifications:WorkerIntervalSeconds", 60), 30, 3600));
        using var timer = new PeriodicTimer(interval);
        do
        {
            try { await ProcessAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Falha no processamento de lembretes do NeuroCare."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NeuroCareDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var nowUtc = clock.UtcNow;
        var nowLocal = clock.ToLocal(nowUtc);

        await ProcessAppointmentsAsync(db, email, clock, nowUtc, ct);
        await ProcessMedicationsAsync(db, email, nowLocal, ct);
    }

    private async Task ProcessAppointmentsAsync(NeuroCareDbContext db, IEmailSender email, IClock clock, DateTime nowUtc, CancellationToken ct)
    {
        var leadHours = Math.Clamp(config.GetValue("Notifications:AppointmentLeadHours", 24), 1, 168);
        var to = nowUtc.AddHours(leadHours);
        var appointments = await db.Appointments.IgnoreQueryFilters().AsNoTracking()
            .Where(a => (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed)
                        && a.StartsAtUtc > nowUtc && a.StartsAtUtc <= to)
            .Take(1000).ToListAsync(ct);

        foreach (var a in appointments)
        {
            var patient = await db.Patients.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(p => p.Id == a.PatientId, ct);
            if (patient?.UserId is null) continue;
            if (config.GetValue("Notifications:RequireConsent", true) && !await HasNotificationConsentAsync(db, patient.Id, ct)) continue;
            var recipient = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == patient.UserId && u.Active && u.EmailConfirmed, ct);
            if (string.IsNullOrWhiteSpace(recipient?.Email)) continue;
            var occurrence = a.StartsAtUtc.ToString("yyyyMMddHHmm");
            var template = EmailTemplates.AppointmentReminder(patient.DisplayName, clock.ToLocal(a.StartsAtUtc));
            await SendOnceAsync(db, email, a.OrganizationId, NotificationKind.AppointmentReminder, a.Id,
                recipient.Id, occurrence, recipient.Email, template, ct);
        }
    }

    private async Task ProcessMedicationsAsync(NeuroCareDbContext db, IEmailSender email, DateTime nowLocal, CancellationToken ct)
    {
        var meds = await db.Medications.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.Status == MedicationStatus.Active && m.ReminderEnabled && m.ReminderTimes != null)
            .Take(5000).ToListAsync(ct);
        foreach (var med in meds)
        {
            if (med.StartDate.Date > nowLocal.Date || (med.EndDate.HasValue && med.EndDate.Value.Date < nowLocal.Date)) continue;
            var due = ParseTimes(med.ReminderTimes!).Any(t => Math.Abs((nowLocal.TimeOfDay - t).TotalMinutes) <= 2);
            if (!due) continue;
            var patient = await db.Patients.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(p => p.Id == med.PatientId, ct);
            if (patient?.UserId is null) continue;
            if (config.GetValue("Notifications:RequireConsent", true) && !await HasNotificationConsentAsync(db, patient.Id, ct)) continue;
            var recipient = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == patient.UserId && u.Active && u.EmailConfirmed, ct);
            if (string.IsNullOrWhiteSpace(recipient?.Email)) continue;
            var matched = ParseTimes(med.ReminderTimes!).OrderBy(t => Math.Abs((nowLocal.TimeOfDay - t).TotalMinutes)).First();
            var occurrence = $"{nowLocal:yyyyMMdd}-{matched:hh\\:mm}";
            var template = EmailTemplates.MedicationReminder(patient.DisplayName, med.Name, med.Dosage);
            await SendOnceAsync(db, email, med.OrganizationId, NotificationKind.MedicationReminder, med.Id,
                recipient.Id, occurrence, recipient.Email, template, ct);
        }
    }

    private static Task<bool> HasNotificationConsentAsync(NeuroCareDbContext db, Guid patientId, CancellationToken ct) =>
        db.ConsentRecords.IgnoreQueryFilters().AsNoTracking().AnyAsync(x => x.PatientId == patientId &&
            x.PurposeKey == "notifications-email" && x.Status == ConsentStatus.Granted, ct);

    private static IEnumerable<TimeSpan> ParseTimes(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => TimeSpan.TryParseExact(x, @"hh\:mm", null, out var t) ? (TimeSpan?)t : null)
        .Where(x => x.HasValue).Select(x => x!.Value);

    private async Task SendOnceAsync(NeuroCareDbContext db, IEmailSender sender, Guid orgId, NotificationKind kind,
        Guid referenceId, Guid recipientUserId, string occurrenceKey, string to, (string Subject, string Body) content, CancellationToken ct)
    {
        var dispatch = await db.NotificationDispatches.IgnoreQueryFilters().FirstOrDefaultAsync(x =>
            x.OrganizationId == orgId && x.Kind == kind && x.ReferenceId == referenceId &&
            x.RecipientUserId == recipientUserId && x.OccurrenceKey == occurrenceKey, ct);
        if (dispatch?.Status == NotificationStatus.Sent) return;
        if (dispatch is null)
        {
            dispatch = new NotificationDispatch
            {
                OrganizationId = orgId, Kind = kind, ReferenceId = referenceId,
                RecipientUserId = recipientUserId, OccurrenceKey = occurrenceKey, Status = NotificationStatus.Pending
            };
            await db.NotificationDispatches.AddAsync(dispatch, ct);
            await db.SaveChangesAsync(ct);
        }

        try
        {
            await sender.SendAsync(to, content.Subject, content.Body, ct);
            dispatch.Status = NotificationStatus.Sent;
            dispatch.SentAtUtc = DateTime.UtcNow;
            dispatch.ErrorCode = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            dispatch.Status = NotificationStatus.Failed;
            dispatch.ErrorCode = ex.GetType().Name;
            logger.LogError(ex, "Falha ao enviar {Kind} para usuário {RecipientUserId}", kind, recipientUserId);
        }
        await db.SaveChangesAsync(ct);
    }
}
