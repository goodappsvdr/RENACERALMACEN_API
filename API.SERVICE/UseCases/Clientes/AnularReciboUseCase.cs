using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;

namespace API.SERVICE.UseCases.Clientes;

public interface IAnularReciboUseCase
{
    Task ExecuteAsync(int idRecibo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Anulación de recibo de cobro en una transacción. Port de Editar_Ws (FrmRecibos): anula el recibo, su detalle,
/// los movimientos de caja, cheques, bancos y retenciones, devuelve el saldo a los comprobantes imputados
/// (y les restaura el estado) y anula la cta. cte. del recibo.
/// </summary>
public sealed class AnularReciboUseCase : IAnularReciboUseCase
{
    private readonly IReciboCobroRepository _repository;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;

    public AnularReciboUseCase(IReciboCobroRepository repository, IReferenciasRepository referencias, IUnitOfWork unitOfWork, IServerClock clock)
    {
        _repository = repository;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task ExecuteAsync(int idRecibo, CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var codigos = await CodigosCobranza.LoadAsync(_referencias, ct);
            var rec = codigos.Rec;

            var recibo = await _repository.GetReciboAsync(idRecibo, ct)
                ?? throw new NotFoundException($"Recibo {idRecibo} no existe.");

            var anulado = await _referencias.IdAsync(EstadosCobranza.ReciboAnulado, ct);
            if (recibo.Estado == anulado)
                throw new ConflictException($"El recibo {idRecibo} ya está anulado.");

            var ahora = await _clock.GetNowAsync(ct);

            await _repository.SetEstadoReciboAsync(idRecibo, anulado, ct);
            await _repository.AnularDetalleAsync(idRecibo, ahora, ct);
            await _repository.AnularCajaAsync(rec, idRecibo, ct);
            await _repository.AnularChequesAsync(idRecibo, rec, await _referencias.IdAsync(EstadosCobranza.ChequeAnulado, ct), ct);
            await _repository.AnularMovimientosBancoAsync(rec, idRecibo, await _referencias.IdAsync(EstadosCobranza.MovimientoBancoAnulado, ct), ahora, ct);
            await _repository.AnularRetencionesAsync(rec, idRecibo, await _referencias.IdAsync(EstadosCobranza.RetencionAnulada, ct), ct);

            // Comprobantes imputados: se leen antes de borrar las imputaciones del recibo. El estado de cada uno
            // se decide después del borrado (si sigue imputado por OTRO recibo queda cobrado parcial).
            var imputaciones = await _repository.GetImputacionesAsync(idRecibo, ct);
            if (imputaciones.Count > 0)
                await _repository.BorrarImputacionesAsync(idRecibo, ct);

            foreach (var imputacion in imputaciones)
            {
                var idComprobante = imputacion.IdDocumentoCliente ?? 0;
                var idTipo = imputacion.IdComprobanteTipo ?? 0;

                // Como el ERP: se devuelve lo imputado menos el interés acumulado del comprobante.
                var saldo = ImputacionRules.Redondear((imputacion.ImporteRecibo ?? 0) - imputacion.InteresAplicado);
                await _repository.RevertirImputacionCtaCteAsync(idComprobante, idTipo, saldo, imputacion.InteresAplicado, ahora, ct);

                await RestaurarEstadoAsync(codigos, idTipo, idComprobante, ct);
            }

            var idCtaCte = await _repository.GetIdCtaCteAsync(rec, idRecibo, ct);
            if (idCtaCte is not null)
                await _repository.AnularCtaCteAsync(idCtaCte.Value, await _referencias.IdAsync(EstadosCobranza.CtaCteAnulado, ct), ahora, ct);

            return true;
        }, cancellationToken);

    /// <summary>Mismo orden de comparación que el ERP (FV, VEN/NC, FC/COM, REC, OP).</summary>
    private async Task RestaurarEstadoAsync(CodigosCobranza c, int idTipo, int idComprobante, CancellationToken ct)
    {
        if (idTipo == c.Fv)
        {
            var estado = await _repository.TieneRelacionAsync(idComprobante, ct) ? EstadosCobranza.DocumentoCancelado : EstadosCobranza.DocumentoGenerado;
            await _repository.SetEstadoDocumentoClienteAsync(idComprobante, await _referencias.IdAsync(estado, ct), ct);
        }
        else if (idTipo == c.Ven || idTipo == c.Nc)
        {
            var estado = await _repository.TieneImputacionesAsync(idComprobante, ct) ? EstadosCobranza.DocumentoCobradoParcial : EstadosCobranza.DocumentoGenerado;
            await _repository.SetEstadoDocumentoClienteAsync(idComprobante, await _referencias.IdAsync(estado, ct), ct);
        }
        else if (idTipo == c.Fc || idTipo == c.Com)
        {
            // Ojo: el alta marca FC/COM en DocumentosCliente, pero el ERP los revierte en DocumentosProveedor.
            await _repository.SetEstadoDocumentoProveedorAsync(idComprobante, await _referencias.IdAsync(EstadosCobranza.DocumentoProveedorGenerado, ct), ct);
        }
        else if (idTipo == c.Rec)
        {
            await _repository.SetEstadoReciboAsync(idComprobante, await _referencias.IdAsync(EstadosCobranza.ReciboGenerado, ct), ct);
        }
        else if (idTipo == c.Op)
        {
            await _repository.SetEstadoOrdenPagoAsync(idComprobante, await _referencias.IdAsync(EstadosCobranza.OrdenPagoGenerada, ct), ct);
        }
    }
}
