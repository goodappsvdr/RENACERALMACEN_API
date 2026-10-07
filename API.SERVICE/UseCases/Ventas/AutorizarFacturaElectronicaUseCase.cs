using API.SERVICE.Domain.Afip;
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

public interface IAutorizarFacturaElectronicaUseCase
{
    /// <param name="reintento">
    /// true cuando la factura ya se intentó autorizar (endpoint de reintento): antes de volver a pedir CAE se verifica
    /// en AFIP que no se haya emitido igual en el intento anterior.
    /// </param>
    Task<AutorizacionAfipResultado> ExecuteAsync(int idDocumentoCliente, bool reintento, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fase 2 de la factura electrónica: pide el CAE para una factura ya grabada (CAE = "0").
/// La llamada a AFIP va fuera de toda transacción, con un lock por factura para que no haya dos pedidos a la vez.
/// <list type="bullet">
/// <item>Aprobada → CAE, número definitivo, código de barras, QR y libro de IVA ventas (si la sucursal es RI).</item>
/// <item>Rechazada → se revierte la factura (stock, cta. cte., cobro) y se informan las observaciones de AFIP (422).</item>
/// <item>AFIP no responde → queda pendiente; se reintenta con <c>reintento = true</c>.</item>
/// </list>
/// </summary>
public sealed class AutorizarFacturaElectronicaUseCase : IAutorizarFacturaElectronicaUseCase
{
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IVentaAnulador _anulador;
    private readonly IReferenciasRepository _referencias;
    private readonly IAfipGateway _afip;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AutorizarFacturaElectronicaUseCase> _logger;

    public AutorizarFacturaElectronicaUseCase(
        IVentaRepository ventas,
        IReciboCobroRepository comprobantes,
        IVentaAnulador anulador,
        IReferenciasRepository referencias,
        IAfipGateway afip,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser,
        ILogger<AutorizarFacturaElectronicaUseCase> logger)
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

    public async Task<AutorizacionAfipResultado> ExecuteAsync(int idDocumentoCliente, bool reintento, CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);
        var fv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "FV", cancellationToken);

        await using var _ = await _ventas.BloquearAutorizacionAsync(idDocumentoCliente, cancellationToken);

        var doc = await _ventas.GetDocumentoAsync(idDocumentoCliente, cancellationToken)
            ?? throw new NotFoundException($"Comprobante {idDocumentoCliente} no existe.");
        if (doc.IdComprobanteTipo != fv)
            throw new BusinessException($"El comprobante {idDocumentoCliente} no es una factura electrónica.");
        if (doc.Estado == await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "ANULADO", cancellationToken))
            throw new ConflictException($"La factura {idDocumentoCliente} está anulada.");
        if (doc.Cae != AfipRules.CaePendiente)
            throw new ConflictException($"La factura {idDocumentoCliente} ya está autorizada (CAE {doc.Cae}).");

        var emisor = await FacturaElectronicaContexto.ResolverAsync(_ventas, _referencias, doc.IdSucursal ?? 0, doc.Letra ?? string.Empty, cancellationToken);

        if (reintento)
            await ComprobanteElectronicoAfip.VerificarQueNoSeEmitioAsync(_afip, doc, emisor, cancellationToken);

        var solicitud = await ArmarSolicitudAsync(doc, emisor, cancellationToken);

        AfipRespuestaCae respuesta;
        try
        {
            respuesta = await _afip.SolicitarCaeAsync(solicitud.Solicitud, cancellationToken);
        }
        catch (AfipNoDisponibleException ex)
        {
            _logger.LogWarning(ex, "Factura {IdDocumento}: AFIP no respondió, queda pendiente de autorización", idDocumentoCliente);
            return new AutorizacionAfipResultado(idDocumentoCliente, EstadoAutorizacionAfip.PendienteAfip, null, doc.Numero,
                "AFIP no respondió. La factura quedó grabada pendiente de autorización: reintentar con POST /api/DocumentoCliente/{id}/autorizar.");
        }

        if (respuesta.Aprobado)
            return await RegistrarAprobacionAsync(doc, emisor, solicitud, respuesta, cancellationToken);

        await RevertirRechazoAsync(doc, idUsuario, respuesta, cancellationToken);
        throw new UnprocessableException(
            $"AFIP rechazó la factura: {respuesta.Observaciones}. Se anuló el comprobante {idDocumentoCliente} y se revirtieron stock, cta. cte. y cobro.");
    }

    private async Task<SolicitudArmada> ArmarSolicitudAsync(Db.DocumentosCliente doc, EmisorFactura emisor, CancellationToken ct)
    {
        var detalles = await _ventas.GetDetallesAsync(doc.IdDocumentoCliente, ct);
        var iva = AfipRules.AgruparIva(detalles.Select(d => (d.IdImpuestoIva ?? 0, d.Neto ?? 0m, d.Iva ?? 0m)), doc.Porcentaje ?? 0m);

        var (docTipoNombre, docNro) = AfipRules.Documento(doc.NroDoc);
        var docTipo = await _referencias.GetParametroEnteroAsync("DOCTIPO", docTipoNombre, ct);
        var concepto = await _referencias.GetParametroEnteroAsync("AFIP", "CONCEPTO", ct);

        var entidad = await _comprobantes.GetEntidadAsync(doc.IdCliente ?? 0, ct);
        var fecha = doc.FechaEmision ?? throw new BusinessException("La factura no tiene fecha de emisión.");
        var total = doc.TotalGeneral ?? 0m;

        var solicitud = emisor.ResponsableInscripto
            ? new AfipSolicitudCae(
                emisor.Afip, emisor.CbteTipo, Monotributo: false, fecha, concepto, docNro, docTipo,
                FechaVtoPago: fecha.AddDays(entidad?.DiasInteres ?? 0),
                ImpNeto: Math.Round((doc.TotalNeto ?? 0m) - iva.NetoExento, 2, MidpointRounding.AwayFromZero),
                ImpTotConc: 0, ImpOpEx: iva.NetoExento, ImpTrib: 0, ImpIva: doc.TotalIva ?? 0m, ImpTotal: total,
                FechaServDesde: fecha, FechaServHasta: fecha, iva.ParaAfip())
            : new AfipSolicitudCae(
                emisor.Afip, emisor.CbteTipo, Monotributo: true, fecha, concepto, docNro, docTipo,
                FechaVtoPago: fecha.AddDays(30), ImpNeto: total, ImpTotConc: 0, ImpOpEx: 0, ImpTrib: 0, ImpIva: 0, ImpTotal: total,
                FechaServDesde: fecha, FechaServHasta: fecha, []);

        return new SolicitudArmada(solicitud, iva, docTipo, docNro);
    }

    private async Task<AutorizacionAfipResultado> RegistrarAprobacionAsync(
        Db.DocumentosCliente doc, EmisorFactura emisor, SolicitudArmada s, AfipRespuestaCae respuesta, CancellationToken cancellationToken)
    {
        var fecha = doc.FechaEmision!.Value;
        var fv = doc.IdComprobanteTipo ?? 0;
        var cae = respuesta.Cae!;
        var numero = respuesta.Numero.ToString().PadLeft(8, '0');
        var puntoVenta = (doc.PuntoVenta ?? string.Empty).PadLeft(4, '0');

        if (numero != doc.Numero)
            _logger.LogInformation("Factura {IdDocumento}: AFIP asignó el número {Numero} (se esperaba {Esperado})", doc.IdDocumentoCliente, numero, doc.Numero);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var codigoBarras = AfipRules.CodigoBarras(emisor.Afip.Cuit, emisor.CbteTipo, emisor.Afip.PuntoVenta, cae, fecha.AddDays(30).ToString("yyyyMMdd"));
            await _ventas.ModificarDatosAfipAsync(doc.IdDocumentoCliente, puntoVenta, numero, cae, codigoBarras, ct);

            await ComprobanteElectronicoAfip.AgregarQrAsync(_ventas, _referencias, doc, emisor, respuesta.Numero, cae, s.DocTipo, s.DocNro, ct);

            if (emisor.ResponsableInscripto)
                ComprobanteElectronicoAfip.RegistrarLibroIva(_ventas, doc, emisor.CbteTipo, puntoVenta, numero, s.Iva, fv);

            await _ventas.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return new AutorizacionAfipResultado(doc.IdDocumentoCliente, EstadoAutorizacionAfip.Autorizada, cae, numero, null);
    }

    /// <summary>AFIP rechazó: la factura no existe fiscalmente, así que se revierte entera y queda anulada con el motivo.</summary>
    private Task RevertirRechazoAsync(Db.DocumentosCliente doc, int idUsuario, AfipRespuestaCae respuesta, CancellationToken cancellationToken) =>
        _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _comprobantes.BloquearEntidadAsync(doc.IdCliente ?? 0, ct);
            await _anulador.AnularAsync(doc, "FV", idUsuario, await _clock.GetNowAsync(ct), ct);
            _ventas.Add(new Db.DocumentosClienteObservaciones
            {
                IdDocumentoCliente = doc.IdDocumentoCliente,
                Observaciones = VentaRules.Truncar($"RECHAZADO POR AFIP: {respuesta.Observaciones}", 5000),
            });
            await _ventas.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    private sealed record SolicitudArmada(AfipSolicitudCae Solicitud, IvaPorAlicuota Iva, int DocTipo, long DocNro);
}
