using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NeuroCare.Application;

namespace NeuroCare.Infrastructure;

/// <summary>SMTP configurado por Email:Smtp:* (senha somente via User Secrets/variável de ambiente).</summary>
public class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = config["Email:Smtp:Host"] ?? throw new InvalidOperationException("Email:Smtp:Host não configurado.");
        var from = config["Email:From"];
        if (string.IsNullOrWhiteSpace(from)) throw new InvalidOperationException("Email:From não configurado.");

        using var client = new SmtpClient(host, config.GetValue("Email:Smtp:Port", 587))
        {
            EnableSsl = config.GetValue("Email:Smtp:EnableSsl", true)
        };
        var user = config["Email:Smtp:User"];
        if (!string.IsNullOrWhiteSpace(user))
            client.Credentials = new NetworkCredential(user, config["Email:Smtp:Password"]);

        using var message = new MailMessage(new MailAddress(from), new MailAddress(to))
        {
            Subject = subject, Body = htmlBody, IsBodyHtml = true
        };
        await client.SendMailAsync(message, ct);
        logger.LogInformation("E-mail enviado ({Subject})", subject);
    }
}

/// <summary>SOMENTE desenvolvimento: registra o e-mail (com o link) no log em vez de enviar.</summary>
public class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogWarning("[DEV] E-mail NÃO enviado (LogEmailSender). Para: {To} | Assunto: {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}

public class UnconfiguredEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "Envio de e-mail não configurado. Defina Email:Smtp:Host e Email:From (ou Email:UseLogSender=true apenas em desenvolvimento).");
}
