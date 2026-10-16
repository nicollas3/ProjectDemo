using Microsoft.Extensions.Logging;

namespace NeuroCare.Application;

public interface IInviteService
{
    /// <summary>Envia o convite (link para definir senha). Retorna false se o envio falhar; nunca lança.</summary>
    Task<bool> TrySendAsync(Guid userId, string toEmail, string fullName, CancellationToken ct = default);
}

public class InviteService(IIdentityGateway identity, ILinkBuilder links, IEmailSender sender, ILogger<InviteService> logger) : IInviteService
{
    public async Task<bool> TrySendAsync(Guid userId, string toEmail, string fullName, CancellationToken ct = default)
    {
        try
        {
            var token = await identity.GenerateResetTokenAsync(userId, ct);
            var (subject, body) = EmailTemplates.Invite(fullName, links.ResetPassword(userId, token));
            await sender.SendAsync(toEmail, subject, body, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Falha ao enviar convite para o usuário {UserId}", userId);
            return false;
        }
    }
}
