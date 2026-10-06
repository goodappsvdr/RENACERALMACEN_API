using API.SERVICE.Domain.Afip;
using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Afip;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;
using API.SERVICE.Models.Ventas;
using Microsoft.Extensions.Logging;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Ventas;

public interface IAutorizarNotaCreditoUseCase
{
    Task<AutorizacionAfipResultado> ExecuteAsync(int idNotaCredito, bool reintento, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fase 2 de la nota de crédito: pide el CAE con la factura como comprobante asociado y, si AFIP aprueba, aplica en la
/// misma transacción TODOS los efectos (stock o remitos, cta. cte., recibos de la factura, números de serie).
/// Hasta tener CAE la nota de crédito es un borrador sin efectos: si AFIP la rechaza solo se anula el borrador.
/// (El ERP aplicaba los efectos antes de llamar a AFIP, todo en una transacción abierta durante la llamada.)
/// </summary>
public sealed class AutorizarNotaCreditoUseCase : IAutorizarNotaCreditoUseCase
{
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IVentaAnulador _anulador;
    private readonly IReferenciasRepository _referencias;
    private readonly IAfipGateway _afip;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AutorizarNotaCreditoUseCase> _logger;

    public AutorizarNotaCreditoUseCase(
        IVentaRepository ventas,
        IReciboCobroRepository comprobantes,
        IVentaAnulador anulador,
        IReferenciasRepository referencias,
        IAfipGateway afip,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser,
        ILogger<AutorizarNotaCreditoUseCase> logger)
    {
        _ventas = ventas;
        _comprobantes = comprobantes;
        _anulador = anulador;
        _referencias = referencias;
        _afip = afip;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<AutorizacionAfipResultado> ExecuteAsync(int idNotaCredito, bool reintento, CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);
        var ncTipo = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "NC", cancellationToken);

        await using var _ = await _ventas.BloquearAutorizacionAsync(idNotaCredito, cancellationToken);

        var nc = await _ventas.GetDocumentoAsync(idNotaCredito, cancellationToken)
            ?? throw new NotFoundException($"Comprobante {idNotaCredito} no existe.");
        if (nc.IdComprobanteTipo != ncTipo)
            throw new BusinessException($"El comprobante {idNotaCredito} no es una nota de crédito.");
        if (nc.Estado == await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "ANULADO", cancellationToken))
            throw new ConflictException($"La nota de crédito {idNotaCredito} está anulada.");
        if (nc.Cae != AfipRules.CaePendiente)
            throw new ConflictException($"La nota de crédito {idNotaCredito} ya está autorizada (CAE {nc.Cae}).");

        var idFactura = await _ventas.GetFacturaDeNotaCreditoAsync(idNotaCredito, cancellationToken)
            ?? throw new BusinessException($"La nota de crédito {idNotaCredito} no tiene factura asociada.");
        var factura = await _ventas.GetDocumentoAsync(idFactura, cancellationToken)
            ?? throw new BusinessException($"La factura {idFactura} asociada a la nota de crédito no existe.");

        var emisor = await FacturaElectronicaContexto.ResolverAsync(_ventas, _referencias, nc.IdSucursal ?? 0, nc.Letra ?? string.Empty, cancellationToken, AfipRules.NotaCredito);
        if (reintento)
            await ComprobanteElectronicoAfip.VerificarQueNoSeEmitioAsync(_afip, nc, emisor, cancellationToken);

        var detalles = await _ventas.GetDetallesAsync(idNotaCredito, cancellationToken);
        var iva = AfipRules.AgruparIva(detalles.Select(d => (d.IdImpuestoIva ?? 0, d.Neto ?? 0m, d.Iva ?? 0m)), porcentajeDescuento: 0);
        var (docTipoNombre, docNro) = AfipRules.Documento(nc.NroDoc);
        var docTipo = await _referencias.GetParametroEnteroAsync("DOCTIPO", docTipoNombre, cancellationToken);
        var solicitud = await ArmarSolicitudAsync(nc, factura, emisor, iva, docTipo, docNro, cancellationToken);

        AfipRespuestaCae respuesta;
        try
        {
            respuesta = await _afip.SolicitarCaeAsync(solicitud, cancellationToken);
        }
        catch (AfipNoDisponibleException ex)
        {
            _logger.LogWarning(ex, "Nota de crédito {IdDocumento}: AFIP no respondió, queda pendiente", idNotaCredito);
            return new AutorizacionAfipResultado(idNotaCredito, EstadoAutorizacionAfip.PendienteAfip, null, nc.Numero,
                "AFIP no respondió. La nota de crédito quedó grabada pendiente de autorización (sin efectos sobre la factura): reintentar con POST /api/DocumentoCliente/{id}/autorizar.");
        }

        if (!respuesta.Aprobado)
        {
            await DescartarBorradorAsync(nc, idFactura, respuesta, cancellationToken);
            throw new UnprocessableException($"AFIP rechazó la nota de crédito: {respuesta.Observaciones}. Se anuló el borrador {idNotaCredito}; la factura no se modificó.");
        }

        var cae = respuesta.Cae!;
        var numero = respuesta.Numero.ToString().PadLeft(8, '0');
        var puntoVenta = (nc.PuntoVenta ?? string.Empty).PadLeft(4, '0');

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _comprobantes.BloquearEntidadAsync(nc.IdCliente ?? 0, ct);

            // El ERP arma el código de barras de la NC con la fecha de emisión (en la factura usa fecha + 30 días).
            var fecha = nc.FechaEmision!.Value;
            var codigoBarras = AfipRules.CodigoBarras(emisor.Afip.Cuit, emisor.CbteTipo, emisor.Afip.PuntoVenta, cae, fecha.ToString("yyyyMMdd"));
            await _ventas.ModificarDatosAfipAsync(idNotaCredito, puntoVenta, numero, cae, codigoBarras, ct);
            await ComprobanteElectronicoAfip.AgregarQrAsync(_ventas, _referencias, nc, emisor, respuesta.Numero, cae, docTipo, docNro, ct);
            if (emisor.ResponsableInscripto)
                ComprobanteElectronicoAfip.RegistrarLibroIva(_ventas, nc, emisor.CbteTipo, puntoVenta, numero, iva, ncTipo);

            await AplicarEfectosAsync(nc, factura, detalles, ncTipo, puntoVenta, numero, idUsuario, ct);

            await _ventas.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return new AutorizacionAfipResultado(idNotaCredito, EstadoAutorizacionAfip.Autorizada, cae, numero, null);
    }

    private async Task<AfipSolicitudCae> ArmarSolicitudAsync(
        Db.DocumentosCliente nc, Db.DocumentosCliente factura, EmisorFactura emisor, IvaPorAlicuota iva, int docTipo, long docNro, CancellationToken ct)
    {
        var concepto = await _referencias.GetParametroEnteroAsync("AFIP", "CONCEPTO", ct);
        var fecha = nc.FechaEmision ?? throw new BusinessException("La nota de crédito no tiene fecha de emisión.");
        var total = nc.TotalGeneral ?? 0m;
        var asociado = new AfipComprobanteAsociado(
            AfipRules.TipoAfipFactura(factura.Letra),
            factura.IdPuntoVenta ?? 0,
            long.TryParse(factura.Numero, out var nro) ? nro : 0);

        var solicitud = emisor.ResponsableInscripto
            ? new AfipSolicitudCae(
                emisor.Afip, emisor.CbteTipo, Monotributo: false, fecha, concepto, docNro, docTipo, FechaVtoPago: fecha.AddDays(30),
                ImpNeto: Math.Round((nc.TotalNeto ?? 0m) - iva.NetoExento, 2, MidpointRounding.AwayFromZero),
                ImpTotConc: 0, ImpOpEx: iva.NetoExento, ImpTrib: 0, ImpIva: nc.TotalIva ?? 0m, ImpTotal: total,
                FechaServDesde: fecha, FechaServHasta: fecha, iva.ParaAfip())
            : new AfipSolicitudCae(
                emisor.Afip, emisor.CbteTipo, Monotributo: true, fecha, concepto, docNro, docTipo, FechaVtoPago: fecha.AddDays(30),
                ImpNeto: total, ImpTotConc: 0, ImpOpEx: 0, ImpTrib: 0, ImpIva: 0, ImpTotal: total,
                FechaServDesde: fecha, FechaServHasta: fecha, []);

        return solicitud with { ComprobantesAsociados = [asociado] };
    }

    /// <summary>Efectos de la nota de crédito sobre stock, cta. cte. y la factura (Agregar_Ws de FrmNotasCreditoAFIP).</summary>
    private async Task AplicarEfectosAsync(
        Db.DocumentosCliente nc, Db.DocumentosCliente factura, List<Db.DocumentosClienteDetalle> detalles,
        int ncTipo, string puntoVenta, string numero, int idUsuario, CancellationToken ct)
    {
        var fecha = nc.FechaEmision!.Value;
        var idCliente = nc.IdCliente ?? 0;
        var idSucursal = nc.IdSucursal ?? 0;
        var fvTipo = factura.IdComprobanteTipo ?? 0;
        var concepto = VentaRules.Concepto("NC", nc.Letra ?? string.Empty, puntoVenta, numero);

        // 1. Si la factura vino de remitos/presupuestos se les devuelve el saldo; si no, se devuelve el stock de cada línea.
        var conRemitos = await _anulador.RevertirRemitosAsync(factura, idUsuario, fecha, ct);
        var lineasFactura = await _ventas.GetDetallesAsync(factura.IdDocumentoCliente, ct);
        var usadas = new HashSet<long>();

        foreach (var det in detalles)
        {
            var idItem = det.IdItem ?? 0;
            var cantidad = det.Cantidad ?? 0m;
            var lineaFactura = lineasFactura.FirstOrDefault(l => l.IdItem == idItem && usadas.Add(l.IdDocumentoClienteDetalle));
            var idLineaFactura = (int)(lineaFactura?.IdDocumentoClienteDetalle ?? 0);

            if (!conRemitos)
            {
                await _ventas.SumarStockAsync(idItem, idSucursal, cantidad, fecha, ct);
                _ventas.Add(new Db.ItemsMovimientosDetalles
                {
                    IdItem = idItem,
                    IdComprobante = nc.IdDocumentoCliente,
                    IdComprobanteTipo = ncTipo,
                    IdComprobanteDetalle = (int)det.IdDocumentoClienteDetalle,
                    FechaAlta = fecha,
                    IdUsuario = idUsuario,
                    IdSucursal = idSucursal,
                    Concepto = VentaRules.Truncar(concepto, 100),
                    Item = VentaRules.Truncar(det.Descripcion, 500),
                    Total = cantidad,
                    Debe = cantidad,
                    Haber = 0,
                    Total2 = cantidad,
                    Automatico = true,
                });
                if (idLineaFactura > 0)
                    await _ventas.AjustarSaldoStockAsync(factura.IdDocumentoCliente, idLineaFactura, fvTipo, idItem, -cantidad, ct);
            }

            _ventas.Add(new Db.EntidadesCtaCteStockMovimientosDetalle
            {
                IdEntidad = idCliente,
                IdComprobante = nc.IdDocumentoCliente,
                IdComprobanteTipo = ncTipo,
                Concepto = VentaRules.Truncar(concepto, 50),
                IdItem = idItem,
                Total = cantidad,
                Saldo = 0,
                Saldo2 = conRemitos ? cantidad : -cantidad,
                Fecha = fecha,
                IdSucursal = idSucursal,
                IdComprobanteDetalle = (int)det.IdDocumentoClienteDetalle,
                IdComprobanteRelacion = factura.IdDocumentoCliente,
                IdComprobanteRelacionTipo = fvTipo,
                IdComprobanteRelacionDetalle = idLineaFactura,
            });
        }

        // 2. Cta. cte.: saldo a favor del cliente
        var total = nc.TotalGeneral ?? 0m;
        var ctaCte = new Db.EntidadesCtaCte
        {
            IdEntidad = idCliente,
            IdComprobanteTipo = ncTipo,
            IdComprobante = nc.IdDocumentoCliente,
            Concepto = concepto,
            NroCuota = 1,
            Total = total,
            Saldo = -total,
            Cancelado = false,
            Fecha = fecha,
            FechaVencimiento = fecha.AddDays(30),
            FechaAnulacion = fecha,
            FechaPago = fecha,
            InteresAplicado = 0,
            Estado = await _referencias.IdAsync(EstadosCobranza.CtaCteGenerado, ct),
            Total2 = -total,
            IdEmpresa = 1,
            IdSucursal = idSucursal,
            IdUsuario = idUsuario,
        };
        _ventas.Add(ctaCte);
        await _ventas.SaveChangesAsync(ct);

        _ventas.Add(new Db.EntidadesCtaCteMovimientos
        {
            IdEntidadCtaCte = ctaCte.IdEntidadCtaCte,
            Concepto = concepto,
            AfavorEntidad = total,
            EnContraEntidad = 0,
            Fecha = fecha,
            IdElementoCobroPago = await _referencias.GetParametroEnteroAsync("ELEMENTO", "CTACTE", ct),
            IdElemento = 1,
            IdComprobanteTipo = ncTipo,
            IdComprobante = nc.IdDocumentoCliente,
        });

        // 3. La factura: si se cobró con recibos, el ERP los anula; si no, la marca cancelada.
        var recibos = await _ventas.GetRecibosImputadosAsync(factura.IdDocumentoCliente, fvTipo, ct);
        var ahora = await _clock.GetNowAsync(ct);
        if (recibos.Count > 0)
        {
            foreach (var idRecibo in recibos)
                await _anulador.AnularReciboAsync(idRecibo, ahora, ct);
        }
        else
        {
            await _comprobantes.SetEstadoDocumentoClienteAsync(factura.IdDocumentoCliente, await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "CANCELADO", ct), ct);
        }

        // 4. Números de serie vendidos con la factura
        await _ventas.LiberarNrosSerieAsync(fvTipo, factura.IdDocumentoCliente, await _referencias.GetIdEstadoAsync("ITEMSNROSERIE", "DISPONIBLE", ct), ct);
    }

    /// <summary>AFIP rechazó: el borrador no tuvo efectos, así que alcanza con anularlo y desvincularlo de la factura.</summary>
    private Task DescartarBorradorAsync(Db.DocumentosCliente nc, int idFactura, AfipRespuestaCae respuesta, CancellationToken cancellationToken) =>
        _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var baja = await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "BAJA", ct);
            foreach (var det in await _ventas.GetDetallesAsync(nc.IdDocumentoCliente, ct))
                await _ventas.AnularDetalleAsync(det.IdDocumentoClienteDetalle, baja, ct);

            await _ventas.BorrarRelacionAsync(idFactura, nc.IdDocumentoCliente, ct);
            await _ventas.AnularDocumentoAsync(nc.IdDocumentoCliente, await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "ANULADO", ct), await _clock.GetNowAsync(ct), ct);
            _ventas.Add(new Db.DocumentosClienteObservaciones
            {
                IdDocumentoCliente = nc.IdDocumentoCliente,
                Observaciones = VentaRules.Truncar($"RECHAZADO POR AFIP: {respuesta.Observaciones}", 5000),
            });
            await _ventas.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
}
