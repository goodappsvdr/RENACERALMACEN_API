namespace API.SERVICE.Domain.Exceptions;

/// <summary>Credenciales inválidas o ausentes. HTTP 401.</summary>
public class UnauthorizedException : BusinessException
{
    public UnauthorizedException(string message) : base(message) { }

    public override int StatusCode => 401;
}
