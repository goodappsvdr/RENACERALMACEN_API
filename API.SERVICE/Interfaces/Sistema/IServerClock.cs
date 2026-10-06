namespace API.SERVICE.Interfaces.Sistema;

/// <summary>
/// Fecha y hora del servidor SQL (GETDATE()), igual que FechaHoraServidor() del WebForms.
/// Las columnas de fecha legacy guardan hora local del servidor de base, no UTC.
/// </summary>
public interface IServerClock
{
    Task<DateTime> GetNowAsync(CancellationToken cancellationToken = default);
}
