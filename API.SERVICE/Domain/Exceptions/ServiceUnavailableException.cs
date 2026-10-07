namespace API.SERVICE.Domain.Exceptions;

/// <summary>Un servicio externo necesario (p. ej. AFIP) no está disponible. HTTP 503.</summary>
public class ServiceUnavailableException : BusinessException
{
    public ServiceUnavailableException(string message) : base(message) { }

    public override int StatusCode => 503;
}
