using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using NeuroCare.Application;

namespace NeuroCare.Web.Infrastructure;

/// <summary>
/// Links de e-mail usam App:PublicBaseUrl (obrigatória fora de Development) para evitar
/// host header injection em e-mails de redefinição de senha.
/// </summary>
public class LinkBuilder(IConfiguration config, IHttpContextAccessor accessor, LinkGenerator generator) : ILinkBuilder
{
    public string ResetPassword(Guid userId, string token)
    {
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var path = generator.GetPathByAction("ResetPassword", "Account", new { userId, token = encoded })
                   ?? throw new InvalidOperationException("Rota ResetPassword não encontrada.");

        var baseUrl = config["App:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl)) return baseUrl.TrimEnd('/') + path;

        var request = accessor.HttpContext?.Request
                      ?? throw new InvalidOperationException("App:PublicBaseUrl não configurada e não há requisição HTTP.");
        return $"{request.Scheme}://{request.Host}{path}";
    }
}
