using API.SERVICE.Interfaces.Afip;

namespace API.TESTS.Infrastructure;

/// <summary>AFIP en memoria: último número autorizado, respuesta configurable y caídas simuladas.</summary>
public sealed class FakeAfipGateway : IAfipGateway
{
    public long UltimoAutorizado { get; set; } = 122;

    /// <summary>Respuesta al pedido de CAE; por defecto aprueba con el número siguiente al último autorizado.</summary>
    public Func<AfipSolicitudCae, AfipRespuestaCae>? Responder { get; set; }

    public bool CaidoAlConsultarNumero { get; set; }
    public bool CaidoAlPedirCae { get; set; }

    public List<AfipSolicitudCae> Solicitudes { get; } = [];

    public Task<long> GetUltimoAutorizadoAsync(AfipEmisor emisor, int cbteTipo, CancellationToken cancellationToken = default)
    {
        if (CaidoAlConsultarNumero)
            throw new AfipNoDisponibleException("AFIP caído (test)");
        return Task.FromResult(UltimoAutorizado);
    }

    public Task<AfipRespuestaCae> SolicitarCaeAsync(AfipSolicitudCae solicitud, CancellationToken cancellationToken = default)
    {
        Solicitudes.Add(solicitud);
        if (CaidoAlPedirCae)
            throw new AfipNoDisponibleException("AFIP no respondió (test)");

        var respuesta = Responder?.Invoke(solicitud)
            ?? new AfipRespuestaCae("A", "71234567890123", "20261016", UltimoAutorizado + 1, null);
        if (respuesta.Aprobado)
            UltimoAutorizado = respuesta.Numero;
        return Task.FromResult(respuesta);
    }
}
