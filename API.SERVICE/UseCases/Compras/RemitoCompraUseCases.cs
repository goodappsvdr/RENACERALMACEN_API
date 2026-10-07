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

public interface ICreateRemitoCompraUseCase
{
    Task<DocumentoProveedorDisplay> ExecuteAsync(CreateRemitoCompraDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alta de remito de compra en una transacción (Agregar_Ws de FrmRemitosCompra). Por línea, como el ERP:
/// directa → suma stock y queda pendiente de facturar; de una orden de compra → consume su saldo, suma stock y queda pendiente
/// de facturar; de una factura de compra → solo consume su saldo (el stock lo sumó la factura).
/// </summary>
public sealed class CreateRemitoCompraUseCase : ICreateRemitoCompraUseCase
{
    private const string Letra = "R";

    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _stock;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public CreateRemitoCompraUseCase(
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

    public async Task<DocumentoProveedorDisplay> ExecuteAsync(CreateRemitoCompraDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = CompraContexto.RequireIdUsuario(_currentUser);
        if (dto.Items.Count == 0)
            throw new BusinessException("Agregue al menos un ítem al remito de compra.");

        var idProveedor = dto.IdProveedor!.Value;
        var escritura = new CompraEscritura(_compras, _stock, _comprobantes, _referencias);

        var remito = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _comprobantes.BloquearEntidadAsync(idProveedor, ct);

            var t = await CompraContexto.TiposAsync(_referencias, ct);
            var p = await escritura.PrepararAsync(dto, Letra, t.Rc, "remito", idUsuario, ct, validarLetra: false);
            var origenes = await ValidarOrigenesAsync(dto, idProveedor, t, ct);

            var ahora = await _clock.GetNowAsync(ct);
            var concepto = VentaRules.Concepto("RC", Letra, p.PuntoVenta, p.Numero);
            var doc = await escritura.GrabarCabeceraAsync(dto, p, t.Rc, idUsuario, ct, string.IsNullOrWhiteSpace(dto.Cae) ? null : dto.Cae.Trim(), dto.FechaVencimiento);
            _compras.Add(new Db.ComprobantesCarga { IdComprobante = doc.IdDocumentoProveedor, IdComprobanteTipo = t.Rc, FechaCarga = ahora, IdUsuario = idUsuario });

            var estadoLibroActivo = await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "ACTIVO", ct);
            var serieDisponible = await _referencias.GetIdEstadoAsync("ITEMSNROSERIE", "DISPONIBLE", ct);
            foreach (var item in dto.Items)
                await GrabarItemAsync(escritura, item, doc, origenes, t, idUsuario, concepto, ahora, estadoLibroActivo, serieDisponible, ct);

            await ActualizarOrigenesAsync(doc.IdDocumentoProveedor, origenes.Values, t, ct);
            await _compras.SaveChangesAsync(ct);
            return doc;
        }, cancellationToken);

        return remito.ToDisplay();
    }

    /// <summary>Facturas / órdenes de compra del proveedor, no anuladas; cada línea del mismo ítem y sin superar su saldo. El ERP no validaba nada.</summary>
    private async Task<Dictionary<int, Db.DocumentosProveedor>> ValidarOrigenesAsync(CreateRemitoCompraDto dto, int idProveedor, CompraContexto.Tipos t, CancellationToken ct)
    {
        var anulado = await CompraContexto.EstadoAsync(_referencias, "ANULADO", ct);
        var origenes = new Dictionary<int, Db.DocumentosProveedor>();
        foreach (var id in dto.Comprobantes.Select(c => c.IdDocumentoCliente!.Value).Distinct())
        {
            var origen = await _compras.GetDocumentoAsync(id, ct) ?? throw new NotFoundException($"Comprobante de proveedor {id} no existe.");
            if (origen.IdProveedor != idProveedor)
                throw new BusinessException($"El comprobante {id} no es del proveedor {idProveedor}.");
            if (origen.IdComprobanteTipo != t.Fc && origen.IdComprobanteTipo != t.Oc)
                throw new BusinessException($"El comprobante {id} no es una factura ni una orden de compra.");
            if (origen.Estado == anulado)
                throw new ConflictException($"El comprobante {id} está anulado.");
            origenes[id] = origen;
        }

        var lineasPorOrigen = new Dictionary<int, List<LineaPendienteCompraRow>>();
        foreach (var grupo in dto.Items.Where(i => i.Relacion is { IdDocumentoCliente: > 0 })
                     .GroupBy(i => (Doc: i.Relacion!.IdDocumentoCliente!.Value, Det: i.Relacion.IdDocumentoClienteDetalle!.Value)))
        {
            if (!origenes.ContainsKey(grupo.Key.Doc))
                throw new BusinessException($"La línea relacionada al comprobante {grupo.Key.Doc} requiere incluirlo en Comprobantes.");
            if (!lineasPorOrigen.TryGetValue(grupo.Key.Doc, out var lineas))
                lineasPorOrigen[grupo.Key.Doc] = lineas = await _compras.GetLineasPendientesAsync(grupo.Key.Doc, t.ConStockPendiente, ct);

            var linea = lineas.FirstOrDefault(l => l.Detalle.IdDocumentoProveedorDetalle == grupo.Key.Det)
                ?? throw new BusinessException($"La línea {grupo.Key.Det} del comprobante {grupo.Key.Doc} no tiene saldo pendiente.");
            if (grupo.Any(i => i.IdItem != linea.Detalle.IdItem))
                throw new BusinessException($"La línea {grupo.Key.Det} del comprobante {grupo.Key.Doc} es de otro ítem.");
            var cantidad = grupo.Sum(i => i.Cantidad);
            if (cantidad > linea.Saldo)
                throw new BusinessException($"Se remiten {cantidad} de {linea.Detalle.Descripcion} y el pendiente es {linea.Saldo}.");
        }

        var sinLineas = origenes.Keys.Except(lineasPorOrigen.Keys).ToList();
        if (sinLineas.Count > 0)
            throw new BusinessException($"El remito no incluye ninguna línea de los comprobantes {string.Join(", ", sinLineas)}.");
        return origenes;
    }

    private async Task GrabarItemAsync(
        CompraEscritura escritura, FacturaCompraItemDto item, Db.DocumentosProveedor doc, IReadOnlyDictionary<int, Db.DocumentosProveedor> origenes,
        CompraContexto.Tipos t, int idUsuario, string concepto, DateTime ahora, int estadoLibroActivo, int serieDisponible, CancellationToken ct)
    {
        var idItem = item.IdItem!.Value;
        var descripcion = item.Descripcion.ToUpperInvariant();
        var cantidad = item.Cantidad;
        var idSucursal = doc.IdSucursal ?? 0;
        var detalle = await escritura.GrabarDetalleAsync(item, doc, t.Rc, estadoLibroActivo, serieDisponible, ct);
        var idDetalle = (int)detalle.IdDocumentoProveedorDetalle;

        var relacion = item.Relacion is { IdDocumentoCliente: > 0 } r ? r : null;
        if (relacion is null)
        {
            escritura.AgregarStockDetalle(doc, t.Rc, concepto, idItem, cantidad, cantidad, cantidad, idDetalle, 0, 0, 0);
            await escritura.MoverStockAsync(true, idItem, idSucursal, cantidad, doc.IdDocumentoProveedor, t.Rc, idDetalle, idUsuario, concepto, descripcion, ahora, ct);
            return;
        }

        var idRel = relacion.IdDocumentoCliente!.Value;
        var tipoRel = origenes[idRel].IdComprobanteTipo ?? 0;
        var detRel = relacion.IdDocumentoClienteDetalle!.Value;
        await _stock.AjustarSaldoStockAsync(idRel, detRel, tipoRel, idItem, -cantidad, ct);

        if (tipoRel != t.Oc)
        {
            // Entrega de una factura: el stock ya ingresó con la factura y no queda nada por facturar.
            escritura.AgregarStockDetalle(doc, t.Rc, concepto, idItem, cantidad, 0, -cantidad, idDetalle, idRel, tipoRel, detRel);
            return;
        }

        escritura.AgregarStockDetalle(doc, t.Rc, concepto, idItem, cantidad, cantidad, cantidad, idDetalle, idRel, tipoRel, detRel);
        await escritura.MoverStockAsync(true, idItem, idSucursal, cantidad, doc.IdDocumentoProveedor, t.Rc, idDetalle, idUsuario, concepto, descripcion, ahora, ct);
    }

    /// <summary>
    /// Relación y estado de cada comprobante entregado. Una orden de compra pasa a ENTREGADO / ENTREGADO PARCIAL como en el ERP; a una
    /// factura solo se le actualiza el pendiente (el ERP le pisaba el estado, que en la factura refleja el pago).
    /// El remito queda para facturar salvo que venga de una factura.
    /// </summary>
    private async Task ActualizarOrigenesAsync(int idRemito, IEnumerable<Db.DocumentosProveedor> origenes, CompraContexto.Tipos t, CancellationToken ct)
    {
        var lista = origenes.ToList();
        if (lista.Count == 0)
        {
            await _compras.DeterminarRemitarFacturarAsync(idRemito, remitar: false, facturar: true, pendiente: true, ct);
            return;
        }

        await _compras.SaveChangesAsync(ct);
        foreach (var origen in lista)
        {
            var pendiente = (await _compras.GetLineasPendientesAsync(origen.IdDocumentoProveedor, t.ConStockPendiente, ct)).Count > 0;
            _compras.Add(new Db.DocumentosProveedorRemitos { IdDocumentoProveedor = origen.IdDocumentoProveedor, IdRemito = idRemito, IdComprobanteTipo = origen.IdComprobanteTipo });

            if (origen.IdComprobanteTipo == t.Oc)
            {
                var estado = await CompraContexto.EstadoAsync(_referencias, pendiente ? "ENTREGADO PARCIAL" : "ENTREGADO", ct);
                await _compras.SetEstadoPendienteAsync(origen.IdDocumentoProveedor, estado, pendiente, ct);
                await _compras.DeterminarRemitarFacturarAsync(idRemito, remitar: false, facturar: true, pendiente: true, ct);
            }
            else
            {
                await _compras.SetPendienteAsync(origen.IdDocumentoProveedor, pendiente, ct);
            }
        }
    }
}

public interface IAnularRemitoCompraUseCase
{
    Task ExecuteAsync(int idRemito, CancellationToken cancellationToken = default);
}

/// <summary>
/// Anulación de remito de compra (Editar_Ws de FrmRemitosCompra): resta el stock que había sumado (líneas directas y de orden de compra),
/// devuelve el saldo a facturas / órdenes entregadas, anula movimientos, números de serie y el comprobante. Un remito ya facturado no se anula.
/// </summary>
public sealed class AnularRemitoCompraUseCase : IAnularRemitoCompraUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _stock;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public AnularRemitoCompraUseCase(
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

    public async Task ExecuteAsync(int idRemito, CancellationToken cancellationToken = default)
    {
        var idUsuario = CompraContexto.RequireIdUsuario(_currentUser);
        var escritura = new CompraEscritura(_compras, _stock, _comprobantes, _referencias);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var t = await CompraContexto.TiposAsync(_referencias, ct);
            var doc = await _compras.GetDocumentoAsync(idRemito, ct) ?? throw new NotFoundException($"Comprobante de proveedor {idRemito} no existe.");
            if (doc.IdComprobanteTipo != t.Rc)
                throw new BusinessException($"El comprobante {idRemito} no es un remito de compra.");

            await _comprobantes.BloquearEntidadAsync(doc.IdProveedor ?? 0, ct);

            if (doc.Estado != await CompraContexto.EstadoAsync(_referencias, "GENERADO", ct))
                throw new ConflictException($"El remito de compra {idRemito} no se puede anular en su estado actual.");
            if (await _compras.TieneRelacionesComoOrigenAsync(idRemito, ct))
                throw new ConflictException($"El remito de compra {idRemito} está facturado: anule primero la factura.");

            var ahora = await _clock.GetNowAsync(ct);
            var detalles = (await _compras.GetDetallesAsync(idRemito, ct)).ToDictionary(d => d.IdDocumentoProveedorDetalle);
            var concepto = VentaRules.Concepto("RC", doc.Letra ?? string.Empty, doc.PuntoVenta ?? string.Empty, doc.Numero ?? string.Empty);

            foreach (var mov in await _stock.GetMovimientosStockAsync(idRemito, t.Rc, ct))
            {
                var cantidad = mov.Total ?? 0m;
                var tipoRel = mov.IdComprobanteRelacionTipo ?? 0;
                var directa = (mov.IdComprobanteRelacion ?? 0) == 0;
                if (!directa)
                    await _stock.AjustarSaldoStockAsync(mov.IdComprobanteRelacion ?? 0, mov.IdComprobanteRelacionDetalle ?? 0, tipoRel, mov.IdItem ?? 0, cantidad, ct);

                // Resta el stock que el remito había sumado (el ERP no lo restaba en las líneas directas de un remito con relaciones).
                if (directa || tipoRel == t.Oc)
                {
                    var item = directa
                        ? (detalles.GetValueOrDefault(mov.IdComprobanteDetalle ?? 0)?.Descripcion ?? string.Empty).ToUpperInvariant()
                        : "DEVOLUCION POR ANULACION";
                    await escritura.MoverStockAsync(false, mov.IdItem ?? 0, mov.IdSucursal ?? doc.IdSucursal ?? 0, cantidad, idRemito, t.Rc,
                        mov.IdComprobanteDetalle ?? 0, idUsuario, directa ? concepto : mov.Concepto ?? concepto, item, ahora, ct);
                }
            }
            await _compras.SaveChangesAsync(ct);

            foreach (var relacion in await _compras.GetRelacionesComoDestinoAsync(idRemito, ct))
            {
                var idOrigen = relacion.IdDocumentoProveedor ?? 0;
                if (relacion.IdComprobanteTipo == t.Oc)
                {
                    var consumido = await _stock.GetSaldoStockAsync(idOrigen, t.Oc, ct);
                    var estado = await CompraContexto.EstadoAsync(_referencias, consumido == 0 ? "GENERADO" : "ENTREGADO PARCIAL", ct);
                    await _compras.SetEstadoPendienteAsync(idOrigen, estado, pendiente: true, ct);
                }
                else
                {
                    await _compras.SetPendienteAsync(idOrigen, true, ct);
                }
                await _compras.BorrarRelacionAsync(relacion.IdDocumentoProveedorRemito, ct);
            }

            await _stock.AnularMovimientosStockAsync(idRemito, t.Rc, ct);
            await _stock.LiberarNrosSerieAsync(t.Rc, idRemito, await _referencias.GetIdEstadoAsync("ITEMSNROSERIE", "NO DISPONIBLE", ct), ct);
            await _compras.AnularDocumentoAsync(idRemito, await CompraContexto.EstadoAsync(_referencias, "ANULADO", ct), ahora, ct);
            return true;
        }, cancellationToken);
    }
}

public interface IGetComprobantesCompraParaRemitirUseCase
{
    Task<IReadOnlyList<ComprobanteCompraPendienteDisplay>> ExecuteAsync(int idProveedor, CancellationToken cancellationToken = default);
}

/// <summary>
/// Facturas y órdenes de compra del proveedor con mercadería pendiente de recibir (BuscarComprobantes_WS de FrmRemitosCompra). Sin rol
/// CEO / CTO, solo de la sucursal del usuario.
/// </summary>
public sealed class GetComprobantesCompraParaRemitirUseCase : IGetComprobantesCompraParaRemitirUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public GetComprobantesCompraParaRemitirUseCase(ICompraRepository compras, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _compras = compras;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ComprobanteCompraPendienteDisplay>> ExecuteAsync(int idProveedor, CancellationToken cancellationToken = default)
    {
        CompraContexto.RequireIdUsuario(_currentUser);
        var t = await CompraContexto.TiposAsync(_referencias, cancellationToken);
        var excluidos = new[] { await CompraContexto.EstadoAsync(_referencias, "ANULADO", cancellationToken), await CompraContexto.EstadoAsync(_referencias, "CANCELADO", cancellationToken) };
        var sucursal = CompraContexto.EsGlobal(_currentUser) ? null : _currentUser.IdSucursal;

        return (await _compras.GetComprobantesConPendienteAsync(idProveedor, [t.Fc, t.Oc], excluidos, sucursal, cancellationToken))
            .Select(d => new ComprobanteCompraPendienteDisplay(
                d.IdDocumentoProveedor, d.IdComprobanteTipo ?? 0, $"{d.Letra}-{d.PuntoVenta}-{d.Numero}", d.RazonSocial, d.FechaEmision, d.Estado, d.TotalGeneral))
            .ToList();
    }
}
