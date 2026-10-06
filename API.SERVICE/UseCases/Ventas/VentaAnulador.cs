using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Ventas;
public interface IVentaAnulador
{
    /// <summary>
    /// Revierte un comprobante de venta dentro de la transacción en curso (no valida estado ni toma locks):
    /// stock o saldos de remitos/presupuestos, ofertas, números de serie, detalle, caja, cheques, bancos,
    /// retenciones, cta. cte., recibo del cobro en el momento y el comprobante.
    /// </summary>
    /// <param name="prefijoConcepto">"VEN" / "FV", para el concepto de los movimientos de stock.</param>
    Task AnularAsync(Db.DocumentosCliente doc, string prefijoConcepto, int idUsuario, DateTime ahora, CancellationToken ct);

    /// <summary>
    /// Si el comprobante facturó remitos/presupuestos, les devuelve el saldo (y el stock de lo presupuestado) y los libera.
    /// Devuelve false si no tenía remitos asociados. Lo usa también la nota de crédito sobre su factura.
    /// </summary>
    Task<bool> RevertirRemitosAsync(Db.DocumentosCliente doc, int idUsuario, DateTime ahora, CancellationToken ct);

    /// <summary>Anula un recibo entero: estado, detalle, caja, cheques, bancos, retenciones y su cta. cte. (AnularRecibo de FrmFacturas).</summary>
    Task AnularReciboAsync(int idRecibo, DateTime ahora, CancellationToken ct);
}

/// <summary>Port de Editar_Ws + AnularRecibo (FrmFacturas). Lo usan la anulación del interno y el rechazo de AFIP.</summary>
public sealed class VentaAnulador : IVentaAnulador
{
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;

    public VentaAnulador(IVentaRepository ventas, IReciboCobroRepository comprobantes, IReferenciasRepository referencias)
    {
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
    }

    public async Task AnularAsync(Db.DocumentosCliente doc, string prefijoConcepto, int idUsuario, DateTime ahora, CancellationToken ct)
    {
        var ven = doc.IdComprobanteTipo ?? 0;
        var idDocumentoCliente = doc.IdDocumentoCliente;

        // 1. Stock: si facturó remitos/presupuestos se devuelve su saldo; si no, se devuelve el stock de cada línea.
        var remitos = await _ventas.GetRemitosAsociadosAsync(idDocumentoCliente, ct);
        if (remitos.Count > 0)
            await RevertirRelacionadosAsync(doc, remitos, ven, idUsuario, ahora, ct);
        else
            await DevolverStockAsync(doc, prefijoConcepto, idUsuario, ahora, ct);

        await _ventas.AnularMovimientosStockAsync(idDocumentoCliente, ven, ct);

        // 2. Caja, cheques, bancos y retenciones registrados contra el comprobante
        await _comprobantes.AnularCajaAsync(ven, idDocumentoCliente, ct);
        await _comprobantes.AnularChequesAsync(idDocumentoCliente, ven, await _referencias.IdAsync(EstadosCobranza.ChequeAnulado, ct), ct);
        await _comprobantes.AnularMovimientosBancoAsync(ven, idDocumentoCliente, await _referencias.IdAsync(EstadosCobranza.MovimientoBancoAnulado, ct), ahora, ct);
        await _comprobantes.AnularRetencionesAsync(ven, idDocumentoCliente, await _referencias.IdAsync(EstadosCobranza.RetencionAnulada, ct), ct);

        // 3. Cta. cte. del comprobante
        var ctaCteAnulada = await _referencias.IdAsync(EstadosCobranza.CtaCteAnulado, ct);
        var idCtaCte = await _comprobantes.GetIdCtaCteAsync(ven, idDocumentoCliente, ct);
        if (idCtaCte is not null)
            await _comprobantes.AnularCtaCteAsync(idCtaCte.Value, ctaCteAnulada, ahora, ct);

        // 4. Recibo cobrado en el momento (AnularRecibo de FrmFacturas)
        var idRecibo = await _ventas.GetReciboImputadoAsync(idDocumentoCliente, ven, ct);
        if (idRecibo is not null)
            await AnularReciboAsync(idRecibo.Value, ctaCteAnulada, ahora, ct);

        // 5. Números de serie y comprobante
        await _ventas.LiberarNrosSerieAsync(ven, idDocumentoCliente, await _referencias.GetIdEstadoAsync("ITEMSNROSERIE", "DISPONIBLE", ct), ct);
        await _ventas.AnularDocumentoAsync(idDocumentoCliente, await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "ANULADO", ct), ahora, ct);
    }

    public async Task<bool> RevertirRemitosAsync(Db.DocumentosCliente doc, int idUsuario, DateTime ahora, CancellationToken ct)
    {
        var remitos = await _ventas.GetRemitosAsociadosAsync(doc.IdDocumentoCliente, ct);
        if (remitos.Count == 0)
            return false;

        await RevertirRelacionadosAsync(doc, remitos, doc.IdComprobanteTipo ?? 0, idUsuario, ahora, ct);
        return true;
    }

    public async Task AnularReciboAsync(int idRecibo, DateTime ahora, CancellationToken ct) =>
        await AnularReciboAsync(idRecibo, await _referencias.IdAsync(EstadosCobranza.CtaCteAnulado, ct), ahora, ct);

    private async Task RevertirRelacionadosAsync(Db.DocumentosCliente doc, List<Db.DocumentosClienteRemitos> remitos, int ven, int idUsuario, DateTime ahora, CancellationToken ct)
    {
        var pv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "PV", ct);

        foreach (var mov in await _ventas.GetMovimientosStockAsync(doc.IdDocumentoCliente, ven, ct))
        {
            var cantidad = mov.Total ?? 0m;
            await _ventas.AjustarSaldoStockAsync(mov.IdComprobanteRelacion ?? 0, mov.IdComprobanteRelacionDetalle ?? 0, mov.IdComprobanteRelacionTipo ?? 0, mov.IdItem ?? 0, cantidad, ct);

            // Lo que vino de un presupuesto había descontado stock: se devuelve.
            if (mov.IdComprobanteRelacionTipo == pv)
            {
                await _ventas.SumarStockAsync(mov.IdItem ?? 0, mov.IdSucursal ?? 0, cantidad, ahora, ct);
                RegistrarMovimiento(mov.IdItem ?? 0, doc.IdDocumentoCliente, mov.IdComprobanteTipo ?? ven, mov.IdComprobanteDetalle ?? 0,
                    idUsuario, doc.IdSucursal ?? 0, mov.Concepto, "DEVOLUCION POR ANULACION", cantidad, ahora);
            }
        }

        foreach (var remito in remitos)
        {
            var idRemito = remito.IdRemito ?? 0;
            var saldo = await _ventas.GetSaldoStockAsync(idRemito, remito.IdComprobanteTipo ?? 0, ct);
            var estado = await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", saldo == 0 ? "GENERADO" : "FACTURADO PARCIAL", ct);
            await _ventas.SetEstadoPendienteAsync(idRemito, estado, pendiente: true, ct);
            await _ventas.BorrarRemitoAsociadoAsync(remito.IdDocumentoClienteRemito, ct);
        }

        await _ventas.SaveChangesAsync(ct);
    }

    private async Task DevolverStockAsync(Db.DocumentosCliente doc, string prefijoConcepto, int idUsuario, DateTime ahora, CancellationToken ct)
    {
        var idSucursal = doc.IdSucursal ?? 0;
        var baja = await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "BAJA", ct);
        var concepto = VentaRules.Concepto(prefijoConcepto, doc.Letra ?? VentaRules.LetraInterna, doc.PuntoVenta ?? string.Empty, doc.Numero ?? string.Empty);

        foreach (var detalle in await _ventas.GetDetallesAsync(doc.IdDocumentoCliente, ct))
        {
            var idItem = detalle.IdItem ?? 0;
            var cantidad = detalle.Cantidad ?? 0m;

            var oferta = await _ventas.GetOfertaActivaAsync(idSucursal, idItem, ct);
            if (oferta?.TipoOferta == VentaRules.OfertaPorAgotamiento)
                await _ventas.AjustarOfertaDisponibleAsync(oferta.IdOferta, cantidad, ct);

            await _ventas.SumarStockAsync(idItem, idSucursal, cantidad, ahora, ct);
            RegistrarMovimiento(idItem, doc.IdDocumentoCliente, doc.IdComprobanteTipo ?? 0, detalle.IdDocumentoClienteDetalle,
                idUsuario, idSucursal, concepto, (detalle.Descripcion ?? string.Empty).ToUpperInvariant(), cantidad, ahora);

            await _ventas.AnularDetalleAsync(detalle.IdDocumentoClienteDetalle, baja, ct);
        }

        await _ventas.SaveChangesAsync(ct);
    }

    /// <summary>Recibo del cobro en el momento: se anula entero (el ERP no revierte su imputación porque la cta. cte. de la venta ya se anuló).</summary>
    private async Task AnularReciboAsync(int idRecibo, int ctaCteAnulada, DateTime ahora, CancellationToken ct)
    {
        var rec = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "REC", ct);

        await _comprobantes.SetEstadoReciboAsync(idRecibo, await _referencias.IdAsync(EstadosCobranza.ReciboAnulado, ct), ct);
        await _comprobantes.AnularDetalleAsync(idRecibo, ahora, ct);
        await _comprobantes.AnularCajaAsync(rec, idRecibo, ct);
        await _comprobantes.AnularChequesAsync(idRecibo, rec, await _referencias.IdAsync(EstadosCobranza.ChequeAnulado, ct), ct);
        await _comprobantes.AnularMovimientosBancoAsync(rec, idRecibo, await _referencias.IdAsync(EstadosCobranza.MovimientoBancoAnulado, ct), ahora, ct);
        await _comprobantes.AnularRetencionesAsync(rec, idRecibo, await _referencias.IdAsync(EstadosCobranza.RetencionAnulada, ct), ct);

        var idCtaCteRecibo = await _comprobantes.GetIdCtaCteAsync(rec, idRecibo, ct);
        if (idCtaCteRecibo is not null)
            await _comprobantes.AnularCtaCteAsync(idCtaCteRecibo.Value, ctaCteAnulada, ahora, ct);
    }

    private void RegistrarMovimiento(int idItem, int idComprobante, int idTipo, long idDetalle, int idUsuario, int idSucursal,
        string? concepto, string item, decimal cantidad, DateTime ahora) =>
        _ventas.Add(new Db.ItemsMovimientosDetalles
        {
            IdItem = idItem,
            IdComprobante = idComprobante,
            IdComprobanteTipo = idTipo,
            IdComprobanteDetalle = (int)idDetalle,
            FechaAlta = ahora,
            IdUsuario = idUsuario,
            IdSucursal = idSucursal,
            Concepto = VentaRules.Truncar(concepto, 100),
            Item = VentaRules.Truncar(item, 500),
            Total = cantidad,
            Debe = cantidad,
            Haber = 0,
            Total2 = cantidad,
            Automatico = true,
        });
}
