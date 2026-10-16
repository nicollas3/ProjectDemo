namespace NeuroCare.Application;

public class NotFoundException(string message = "Recurso não encontrado.") : Exception(message);

public class ForbiddenException(string message = "Acesso negado.") : Exception(message);

public class RequestValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public RequestValidationException(IDictionary<string, string[]> errors) : base("Dados inválidos.")
        => Errors = new Dictionary<string, string[]>(errors);

    public RequestValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] }) { }
}
