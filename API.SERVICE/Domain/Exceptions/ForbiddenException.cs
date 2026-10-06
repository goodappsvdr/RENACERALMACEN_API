namespace API.SERVICE.Domain.Exceptions;

/// <summary>Usuario autenticado sin permiso. HTTP 403.</summary>
public class ForbiddenException : BusinessException
{
    public ForbiddenException(string message) : base(message) { }

    public override int StatusCode => 403;
}
