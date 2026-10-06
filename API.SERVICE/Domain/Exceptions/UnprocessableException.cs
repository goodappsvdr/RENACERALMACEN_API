namespace API.SERVICE.Domain.Exceptions;

/// <summary>La operación se procesó pero un tercero la rechazó (p. ej. AFIP rechazó el comprobante). HTTP 422.</summary>
public class UnprocessableException : BusinessException
{
    public UnprocessableException(string message) : base(message) { }

    public override int StatusCode => 422;
}
