namespace API.SERVICE.Models.Common;

/// <summary>Cuerpo estándar de error que devuelve el middleware.</summary>
public sealed record ErrorCatchResponse(string Message, int StatusCode);
