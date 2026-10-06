namespace API.SERVICE.Domain.Exceptions;

/// <summary>Regla de negocio violada. El middleware la traduce a HTTP 400.</summary>
public class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }

    public virtual int StatusCode => 400;
}
