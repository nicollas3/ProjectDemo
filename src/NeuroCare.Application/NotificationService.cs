using Microsoft.Extensions.Logging;
using NeuroCare.Domain;

namespace NeuroCare.Application;

public interface ISecurityNotificationService
{
    Task NotifyQuestionnaireSafetyFlagAsync(Patient patient, QuestionnaireResponse response, string questionnaireTitle, CancellationToken ct = default);
}

public class SecurityNotificationService(
    IDoctorRepository doctors, IIdentityGateway identity, IEmailSender email,
    ILogger<SecurityNotificationService> logger) : ISecurityNotificationService
{
    public async Task NotifyQuestionnaireSafetyFlagAsync(Patient patient, QuestionnaireResponse response, string questionnaireTitle, CancellationToken ct = default)
    {
        if (!patient.ResponsibleDoctorId.HasValue) return;
        var doctor = await doctors.GetByIdAsync(patient.ResponsibleDoctorId.Value, ct);
        if (doctor is null || !doctor.Active) return;
        var address = await identity.GetEmailAsync(doctor.UserId, ct);
        if (string.IsNullOrWhiteSpace(address)) return;
        var (subject, body) = EmailTemplates.SecurityAlert(patient.DisplayName, questionnaireTitle);
        try
        {
            await email.SendAsync(address, subject, body, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A gravação clínica não pode ser revertida por indisponibilidade do SMTP.
            logger.LogError(ex, "Falha ao enviar alerta de segurança da resposta {ResponseId}", response.Id);
        }
    }
}
