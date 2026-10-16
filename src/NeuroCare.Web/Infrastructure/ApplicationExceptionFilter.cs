using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NeuroCare.Application;
using NeuroCare.Domain;

namespace NeuroCare.Web.Infrastructure;

/// <summary>Tratamento centralizado de erros de aplicação (MVC e API). Nunca loga dados clínicos.</summary>
public class ApplicationExceptionFilter(ILogger<ApplicationExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var isApi = context.HttpContext.Request.Path.StartsWithSegments("/api");

        switch (context.Exception)
        {
            case NotFoundException:
                context.Result = isApi
                    ? new NotFoundObjectResult(new ProblemDetails { Status = 404, Title = "Recurso não encontrado." })
                    : new RedirectToActionResult("NotFound", "Home", null);
                break;

            case ForbiddenException or TenantViolationException:
                logger.LogWarning("Acesso negado em {Path} para o usuário {UserId}",
                    context.HttpContext.Request.Path, context.HttpContext.User.Identity?.Name is null ? "anon" : "authenticated");
                context.Result = isApi
                    ? new ObjectResult(new ProblemDetails { Status = 403, Title = "Acesso negado." }) { StatusCode = 403 }
                    : new RedirectToActionResult("AccessDenied", "Account", null);
                break;

            case RequestValidationException ex when isApi:
                context.Result = new BadRequestObjectResult(new ValidationProblemDetails(
                    ex.Errors.ToDictionary(k => k.Key, v => v.Value)));
                break;

            default:
                return; // demais exceções seguem para o handler global
        }
        context.ExceptionHandled = true;
    }
}
