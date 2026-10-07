using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Bancos;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Compras;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Mappings.Bancos;
using API.SERVICE.Models.Bancos;
using API.SERVICE.Models.Compras;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Compras;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Bancos;

internal static class OrdenBancariaContexto
{
    public const int MovimientoDeposito = 1;
    public const int MovimientoDepositoCheque = 5;
    public const int MovimientoExtraccion = 6;

    public static int RequireIdUsuario(ICurrentUser user) =>
        user.IdUsuario ?? throw new ForbiddenException("El usuario no tiene un registro en Usuarios del ERP; no puede operar cuentas bancarias.");

    public static async Task<Db.BancosCuentas> GetCuentaActivaAsync(IOrdenBancariaRepository ordenes, IReferenciasRepository referencias, int idBancoCuenta, CancellationToken ct)
    {
        var cuenta = await ordenes.GetCuentaAsync(idBancoCuenta, ct) ?? throw new NotFoundException($"Cuenta bancaria {idBancoCuenta} no existe.");
        if (cuenta.Estado != await referencias.GetIdEstadoAsync("BANCOSCUENTAS", "ACTIVA", ct))
            throw new BusinessException($"La cuenta bancaria {idBancoCuenta} no está activa.");
        return cuenta;
    }

    public static Db.BancosCuentasMovimientos Movimiento(
        Db.BancosCuentas cuenta, int tipoMovimiento, bool ingreso, decimal importe, DateTime fecha, int tipo, int idComprobante, int estado,
        int bancoOrigen = 0, int sucursalOrigen = 0, int tipoOrigen = 0, string? nroOrigen = null,
        int bancoDestino = 0, int sucursalDestino = 0, int tipoDestino = 0, string? nroDestino = null) => new()
    {
        IdBancoCuenta = cuenta.IdBancoCuenta,
        IdMovimientoTipo = tipoMovimiento,
        Debe = ingreso ? importe : 0,
        Haber = ingreso ? 0 : importe,
        Importe = importe,
        Fecha = fecha,
        IdBancoOrigen = bancoOrigen,
        IdBancoSucursalOrigen = sucursalOrigen,
        CuentaTipoOrigen = tipoOrigen,
        NroCuentaOrigen = nroOrigen ?? string.Empty,
        IdBancoDestino = bancoDestino,
        IdBancoSucursalDestino = sucursalDestino,
        CuentaTipoDestino = tipoDestino,
        NroCuentaDestino = nroDestino ?? string.Empty,
        IdComprobanteTipo = tipo,
        IdComprobante = idComprobante,
        Estado = estado,
        Total = ingreso ? importe : -importe,
    };
}

public interface IIniciarOrdenBancariaUseCase
{
    /// <param name="clase">"OD" (depósito) u "OE" (extracción).</param>
    Task<NuevaVentaInternaDisplay> ExecuteAsync(string clase, CancellationToken cancellationToken = default);
}

/// <summary>Planilla de caja abierta, punto de venta y número sugerido (IniciarPuntoVenta_WS de FrmOrdendeDeposito / FrmOrdendeExtraccion).</summary>
public sealed class IniciarOrdenBancariaUseCase : IIniciarOrdenBancariaUseCase
{
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public IniciarOrdenBancariaUseCase(IReciboCobroRepository comprobantes, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _comprobantes = comprobantes;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<NuevaVentaInternaDisplay> ExecuteAsync(string clase, CancellationToken cancellationToken = default) =>
        await NumeracionCompra.IniciarAsync(_comprobantes, _referencias, OrdenBancariaContexto.RequireIdUsuario(_currentUser), clase,
            await _referencias.GetParametroEnteroAsync("COMPROBANTE", clase, cancellationToken), cancellationToken);
}

public interface ICreateOrdenDepositoUseCase
{
    Task<OrdenDepositoDisplay> ExecuteAsync(CreateOrdenDepositoDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Orden de depósito en una transacción (Agregar_Ws de FrmOrdendeDeposito): orden, detalle y un movimiento de ingreso en la cuenta por
/// cada elemento; los cheques de terceros pasan de EN CARTERA a DEPOSITADO.
/// </summary>
public sealed class CreateOrdenDepositoUseCase : ICreateOrdenDepositoUseCase
{
    private const int IdEmpresa = 1;

    private readonly IOrdenBancariaRepository _ordenes;
    private readonly IOrdenPagoRepository _cheques;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public CreateOrdenDepositoUseCase(
        IOrdenBancariaRepository ordenes, IOrdenPagoRepository cheques, IReciboCobroRepository comprobantes, IReferenciasRepository referencias,
        IUnitOfWork unitOfWork, IServerClock clock, ICurrentUser currentUser)
    {
        _ordenes = ordenes;
        _cheques = cheques;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<OrdenDepositoDisplay> ExecuteAsync(CreateOrdenDepositoDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = OrdenBancariaContexto.RequireIdUsuario(_currentUser);
        if (dto.Elementos.Count == 0)
            throw new BusinessException("Agregue al menos un elemento a depositar.");

        var deposito = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var od = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "OD", ct);
            var codigos = await CodigosOrdenPago.LoadAsync(_referencias, ct);
            var cuenta = await OrdenBancariaContexto.GetCuentaActivaAsync(_ordenes, _referencias, dto.IdBancoCuenta!.Value, ct);
            var planilla = await Ventas.VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, od, NumeracionCompra.Letra, ct);
            var (puntoVenta, numero) = await NumeracionCompra.ReservarAsync(
                _comprobantes, _referencias, "OD", od, planilla.PuntoVenta!, dto.PuntoVenta, dto.Numero, "depósitos", ct);
            var ahora = await _clock.GetNowAsync(ct);
            var fecha = dto.FechaEmision!.Value;

            var orden = new Db.OrdenesDepositos
            {
                IdComprobanteTipo = od,
                FechaEmision = fecha,
                IdBancoCuenta = cuenta.IdBancoCuenta,
                Letra = NumeracionCompra.Letra,
                PuntoVenta = puntoVenta,
                Numero = numero,
                IdUsuario = idUsuario,
                IdEmpresa = IdEmpresa,
                IdPlanillaCaja = planilla.IdPlanillaCaja,
                Total = dto.Elementos.Sum(e => e.Importe), // el ERP tomaba el total del navegador
                Observaciones = dto.Observaciones ?? string.Empty,
                Estado = await _referencias.GetIdEstadoAsync("ORDENDEPOSITO", "GENERADA", ct),
            };
            _ordenes.Add(orden);
            await _ordenes.SaveChangesAsync(ct);

            var activo = await _referencias.IdAsync(EstadosCobranza.MovimientoBancoActivo, ct);
            foreach (var e in dto.Elementos)
            {
                Db.BancosCuentasMovimientos movimiento;
                switch (codigos.ClasificarElemento(e.IdElementoCobro!.Value))
                {
                    case ElementoPago.Efectivo:
                    case ElementoPago.DepositoBancario:
                        movimiento = OrdenBancariaContexto.Movimiento(cuenta, OrdenBancariaContexto.MovimientoDeposito, true, e.Importe, ahora, od, orden.IdOrdenDepostio, activo,
                            e.IdBancoOrigen, e.IdSucursalOrigen, e.IdTipoOrigen, e.NroCuentaOrigen, e.IdBancoDestino, e.IdSucursalDestino, e.IdTipoDestino, e.NroCuentaDestino);
                        break;

                    case ElementoPago.Tarjeta:
                        // El ERP grababa Debe = importe con Total negativo; acá es un ingreso coherente.
                        movimiento = OrdenBancariaContexto.Movimiento(cuenta, OrdenBancariaContexto.MovimientoDeposito, true, e.Importe, ahora, od, orden.IdOrdenDepostio, activo,
                            bancoDestino: e.IdBancoDestino, sucursalDestino: e.IdSucursalDestino, tipoDestino: e.IdTipoDestino, nroDestino: e.NroCuentaDestino);
                        break;

                    case ElementoPago.ChequeTercero:
                    {
                        var cheque = await _cheques.GetChequeTerceroAsync(e.IdCheque, ct) ?? throw new NotFoundException($"Cheque de terceros {e.IdCheque} no existe.");
                        if (cheque.Importe != e.Importe)
                            throw new BusinessException($"El importe informado no coincide con el del cheque {cheque.Nro}.");
                        if (!await _cheques.AsignarChequeTerceroAsync(e.IdCheque, orden.IdOrdenDepostio, od,
                                await _referencias.GetIdEstadoAsync("CLIENTECHEQUE", "DEPOSITADO", ct), await _referencias.IdAsync(EstadosCobranza.ChequeEnCartera, ct), ct))
                            throw new ConflictException($"El cheque {cheque.Nro} ya no está en cartera.");
                        movimiento = OrdenBancariaContexto.Movimiento(cuenta, OrdenBancariaContexto.MovimientoDepositoCheque, true, e.Importe, fecha, od, orden.IdOrdenDepostio, activo,
                            cheque.IdBanco ?? 0, cheque.IdSucursal ?? 0, 0, cheque.Nro);
                        break;
                    }

                    default:
                        throw new BusinessException($"El elemento {e.IdElementoCobro} no se puede depositar (efectivo, cheque de terceros, depósito bancario o tarjeta).");
                }

                _ordenes.Add(movimiento);
                await _ordenes.SaveChangesAsync(ct);

                _ordenes.Add(new Db.OrdenesDepositosDetalle
                {
                    IdOrdendeposito = orden.IdOrdenDepostio,
                    IdElementoCobroPago = e.IdElementoCobro,
                    Descripcion = e.Descripcion ?? string.Empty,
                    Detalle = e.Descripcion ?? string.Empty,
                    IdBanco = e.IdBancoOrigen,
                    IdSucursal = e.IdSucursalOrigen,
                    Banco = e.Banco ?? string.Empty,
                    Sucursal = e.Sucursal ?? string.Empty,
                    Recepcion = e.FechaRecepcion ?? fecha,
                    Emision = e.FechaEmision ?? fecha,
                    Vto = e.FechaVencimiento ?? fecha,
                    Nro = e.Numero ?? string.Empty,
                    IdElemento = movimiento.IdBancoCuentaMovimiento,
                    Total = e.Importe,
                });
            }

            await _ordenes.SaveChangesAsync(ct);
            return orden;
        }, cancellationToken);

        return deposito.ToDisplay();
    }
}

public interface ICreateOrdenExtraccionUseCase
{
    Task<OrdenExtraccionDisplay> ExecuteAsync(CreateOrdenExtraccionDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Orden de extracción en una transacción (Agregar_Ws de FrmOrdendeExtraccion): orden, detalle y movimiento de egreso de la cuenta.
/// Solo efectivo: el ERP copió el depósito y para cheques / transferencias / tarjetas grababa ingresos.
/// </summary>
public sealed class CreateOrdenExtraccionUseCase : ICreateOrdenExtraccionUseCase
{
    private const int IdEmpresa = 1;

    private readonly IOrdenBancariaRepository _ordenes;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public CreateOrdenExtraccionUseCase(
        IOrdenBancariaRepository ordenes, IReciboCobroRepository comprobantes, IReferenciasRepository referencias,
        IUnitOfWork unitOfWork, IServerClock clock, ICurrentUser currentUser)
    {
        _ordenes = ordenes;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<OrdenExtraccionDisplay> ExecuteAsync(CreateOrdenExtraccionDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = OrdenBancariaContexto.RequireIdUsuario(_currentUser);

        var extraccion = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var oe = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "OE", ct);
            var efectivo = await _referencias.GetParametroEnteroAsync("ELEMENTO", "EFECTIVO", ct);
            var cuenta = await OrdenBancariaContexto.GetCuentaActivaAsync(_ordenes, _referencias, dto.IdBancoCuenta!.Value, ct);
            var planilla = await Ventas.VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, oe, NumeracionCompra.Letra, ct);
            var (puntoVenta, numero) = await NumeracionCompra.ReservarAsync(
                _comprobantes, _referencias, "OE", oe, planilla.PuntoVenta!, dto.PuntoVenta, dto.Numero, "extracciones", ct);
            var ahora = await _clock.GetNowAsync(ct);
            var fecha = dto.FechaEmision!.Value;

            var orden = new Db.OrdenesExrtacciones
            {
                IdComprobanteTipo = oe,
                FechaEmision = fecha,
                IdBancoCuenta = cuenta.IdBancoCuenta,
                Letra = NumeracionCompra.Letra,
                PuntoVenta = puntoVenta,
                Numero = numero,
                IdUsuario = idUsuario,
                IdEmpresa = IdEmpresa,
                IdPlanillaCaja = planilla.IdPlanillaCaja,
                Total = dto.Importe,
                Observaciones = dto.Observaciones ?? string.Empty,
                Estado = await _referencias.GetIdEstadoAsync("ORDENEXTRACCION", "GENERADA", ct),
            };
            _ordenes.Add(orden);
            await _ordenes.SaveChangesAsync(ct);

            // El ERP invierte origen y destino en la extracción: el origen es la cuenta propia.
            var movimiento = OrdenBancariaContexto.Movimiento(cuenta, OrdenBancariaContexto.MovimientoExtraccion, false, dto.Importe, ahora, oe, orden.IdOrdenExtraccion,
                await _referencias.IdAsync(EstadosCobranza.MovimientoBancoActivo, ct),
                cuenta.IdBanco ?? 0, cuenta.IdBancoSucursal ?? 0, cuenta.IdCuentaTipo ?? 0, cuenta.NroCuenta);
            _ordenes.Add(movimiento);
            await _ordenes.SaveChangesAsync(ct);

            _ordenes.Add(new Db.OrdenesExtraccionesDetalle
            {
                IdOrdenExtraccion = orden.IdOrdenExtraccion,
                IdElementoCobroPago = efectivo,
                Descripcion = dto.Descripcion ?? "EFECTIVO",
                Detalle = dto.Descripcion ?? "EFECTIVO",
                IdBanco = cuenta.IdBanco,
                IdSucursal = cuenta.IdBancoSucursal,
                Banco = string.Empty,
                Sucursal = string.Empty,
                Recepcion = fecha,
                Emision = fecha,
                Vto = fecha,
                Nro = string.Empty,
                IdElemento = movimiento.IdBancoCuentaMovimiento,
                Total = dto.Importe,
            });
            await _ordenes.SaveChangesAsync(ct);
            return orden;
        }, cancellationToken);

        return extraccion.ToDisplay();
    }
}

public interface IAnularOrdenBancariaUseCase
{
    Task AnularDepositoAsync(int idOrdenDeposito, CancellationToken cancellationToken = default);

    Task AnularExtraccionAsync(int idOrdenExtraccion, CancellationToken cancellationToken = default);
}

/// <summary>
/// Anulación de orden de depósito / extracción (Editar_Ws): orden ANULADA, movimientos bancarios anulados, cheques de terceros de vuelta
/// en cartera y detalle anulado. Solo una vez (el ERP no controlaba el estado y permitía anular dos veces).
/// </summary>
public sealed class AnularOrdenBancariaUseCase : IAnularOrdenBancariaUseCase
{
    private readonly IOrdenBancariaRepository _ordenes;
    private readonly IOrdenPagoRepository _cheques;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public AnularOrdenBancariaUseCase(
        IOrdenBancariaRepository ordenes, IOrdenPagoRepository cheques, IReciboCobroRepository comprobantes, IReferenciasRepository referencias,
        IUnitOfWork unitOfWork, IServerClock clock, ICurrentUser currentUser)
    {
        _ordenes = ordenes;
        _cheques = cheques;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task AnularDepositoAsync(int idOrdenDeposito, CancellationToken cancellationToken = default)
    {
        OrdenBancariaContexto.RequireIdUsuario(_currentUser);
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var od = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "OD", ct);
            _ = await _ordenes.GetDepositoAsync(idOrdenDeposito, ct) ?? throw new NotFoundException($"Orden de depósito {idOrdenDeposito} no existe.");
            var ahora = await _clock.GetNowAsync(ct);
            if (!await _ordenes.AnularDepositoAsync(idOrdenDeposito, await _referencias.GetIdEstadoAsync("ORDENDEPOSITO", "ANULADA", ct),
                    await _referencias.GetIdEstadoAsync("ORDENDEPOSITO", "GENERADA", ct), ahora, ct))
                throw new ConflictException($"La orden de depósito {idOrdenDeposito} ya está anulada.");

            await _comprobantes.AnularMovimientosBancoAsync(od, idOrdenDeposito, await _referencias.IdAsync(EstadosCobranza.MovimientoBancoAnulado, ct), ahora, ct);
            await _cheques.DevolverChequesTercerosAsync(idOrdenDeposito, od, await _referencias.IdAsync(EstadosCobranza.ChequeEnCartera, ct), ct);
            await _ordenes.AnularDetalleDepositoAsync(idOrdenDeposito, ahora, ct);
            return true;
        }, cancellationToken);
    }

    public async Task AnularExtraccionAsync(int idOrdenExtraccion, CancellationToken cancellationToken = default)
    {
        OrdenBancariaContexto.RequireIdUsuario(_currentUser);
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var oe = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "OE", ct);
            _ = await _ordenes.GetExtraccionAsync(idOrdenExtraccion, ct) ?? throw new NotFoundException($"Orden de extracción {idOrdenExtraccion} no existe.");
            var ahora = await _clock.GetNowAsync(ct);
            if (!await _ordenes.AnularExtraccionAsync(idOrdenExtraccion, await _referencias.GetIdEstadoAsync("ORDENEXTRACCION", "ANULADA", ct),
                    await _referencias.GetIdEstadoAsync("ORDENEXTRACCION", "GENERADA", ct), ahora, ct))
                throw new ConflictException($"La orden de extracción {idOrdenExtraccion} ya está anulada.");

            await _comprobantes.AnularMovimientosBancoAsync(oe, idOrdenExtraccion, await _referencias.IdAsync(EstadosCobranza.MovimientoBancoAnulado, ct), ahora, ct);
            await _ordenes.AnularDetalleExtraccionAsync(idOrdenExtraccion, ahora, ct);
            return true;
        }, cancellationToken);
    }
}
