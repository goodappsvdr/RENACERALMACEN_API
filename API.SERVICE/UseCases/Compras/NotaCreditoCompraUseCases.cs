using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Compras;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;
using API.SERVICE.Mappings.Compras;
using API.SERVICE.Models.Compras;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Compras;

public interface IIniciarNotaCreditoCompraUseCase
{
    Task<NuevaFacturaCompraDisplay> ExecuteAsync(int idProveedor, int idSucursal, CancellationToken cancellationToken = default);
}

/// <summary>Planilla abierta y letras posibles para la nota de crédito del proveedor (IniciarPuntoVenta_WS + CargarCboLetra_WS).</summary>
public sealed class IniciarNotaCreditoCompraUseCase : IIniciarNotaCreditoCompraUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public IniciarNotaCreditoCompraUseCase(
        ICompraRepository compras, IVentaRepository ventas, IReciboCobroRepository comprobantes, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _compras = compras;
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<NuevaFacturaCompraDisplay> ExecuteAsync(int idProveedor, int idSucursal, CancellationToken cancellationToken = default)
    {
        var idUsuario = CompraContexto.RequireIdUsuario(_currentUser);
        var planilla = await _compras.GetPlanillaAbiertaAsync(idUsuario, await _referencias.IdAsync(EstadosCobranza.PlanillaAbierta, cancellationToken), cancellationToken)
            ?? throw new BusinessException("No se pueden registrar notas de crédito de proveedor: el usuario no tiene una planilla de caja abierta.");
        var proveedor = await _comprobantes.GetEntidadAsync(idProveedor, cancellationToken) ?? throw new NotFoundException($"Proveedor {idProveedor} no existe.");
        var sucursal = await _ventas.GetSucursalAsync(idSucursal, cancellationToken) ?? throw new NotFoundException($"Sucursal {idSucursal} no existe.");
        var ncp = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "NCP", cancellationToken);
        return new NuevaFacturaCompraDisplay(planilla.IdPlanillaCaja,
            await _compras.GetLetrasAsync(ncp, sucursal.IdCategoriaIva ?? 0, proveedor.IdCategoriaIva ?? 1, cancellationToken));
    }
}

public interface ICreateNotaCreditoCompraUseCase
{
    Task<DocumentoProveedorDisplay> ExecuteAsync(CreateNotaCreditoCompraDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alta de nota de crédito de proveedor en una transacción (Agregar_Ws de FrmNotaCreditoProveedor): cabecera, detalle, resta de stock
/// (mercadería devuelta), otros tributos, libro IVA compras como NC (sucursal RI) y saldo a favor en la cta. cte. del proveedor.
/// </summary>
public sealed class CreateNotaCreditoCompraUseCase : ICreateNotaCreditoCompraUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _stock;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public CreateNotaCreditoCompraUseCase(
        ICompraRepository compras,
        IVentaRepository stock,
        IReciboCobroRepository comprobantes,
        IReferenciasRepository referencias,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser)
    {
        _compras = compras;
        _stock = stock;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<DocumentoProveedorDisplay> ExecuteAsync(CreateNotaCreditoCompraDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = CompraContexto.RequireIdUsuario(_currentUser);
        if (dto.Items.Count == 0)
            throw new BusinessException("Agregue al menos un ítem a la nota de crédito.");
        if (dto.Items.Any(i => i.Relacion is { IdDocumentoCliente: > 0 }))
            throw new BusinessException("Las líneas de una nota de crédito de proveedor no se relacionan con remitos ni órdenes de compra.");

        var idProveedor = dto.IdProveedor!.Value;
        var escritura = new CompraEscritura(_compras, _stock, _comprobantes, _referencias);

        var nota = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _comprobantes.BloquearEntidadAsync(idProveedor, ct);

            var ncp = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "NCP", ct);
            var p = await escritura.PrepararAsync(dto, ncp, "nota de crédito", idUsuario, ct);
            var ahora = await _clock.GetNowAsync(ct);
            var concepto = VentaRules.Concepto("NCP", p.Letra, p.PuntoVenta, p.Numero);
            var doc = await escritura.GrabarCabeceraAsync(dto, p, ncp, idUsuario, ct);

            var estadoLibroActivo = await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "ACTIVO", ct);
            var serieNoDisponible = await _referencias.GetIdEstadoAsync("ITEMSNROSERIE", "NO DISPONIBLE", ct); // vuelve al proveedor
            foreach (var item in dto.Items)
            {
                var detalle = await escritura.GrabarDetalleAsync(item, doc, ncp, estadoLibroActivo, serieNoDisponible, ct);
                var idDetalle = (int)detalle.IdDocumentoProveedorDetalle;
                escritura.AgregarStockDetalle(doc, ncp, concepto, item.IdItem!.Value, item.Cantidad, item.Cantidad, item.Cantidad, idDetalle, 0, 0, 0);
                await escritura.MoverStockAsync(false, item.IdItem.Value, doc.IdSucursal ?? 0, item.Cantidad, doc.IdDocumentoProveedor, ncp, idDetalle, idUsuario,
                    concepto, item.Descripcion.ToUpperInvariant(), ahora, ct);
            }

            escritura.GrabarTributos(dto, doc, ncp);
            if (p.SucursalInscripta)
                await escritura.RegistrarLibroIvaAsync(doc, dto, ncp, "NC", ct);
            await escritura.RegistrarCtaCteAsync(doc, ncp, concepto, idUsuario, credito: true, ct);

            await _compras.SaveChangesAsync(ct);
            return doc;
        }, cancellationToken);

        return nota.ToDisplay();
    }
}

public interface IAnularNotaCreditoCompraUseCase
{
    Task ExecuteAsync(int idNotaCredito, CancellationToken cancellationToken = default);
}

/// <summary>
/// Anulación de nota de crédito de proveedor (Editar_Ws de FrmNotaCreditoProveedor): devuelve el stock que había restado, borra libro
/// IVA y tributos, anula la cta. cte., los números de serie y el comprobante. El ERP copiaba la anulación de la factura y volvía a
/// restar el stock (lo descontaba dos veces).
/// </summary>
public sealed class AnularNotaCreditoCompraUseCase : IAnularNotaCreditoCompraUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _stock;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public AnularNotaCreditoCompraUseCase(
        ICompraRepository compras,
        IVentaRepository stock,
        IReciboCobroRepository comprobantes,
        IReferenciasRepository referencias,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser)
    {
        _compras = compras;
        _stock = stock;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task ExecuteAsync(int idNotaCredito, CancellationToken cancellationToken = default)
    {
        var idUsuario = CompraContexto.RequireIdUsuario(_currentUser);
        var escritura = new CompraEscritura(_compras, _stock, _comprobantes, _referencias);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var ncp = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "NCP", ct);
            var doc = await _compras.GetDocumentoAsync(idNotaCredito, ct) ?? throw new NotFoundException($"Comprobante de proveedor {idNotaCredito} no existe.");
            if (doc.IdComprobanteTipo != ncp)
                throw new BusinessException($"El comprobante {idNotaCredito} no es una nota de crédito de proveedor.");

            await _comprobantes.BloquearEntidadAsync(doc.IdProveedor ?? 0, ct);

            var anulables = new[] { await CompraContexto.EstadoAsync(_referencias, "GENERADO", ct), await CompraContexto.EstadoAsync(_referencias, "LIQUIDADO", ct) };
            if (!anulables.Contains(doc.Estado ?? 0))
                throw new ConflictException($"La nota de crédito {idNotaCredito} no se puede anular en su estado actual.");

            // Si ya se usó en una orden de pago su saldo dejó de ser el total a favor.
            var ctaCte = await _compras.GetCtaCteAsync(ncp, idNotaCredito, ct);
            if (ctaCte is not null && ctaCte.Saldo != -ctaCte.Total)
                throw new ConflictException($"La nota de crédito {idNotaCredito} ya se imputó en una orden de pago: anule primero la orden.");

            var ahora = await _clock.GetNowAsync(ct);
            var detalles = (await _compras.GetDetallesAsync(idNotaCredito, ct)).ToDictionary(d => d.IdDocumentoProveedorDetalle);
            var concepto = VentaRules.Concepto("NCP", doc.Letra ?? string.Empty, doc.PuntoVenta ?? string.Empty, doc.Numero ?? string.Empty);

            foreach (var mov in await _stock.GetMovimientosStockAsync(idNotaCredito, ncp, ct))
            {
                var descripcion = (detalles.GetValueOrDefault(mov.IdComprobanteDetalle ?? 0)?.Descripcion ?? string.Empty).ToUpperInvariant();
                await escritura.MoverStockAsync(true, mov.IdItem ?? 0, mov.IdSucursal ?? doc.IdSucursal ?? 0, mov.Total ?? 0m, idNotaCredito, ncp,
                    mov.IdComprobanteDetalle ?? 0, idUsuario, concepto, descripcion, ahora, ct);
            }
            await _compras.SaveChangesAsync(ct);

            await _stock.AnularMovimientosStockAsync(idNotaCredito, ncp, ct);
            await _compras.BorrarLibroIvaAsync(idNotaCredito, ncp, ct);
            await _compras.BorrarOtrosTributosAsync(idNotaCredito, ncp, ct);
            if (ctaCte is not null)
                await _comprobantes.AnularCtaCteAsync(ctaCte.IdEntidadCtaCte, await _referencias.IdAsync(EstadosCobranza.CtaCteAnulado, ct), ahora, ct);
            await _stock.LiberarNrosSerieAsync(ncp, idNotaCredito, await _referencias.GetIdEstadoAsync("ITEMSNROSERIE", "NO DISPONIBLE", ct), ct);
            await _compras.AnularDocumentoAsync(idNotaCredito, await CompraContexto.EstadoAsync(_referencias, "ANULADO", ct), ahora, ct);
            return true;
        }, cancellationToken);
    }
}
