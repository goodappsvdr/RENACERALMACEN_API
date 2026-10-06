namespace API.SERVICE.Domain.Exceptions;

/// <summary>Conflicto con el estado actual (duplicados, registros asociados). HTTP 409.</summary>
public class ConflictException : BusinessException
{
    public ConflictException(string message) : base(message) { }

    public override int StatusCode => 409;
}
