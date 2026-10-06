namespace API.SERVICE.Domain.Exceptions;

/// <summary>Recurso inexistente. HTTP 404.</summary>
public class NotFoundException : BusinessException
{
    public NotFoundException(string message) : base(message) { }

    public override int StatusCode => 404;
}
