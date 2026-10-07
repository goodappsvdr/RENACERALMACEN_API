using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Compras;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;
using API.SERVICE.Mappings.Compras;
using API.SERVICE.Models.Compras;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Ventas;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Compras;

internal static class OrdenCompraContexto
{
    public const string Letra = "X";

    /// <summary>Línea de la orden y su saldo pendiente: Total = Saldo = cantidad, Saldo2 = 0 (no mueve stock, como el ERP).</summary>
    public static async Task GrabarLineasAsync(CompraEscritura escritura, IEnumerable<FacturaCompraItemDto> items, Db.DocumentosProveedor doc, int oc, int estadoLibroActivo, CancellationToken ct)
    {
        var concepto = VentaRules.Concepto("OC", doc.Letra ?? Letra, doc.PuntoVenta ?? string.Empty, doc.Numero ?? string.Empty);
        foreach (var item in items)
        {
            var detalle = await escritura.GrabarDetalleAsync(item, doc, oc, estadoLibroActivo, estadoSerie: 0, ct);
            escritura.AgregarStockDetalle(doc, oc, concepto, item.IdItem!.Value, item.Cantidad, item.Cantidad, 0, (int)detalle.IdDocumentoProveedorDetalle, 0, 0, 0);
        }
    }

    public static void ValidarItems(ComprobanteCompraDtoBase dto)
    {
        if (dto.Items.Count == 0)
            throw new BusinessException("Agregue al menos un ítem a la orden de compra.");
        if (dto.Items.Any(i => i.Relacion is { IdDocumentoCliente: > 0 } || !string.IsNullOrWhiteSpace(i.NroSerie)))
            throw new BusinessException("Las líneas de una orden de compra no llevan comprobante relacionado ni número de serie.");
    }

    /// <summary>Modificable / anulable: GENERADA, sin nada remitido ni facturado (el ERP no validaba al modificar).</summary>
    public static async Task<Db.DocumentosProveedor> GetEditableAsync(
        ICompraRepository compras, IVentaRepository stock, IReferenciasRepository referencias, int idOrden, int oc, CancellationToken ct)
    {
        var doc = await compras.GetDocumentoAsync(idOrden, ct) ?? throw new NotFoundException($"Comprobante de proveedor {idOrden} no existe.");
        if (doc.IdComprobanteTipo != oc)
            throw new BusinessException($"El comprobante {idOrden} no es una orden de compra.");
        if (doc.Estado != await CompraContexto.EstadoAsync(referencias, "GENERADO", ct))
            throw new ConflictException($"La orden de compra {idOrden} no se puede modificar ni anular en su estado actual.");
        if (await stock.GetSaldoStockAsync(idOrden, oc, ct) != 0 || await compras.TieneRelacionesComoOrigenAsync(idOrden, ct))
            throw new ConflictException($"La orden de compra {idOrden} ya tiene mercadería recibida o facturada.");
        return doc;
    }
}

public interface IIniciarOrdenCompraUseCase
{
    Task<NuevaVentaInternaDisplay> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>Planilla de caja abierta, punto de venta y número sugerido (IniciarPuntoVenta_WS de FrmOrdenCompraABM).</summary>
public sealed class IniciarOrdenCompraUseCase : IIniciarOrdenCompraUseCase
{
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public IniciarOrdenCompraUseCase(IReciboCobroRepository comprobantes, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _comprobantes = comprobantes;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<NuevaVentaInternaDisplay> ExecuteAsync(CancellationToken cancellationToken = default) =>
        await NumeracionCompra.IniciarAsync(_comprobantes, _referencias, CompraContexto.RequireIdUsuario(_currentUser), "OC",
            await _referencias.GetParametroEnteroAsync("COMPROBANTE", "OC", cancellationToken), cancellationToken);
}

public interface ICreateOrdenCompraUseCase
{
    Task<DocumentoProveedorDisplay> ExecuteAsync(CreateOrdenCompraDto dto, CancellationToken cancellationToken = default);
}

/// <summary>Alta de orden de compra en una transacción (Agregar_Ws de FrmOrdenCompraABM): cabecera, observación y líneas con saldo pendiente.</summary>
public sealed class CreateOrdenCompraUseCase : ICreateOrdenCompraUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _stock;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public CreateOrdenCompraUseCase(
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

    public async Task<DocumentoProveedorDisplay> ExecuteAsync(CreateOrdenCompraDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = CompraContexto.RequireIdUsuario(_currentUser);
        OrdenCompraContexto.ValidarItems(dto);
        var idProveedor = dto.IdProveedor!.Value;
        var escritura = new CompraEscritura(_compras, _stock, _comprobantes, _referencias);

        var orden = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _comprobantes.BloquearEntidadAsync(idProveedor, ct);

            var oc = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "OC", ct);
            var proveedor = await _comprobantes.GetEntidadAsync(idProveedor, ct) ?? throw new NotFoundException($"Proveedor {idProveedor} no existe.");
            var sucursal = await _stock.GetSucursalAsync(dto.IdSucursal!.Value, ct) ?? throw new NotFoundException($"Sucursal {dto.IdSucursal} no existe.");
            var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, oc, OrdenCompraContexto.Letra, ct);
            // El ERP consultaba NUMERACION/PV (la de presupuestos) para decidir si la numeración de la OC era manual.
            var (puntoVenta, numero) = await NumeracionCompra.ReservarAsync(
                _comprobantes, _referencias, "OC", oc, planilla.PuntoVenta!, dto.PuntoVenta, dto.Numero, "órdenes de compra", ct);

            var preparado = new ComprobanteCompraPreparado(
                proveedor, sucursal, dto.IdCategoriaIva ?? proveedor.IdCategoriaIva ?? 1, OrdenCompraContexto.Letra, puntoVenta, numero, planilla.IdPlanillaCaja, false);
            var doc = await escritura.GrabarCabeceraAsync(dto, preparado, oc, idUsuario, ct);
            await _compras.DeterminarRemitarFacturarAsync(doc.IdDocumentoProveedor, remitar: true, facturar: true, pendiente: true, ct);
            _compras.Add(new Db.ComprobantesCarga { IdComprobante = doc.IdDocumentoProveedor, IdComprobanteTipo = oc, FechaCarga = await _clock.GetNowAsync(ct), IdUsuario = idUsuario });

            await OrdenCompraContexto.GrabarLineasAsync(escritura, dto.Items, doc, oc, await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "ACTIVO", ct), ct);
            await _compras.SaveChangesAsync(ct);
            doc.Remitar = doc.Facturar = doc.Pendiente = true;
            return doc;
        }, cancellationToken);

        return orden.ToDisplay();
    }
}

public interface IUpdateOrdenCompraUseCase
{
    Task<DocumentoProveedorDisplay> ExecuteAsync(int idOrden, CreateOrdenCompraDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Modificación de orden de compra (Modificar_Ws de FrmOrdenCompraABM): proveedor, datos impresos, totales y reemplazo de todas las líneas.
/// Número, fecha y sucursal no cambian.
/// </summary>
public sealed class UpdateOrdenCompraUseCase : IUpdateOrdenCompraUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _stock;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public UpdateOrdenCompraUseCase(
        ICompraRepository compras, IVentaRepository stock, IReciboCobroRepository comprobantes, IReferenciasRepository referencias,
        IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _compras = compras;
        _stock = stock;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<DocumentoProveedorDisplay> ExecuteAsync(int idOrden, CreateOrdenCompraDto dto, CancellationToken cancellationToken = default)
    {
        CompraContexto.RequireIdUsuario(_currentUser);
        OrdenCompraContexto.ValidarItems(dto);
        var idProveedor = dto.IdProveedor!.Value;
        var escritura = new CompraEscritura(_compras, _stock, _comprobantes, _referencias);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var oc = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "OC", ct);
            var actual = await _compras.GetDocumentoAsync(idOrden, ct) ?? throw new NotFoundException($"Comprobante de proveedor {idOrden} no existe.");
            foreach (var id in new[] { actual.IdProveedor ?? 0, idProveedor }.Distinct().Order())
                await _comprobantes.BloquearEntidadAsync(id, ct);

            var doc = await OrdenCompraContexto.GetEditableAsync(_compras, _stock, _referencias, idOrden, oc, ct);
            var proveedor = await _comprobantes.GetEntidadAsync(idProveedor, ct) ?? throw new NotFoundException($"Proveedor {idProveedor} no existe.");

            await _compras.ModificarCabeceraAsync(idOrden, new CabeceraProveedorRow(
                idProveedor,
                VentaRules.Truncar(dto.RazonSocial ?? proveedor.RazonSocial, 50),
                dto.IdCategoriaIva ?? proveedor.IdCategoriaIva,
                VentaRules.Truncar(dto.Cuit ?? proveedor.Cuit, 50),
                dto.IdProvincia ?? proveedor.IdProvincia,
                dto.IdLocalidad ?? proveedor.IdLocalidad,
                VentaRules.Truncar(dto.Calle ?? proveedor.Direccion, 50),
                dto.Neto, dto.Iva, dto.Otros, dto.Total), ct);
            await _compras.BorrarDetallesAsync(idOrden, ct);
            await _stock.BorrarMovimientosStockAsync(idOrden, oc, ct);

            doc.IdProveedor = idProveedor;
            await OrdenCompraContexto.GrabarLineasAsync(escritura, dto.Items, doc, oc, await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "ACTIVO", ct), ct);
            await _compras.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        var actualizado = await _compras.GetDocumentoAsync(idOrden, cancellationToken) ?? throw new NotFoundException($"Comprobante de proveedor {idOrden} no existe.");
        return actualizado.ToDisplay();
    }
}

public interface IAnularOrdenCompraUseCase
{
    Task ExecuteAsync(int idOrden, CancellationToken cancellationToken = default);
}

/// <summary>
/// Anulación de orden de compra (Anular_Ws de FrmOrdenCompraABM): baja de las líneas, saldos en 0 y comprobante ANULADO.
/// El ERP usaba las clases de ventas (DocumentosCliente) con el ID de la orden: anulaba el comprobante de VENTA con ese mismo ID.
/// </summary>
public sealed class AnularOrdenCompraUseCase : IAnularOrdenCompraUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _stock;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public AnularOrdenCompraUseCase(
        ICompraRepository compras, IVentaRepository stock, IReciboCobroRepository comprobantes, IReferenciasRepository referencias,
        IUnitOfWork unitOfWork, IServerClock clock, ICurrentUser currentUser)
    {
        _compras = compras;
        _stock = stock;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task ExecuteAsync(int idOrden, CancellationToken cancellationToken = default)
    {
        CompraContexto.RequireIdUsuario(_currentUser);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var oc = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "OC", ct);
            var previa = await _compras.GetDocumentoAsync(idOrden, ct) ?? throw new NotFoundException($"Comprobante de proveedor {idOrden} no existe.");
            await _comprobantes.BloquearEntidadAsync(previa.IdProveedor ?? 0, ct);
            await OrdenCompraContexto.GetEditableAsync(_compras, _stock, _referencias, idOrden, oc, ct);

            await _compras.AnularDetallesAsync(idOrden, await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "BAJA", ct), ct);
            await _stock.AnularMovimientosStockAsync(idOrden, oc, ct);
            await _compras.AnularDocumentoAsync(idOrden, await CompraContexto.EstadoAsync(_referencias, "ANULADO", ct), await _clock.GetNowAsync(ct), ct);
            return true;
        }, cancellationToken);
    }
}
