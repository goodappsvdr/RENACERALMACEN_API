using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Compras;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;
using API.SERVICE.Mappings.Compras;
using API.SERVICE.Mappings.Proveedores;
using API.SERVICE.Models.Compras;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Ventas;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Compras;

/// <summary>Numeración propia (letra X) de los comprobantes de compra que emite la empresa: orden de compra y compra con pago.</summary>
internal static class NumeracionCompra
{
    public const string Letra = "X";

    /// <param name="parametro">Nombre en NUMERACION/* ("OC", "COM").</param>
    public static async Task<(string PuntoVenta, string Numero)> ReservarAsync(
        IReciboCobroRepository comprobantes, IReferenciasRepository referencias, string parametro, int tipo, string puntoVentaPlanilla,
        string? puntoVentaManual, string? numeroManual, string nombre, CancellationToken ct)
    {
        if (await VentaContexto.NumeracionManualAsync(referencias, ct, parametro))
        {
            if (string.IsNullOrWhiteSpace(puntoVentaManual) || string.IsNullOrWhiteSpace(numeroManual))
                throw new BusinessException($"La numeración de {nombre} es manual (NUMERACION/{parametro} = 1): informar punto de venta y número.");
            await comprobantes.ReservarNumeroAsync(puntoVentaManual, Letra, tipo, ct);
            return (puntoVentaManual.PadLeft(4, '0'), numeroManual.PadLeft(8, '0'));
        }

        var numero = await comprobantes.ReservarNumeroAsync(puntoVentaPlanilla, Letra, tipo, ct)
            ?? throw new BusinessException($"No existe el punto de venta {puntoVentaPlanilla} para {nombre} (letra {Letra}).");
        return (puntoVentaPlanilla, numero.ToString().PadLeft(8, '0'));
    }

    public static async Task<NuevaVentaInternaDisplay> IniciarAsync(
        IReciboCobroRepository comprobantes, IReferenciasRepository referencias, int idUsuario, string parametro, int tipo, CancellationToken ct)
    {
        var planilla = await VentaContexto.GetPlanillaAbiertaAsync(comprobantes, referencias, idUsuario, tipo, Letra, ct);
        var manual = await VentaContexto.NumeracionManualAsync(referencias, ct, parametro);
        string? sugerido = null;
        if (!manual)
            sugerido = (await comprobantes.GetProximoNumeroAsync(planilla.PuntoVenta!, Letra, tipo, ct))?.ToString().PadLeft(8, '0');
        return new NuevaVentaInternaDisplay(planilla.IdPlanillaCaja, planilla.PuntoVenta!, Letra, manual, sugerido);
    }
}

public interface IIniciarCompraContadoUseCase
{
    Task<NuevaVentaInternaDisplay> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>Planilla de caja abierta, punto de venta y número sugerido (IniciarPuntoVenta_WS de FrmCompras).</summary>
public sealed class IniciarCompraContadoUseCase : IIniciarCompraContadoUseCase
{
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public IniciarCompraContadoUseCase(IReciboCobroRepository comprobantes, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _comprobantes = comprobantes;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<NuevaVentaInternaDisplay> ExecuteAsync(CancellationToken cancellationToken = default) =>
        await NumeracionCompra.IniciarAsync(_comprobantes, _referencias, CompraContexto.RequireIdUsuario(_currentUser), "COM",
            await _referencias.GetParametroEnteroAsync("COMPROBANTE", "COM", cancellationToken), cancellationToken);
}

public interface ICreateCompraContadoUseCase
{
    Task<CompraContadoResultado> ExecuteAsync(CreateCompraContadoDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Compra con pago en el momento en una transacción (Agregar_Ws + generarOrdenDePago de FrmCompras): comprobante COM, detalle,
/// stock, costo del ítem (si CAMBIAPRECIO = 1), deuda en la cta. cte. del proveedor y, si hay formas de pago, la orden de pago que la
/// imputa (con el mismo writer que el alta de orden de pago).
/// </summary>
public sealed class CreateCompraContadoUseCase : ICreateCompraContadoUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _stock;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IOrdenPagoWriter _ordenPago;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public CreateCompraContadoUseCase(
        ICompraRepository compras,
        IVentaRepository stock,
        IReciboCobroRepository comprobantes,
        IReferenciasRepository referencias,
        IOrdenPagoWriter ordenPago,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser)
    {
        _compras = compras;
        _stock = stock;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _ordenPago = ordenPago;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<CompraContadoResultado> ExecuteAsync(CreateCompraContadoDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = CompraContexto.RequireIdUsuario(_currentUser);
        if (dto.Items.Count == 0)
            throw new BusinessException("Agregue al menos un ítem a la compra.");
        if (dto.Items.Any(i => i.Relacion is { IdDocumentoCliente: > 0 }))
            throw new BusinessException("Las líneas de una compra con pago no se relacionan con remitos ni órdenes de compra.");

        var idProveedor = dto.IdProveedor!.Value;
        var escritura = new CompraEscritura(_compras, _stock, _comprobantes, _referencias);

        var (compra, orden) = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _comprobantes.BloquearEntidadAsync(idProveedor, ct);

            var com = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "COM", ct);
            var proveedor = await _comprobantes.GetEntidadAsync(idProveedor, ct) ?? throw new NotFoundException($"Proveedor {idProveedor} no existe.");
            var sucursal = await _stock.GetSucursalAsync(dto.IdSucursal!.Value, ct) ?? throw new NotFoundException($"Sucursal {dto.IdSucursal} no existe.");
            var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, com, NumeracionCompra.Letra, ct);
            var (puntoVenta, numero) = await NumeracionCompra.ReservarAsync(
                _comprobantes, _referencias, "COM", com, planilla.PuntoVenta!, dto.PuntoVenta, dto.Numero, "compras", ct);

            var preparado = new ComprobanteCompraPreparado(
                proveedor, sucursal, dto.IdCategoriaIva ?? proveedor.IdCategoriaIva ?? 1, NumeracionCompra.Letra, puntoVenta, numero, planilla.IdPlanillaCaja, false);
            var doc = await escritura.GrabarCabeceraAsync(dto, preparado, com, idUsuario, ct);
            var concepto = VentaRules.Concepto("COM", NumeracionCompra.Letra, puntoVenta, numero);
            var ahora = await _clock.GetNowAsync(ct);

            var estadoLibroActivo = await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "ACTIVO", ct);
            var serieDisponible = await _referencias.GetIdEstadoAsync("ITEMSNROSERIE", "DISPONIBLE", ct);
            var actualizaCosto = (await _referencias.GetParametroAsync("CAMBIAPRECIO", "CAMBIAPRECIO", ct))?.Trim() == "1";
            foreach (var item in dto.Items)
            {
                var detalle = await escritura.GrabarDetalleAsync(item, doc, com, estadoLibroActivo, serieDisponible, ct);
                // Como el ERP, la COM no deja saldo de stock pendiente: no se remite ni se factura.
                await escritura.MoverStockAsync(true, item.IdItem!.Value, doc.IdSucursal ?? 0, item.Cantidad, doc.IdDocumentoProveedor, com,
                    (int)detalle.IdDocumentoProveedorDetalle, idUsuario, concepto, item.Descripcion.ToUpperInvariant(), ahora, ct);

                if (actualizaCosto)
                    await _compras.ActualizarCostoItemAsync(item.IdItem.Value, Math.Round(item.PrecioUnitario / (1 + item.IvaAlicuota / 100), 4, MidpointRounding.AwayFromZero), ct);
            }

            // Deuda con el proveedor; el ERP graba el movimiento "a favor".
            await escritura.RegistrarCtaCteAsync(doc, com, concepto, idUsuario, credito: false, ct, movimientoAFavor: true);
            await _compras.SaveChangesAsync(ct);

            Db.ProveedoresRecibos? op = null;
            if (dto.Elementos.Count > 0)
            {
                op = await _ordenPago.GrabarAsync(new CreateOrdenPagoDto
                {
                    IdProveedor = idProveedor,
                    FechaEmision = dto.FechaEmision,
                    IdSucursal = dto.IdSucursal,
                    Observaciones = VentaRules.Truncar(dto.Observaciones, 500),
                    Imputaciones = [new ImputacionPagoDto { IdComprobante = doc.IdDocumentoProveedor, IdComprobanteTipo = com, ImporteComprobante = doc.TotalGeneral ?? 0 }],
                    Elementos = dto.Elementos,
                }, idUsuario, ct);
            }
            return (doc, op);
        }, cancellationToken);

        return new CompraContadoResultado(compra.ToDisplay(), orden?.ToDisplay());
    }
}

public interface IAnularCompraContadoUseCase
{
    Task ExecuteAsync(int idCompra, CancellationToken cancellationToken = default);
}

/// <summary>
/// Anulación de compra con pago (Editar_Ws de FrmCompras): resta el stock, da de baja las líneas, anula la cta. cte. y el comprobante.
/// Solo en GENERADO: si se pagó, primero se anula la orden de pago (que la devuelve a GENERADO).
/// </summary>
public sealed class AnularCompraContadoUseCase : IAnularCompraContadoUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _stock;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public AnularCompraContadoUseCase(
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

    public async Task ExecuteAsync(int idCompra, CancellationToken cancellationToken = default)
    {
        var idUsuario = CompraContexto.RequireIdUsuario(_currentUser);
        var escritura = new CompraEscritura(_compras, _stock, _comprobantes, _referencias);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var com = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "COM", ct);
            var doc = await _compras.GetDocumentoAsync(idCompra, ct) ?? throw new NotFoundException($"Comprobante de proveedor {idCompra} no existe.");
            if (doc.IdComprobanteTipo != com)
                throw new BusinessException($"El comprobante {idCompra} no es una compra con pago.");

            await _comprobantes.BloquearEntidadAsync(doc.IdProveedor ?? 0, ct);

            if (doc.Estado != await CompraContexto.EstadoAsync(_referencias, "GENERADO", ct))
                throw new ConflictException($"La compra {idCompra} no se puede anular en su estado actual: si se pagó, anule primero la orden de pago.");
            var ctaCte = await _compras.GetCtaCteAsync(com, idCompra, ct);
            if (ctaCte is not null && ctaCte.Saldo != ctaCte.Total)
                throw new ConflictException($"La compra {idCompra} tiene pagos imputados: anule primero la orden de pago.");

            var ahora = await _clock.GetNowAsync(ct);
            var concepto = VentaRules.Concepto("COM", doc.Letra ?? string.Empty, doc.PuntoVenta ?? string.Empty, doc.Numero ?? string.Empty);
            foreach (var detalle in await _compras.GetDetallesAsync(idCompra, ct))
                await escritura.MoverStockAsync(false, detalle.IdItem ?? 0, doc.IdSucursal ?? 0, detalle.Cantidad ?? 0, idCompra, com,
                    (int)detalle.IdDocumentoProveedorDetalle, idUsuario, concepto, (detalle.Descripcion ?? string.Empty).ToUpperInvariant(), ahora, ct);
            await _compras.SaveChangesAsync(ct);

            await _compras.AnularDetallesAsync(idCompra, await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "BAJA", ct), ct);
            if (ctaCte is not null)
                await _comprobantes.AnularCtaCteAsync(ctaCte.IdEntidadCtaCte, await _referencias.IdAsync(EstadosCobranza.CtaCteAnulado, ct), ahora, ct);
            await _stock.LiberarNrosSerieAsync(com, idCompra, await _referencias.GetIdEstadoAsync("ITEMSNROSERIE", "NO DISPONIBLE", ct), ct);
            await _compras.AnularDocumentoAsync(idCompra, await CompraContexto.EstadoAsync(_referencias, "ANULADO", ct), ahora, ct);
            return true;
        }, cancellationToken);
    }
}
