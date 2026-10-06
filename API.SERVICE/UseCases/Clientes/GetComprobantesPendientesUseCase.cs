using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Models.Clientes;

namespace API.SERVICE.UseCases.Clientes;

public interface IGetComprobantesPendientesUseCase
{
    Task<IReadOnlyList<ComprobantePendienteDisplay>> ExecuteAsync(int idEntidad, CancellationToken cancellationToken = default);
}

/// <summary>
/// Comprobantes de la cta. cte. del cliente que se pueden imputar en un recibo
/// (port de EntidadesCtaCte_BuscarPorID_Entidad_Recibos, que usa el formulario de recibos).
/// </summary>
public sealed class GetComprobantesPendientesUseCase : IGetComprobantesPendientesUseCase
{
    private readonly IReciboCobroRepository _repository;
    private readonly IServerClock _clock;

    public GetComprobantesPendientesUseCase(IReciboCobroRepository repository, IServerClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ComprobantePendienteDisplay>> ExecuteAsync(int idEntidad, CancellationToken cancellationToken = default)
    {
        var ahora = await _clock.GetNowAsync(cancellationToken);
        var filas = await _repository.GetComprobantesPendientesAsync(idEntidad, cancellationToken);

        return filas.Select(f =>
        {
            var tipo = f.IdComprobanteTipo ?? 0;
            var conVencimiento = ImputacionRules.TiposConVencimiento.Contains(tipo);
            var dias = conVencimiento && f.FechaVencimiento is { } vto ? Math.Max(0, (ahora.Date - vto.Date).Days) : 0;

            return new ComprobantePendienteDisplay(
                IdComprobante: f.IdComprobante ?? 0,
                IdComprobanteTipo: tipo,
                IdEntidad: f.IdEntidad ?? idEntidad,
                Comprobante: f.Concepto,
                RazonSocial: f.RazonSocial,
                FechaEmision: f.Fecha,
                FechaVencimiento: f.FechaVencimiento,
                Saldo: ImputacionRules.Redondear(ImputacionRules.SaldoVisible(tipo, f.Saldo ?? 0)),
                InteresAplicado: f.InteresAplicado ?? 0,
                InteresCliente: f.InteresCliente ?? 0,
                DiasInteres: f.DiasInteres ?? 0,
                // El SP devolvía 1/0 o "Sin Recibo" (sin vencimiento) en la misma columna; acá es un bool nullable.
                Vencido: f.FechaVencimiento is null ? null : conVencimiento && f.FechaVencimiento < ahora,
                DiasVencidos: dias);
        }).ToList();
    }
}
