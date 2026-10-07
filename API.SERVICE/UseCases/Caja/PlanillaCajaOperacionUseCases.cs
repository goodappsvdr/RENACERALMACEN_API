using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Caja;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Models.Caja;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Caja;

/// <summary>Reglas de FrmPlanillasCajaABM (permisos por rol y estados de la planilla).</summary>
internal static class PlanillaCajaContexto
{
    public const string RolAdministrador = "ADMINISTRADOR";

    /// <summary>Roles que en el ERP ven todos los usuarios y puntos de venta (el ERP también pregunta por "CTO", que hoy no existe).</summary>
    public static readonly string[] RolesGlobales = ["CEO", "CTO"];

    public static bool EsGlobal(ICurrentUser user) => RolesGlobales.Any(user.IsInRole);

    public static int RequireIdUsuario(ICurrentUser user) =>
        user.IdUsuario ?? throw new ForbiddenException("El usuario no tiene un registro en Usuarios del ERP; no puede operar planillas de caja.");

    public static async Task<int> AbiertaAsync(IReferenciasRepository referencias, CancellationToken ct) =>
        await referencias.IdAsync(EstadosCobranza.PlanillaAbierta, ct);

    public static Task<int> CerradaAsync(IReferenciasRepository referencias, CancellationToken ct) =>
        referencias.GetIdEstadoAsync("CAJASPLANILLA", "CERRADA", ct);

    /// <summary>Ver / modificar: la planilla tiene que ser de una sucursal que el usuario opera (como la grilla del ERP), salvo roles globales.</summary>
    public static async Task<Db.CajaPlanillas> GetAccesibleAsync(IPlanillaCajaRepository planillas, ICurrentUser user, int idPlanillaCaja, CancellationToken ct)
    {
        var idUsuario = RequireIdUsuario(user);
        var planilla = await planillas.GetAsync(idPlanillaCaja, ct)
            ?? throw new NotFoundException($"Planilla de caja {idPlanillaCaja} no existe.");
        if (!EsGlobal(user) && !user.IsInRole(RolAdministrador) && !await planillas.OperaSucursalAsync(idUsuario, planilla.IdSucursal ?? 0, ct))
            throw new ForbiddenException($"La planilla de caja {idPlanillaCaja} es de una sucursal que el usuario no opera.");
        return planilla;
    }

    /// <summary>ValidarComboEstado del ERP: el administrador siempre; el resto solo con la planilla abierta hoy.</summary>
    public static bool PuedeCerrar(ICurrentUser user, Db.CajaPlanillas planilla, DateTime ahora) =>
        user.IsInRole(RolAdministrador) || planilla.FechaApertura?.Date == ahora.Date;

    public static decimal Saldo(decimal saldoInicial, decimal ingresos, decimal egresos, decimal rendido) =>
        saldoInicial + ingresos - egresos - rendido;
}

public interface IGetPlanillasCajaUseCase
{
    Task<IReadOnlyList<PlanillaCajaListaDisplay>> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>Planillas de las sucursales del usuario (PlanillasCajas_BuscarTodos_Ws).</summary>
public sealed class GetPlanillasCajaUseCase : IGetPlanillasCajaUseCase
{
    private readonly IPlanillaCajaRepository _planillas;
    private readonly ICurrentUser _currentUser;

    public GetPlanillasCajaUseCase(IPlanillaCajaRepository planillas, ICurrentUser currentUser)
    {
        _planillas = planillas;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PlanillaCajaListaDisplay>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var idUsuario = PlanillaCajaContexto.RequireIdUsuario(_currentUser);
        return (await _planillas.GetDeSucursalesDelUsuarioAsync(idUsuario, cancellationToken))
            .Select(r => new PlanillaCajaListaDisplay(
                r.Planilla.IdPlanillaCaja, r.Planilla.PuntoVenta, r.Planilla.FechaApertura, r.Planilla.FechaCierre, r.Planilla.IdUsuario, r.Usuario, r.Planilla.Estado, r.Estado))
            .ToList();
    }
}

public interface IGetPlanillaCajaResumenUseCase
{
    Task<PlanillaCajaResumenDisplay> ExecuteAsync(int idPlanillaCaja, CancellationToken cancellationToken = default);
}

/// <summary>Planilla con ingresos y egresos del detalle (PlanillasCajas_BuscarPorID_Ws).</summary>
public sealed class GetPlanillaCajaResumenUseCase : IGetPlanillaCajaResumenUseCase
{
    private readonly IPlanillaCajaRepository _planillas;
    private readonly IReferenciasRepository _referencias;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public GetPlanillaCajaResumenUseCase(IPlanillaCajaRepository planillas, IReferenciasRepository referencias, IServerClock clock, ICurrentUser currentUser)
    {
        _planillas = planillas;
        _referencias = referencias;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<PlanillaCajaResumenDisplay> ExecuteAsync(int idPlanillaCaja, CancellationToken cancellationToken = default)
    {
        var planilla = await PlanillaCajaContexto.GetAccesibleAsync(_planillas, _currentUser, idPlanillaCaja, cancellationToken);
        var (debe, haber) = await _planillas.GetTotalesDetalleAsync(idPlanillaCaja, cancellationToken);
        var abierta = planilla.Estado == await PlanillaCajaContexto.AbiertaAsync(_referencias, cancellationToken);
        var saldoInicial = planilla.SaldoInicial ?? 0m;
        var rendido = planilla.TotalRendido ?? 0m;

        return new PlanillaCajaResumenDisplay(
            planilla.IdPlanillaCaja, planilla.PuntoVenta, planilla.IdUsuario, planilla.FechaApertura, planilla.FechaCierre, planilla.Estado, abierta,
            saldoInicial, debe, haber, rendido, PlanillaCajaContexto.Saldo(saldoInicial, debe, haber, rendido),
            abierta && PlanillaCajaContexto.PuedeCerrar(_currentUser, planilla, await _clock.GetNowAsync(cancellationToken)));
    }
}

public interface IIniciarPlanillaCajaUseCase
{
    Task<NuevaPlanillaCajaDisplay> ExecuteAsync(int? idUsuario, CancellationToken cancellationToken = default);
}

/// <summary>Saldo inicial sugerido y puntos de venta habilitados (PlanillasCajas_BuscarSaldoInicialPor_IdUsuario + CargarCboPuntosVentas_WS).</summary>
public sealed class IniciarPlanillaCajaUseCase : IIniciarPlanillaCajaUseCase
{
    private readonly IPlanillaCajaRepository _planillas;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public IniciarPlanillaCajaUseCase(IPlanillaCajaRepository planillas, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _planillas = planillas;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<NuevaPlanillaCajaDisplay> ExecuteAsync(int? idUsuario, CancellationToken cancellationToken = default)
    {
        var destino = AbrirPlanillaCajaUseCase.ResolverUsuario(_currentUser, idUsuario);
        var puntos = await AbrirPlanillaCajaUseCase.PuntosVentaHabilitadosAsync(_planillas, _currentUser, cancellationToken);
        var saldo = await _planillas.GetSaldoAnteriorAsync(destino, cancellationToken);
        var abierta = await _planillas.TienePlanillaEnEstadoAsync(destino, await PlanillaCajaContexto.AbiertaAsync(_referencias, cancellationToken), cancellationToken);
        return new NuevaPlanillaCajaDisplay(destino, saldo, puntos, abierta);
    }
}

public interface IAbrirPlanillaCajaUseCase
{
    Task<PlanillaCajaResumenDisplay> ExecuteAsync(AbrirPlanillaCajaDto dto, CancellationToken cancellationToken = default);
}

/// <summary>Apertura de planilla de caja (CajasPlanilla_Agregar_Ws): una sola abierta por usuario.</summary>
public sealed class AbrirPlanillaCajaUseCase : IAbrirPlanillaCajaUseCase
{
    private const int IdEmpresa = 1;

    private readonly IPlanillaCajaRepository _planillas;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public AbrirPlanillaCajaUseCase(
        IPlanillaCajaRepository planillas, IReferenciasRepository referencias, IUnitOfWork unitOfWork, IServerClock clock, ICurrentUser currentUser)
    {
        _planillas = planillas;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<PlanillaCajaResumenDisplay> ExecuteAsync(AbrirPlanillaCajaDto dto, CancellationToken cancellationToken = default)
    {
        var destino = ResolverUsuario(_currentUser, dto.IdUsuario);
        var puntos = await PuntosVentaHabilitadosAsync(_planillas, _currentUser, cancellationToken);
        if (!puntos.Contains(dto.PuntoVenta))
            throw new BusinessException($"El punto de venta {dto.PuntoVenta} no está habilitado para el usuario.");

        var usuario = await _planillas.GetUsuarioAsync(destino, cancellationToken)
            ?? throw new NotFoundException($"Usuario {destino} no existe.");

        var planilla = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _planillas.BloquearUsuarioAsync(destino, ct);

            var abierta = await PlanillaCajaContexto.AbiertaAsync(_referencias, ct);
            if (await _planillas.TienePlanillaEnEstadoAsync(destino, abierta, ct))
                throw new ConflictException("Ya existe una caja abierta para este usuario.");

            var ahora = await _clock.GetNowAsync(ct);
            var nueva = new Db.CajaPlanillas
            {
                PuntoVenta = dto.PuntoVenta,
                IdEmpresa = IdEmpresa,
                FechaApertura = ahora,
                FechaCierre = ahora.Date, // el ERP graba la fecha del formulario (hoy) hasta que se cierra
                IdUsuario = destino,
                SaldoInicial = dto.SaldoInicial ?? await _planillas.GetSaldoAnteriorAsync(destino, ct),
                TotalIngresos = 0,
                TotalEgresos = 0,
                TotalRendido = 0,
                Diferencia = 0,
                Estado = abierta,
                IdSucursal = usuario.IdSucursal,
            };
            _planillas.Add(nueva);
            await _planillas.SaveChangesAsync(ct);
            return nueva;
        }, cancellationToken);

        var saldoInicial = planilla.SaldoInicial ?? 0m;
        return new PlanillaCajaResumenDisplay(
            planilla.IdPlanillaCaja, planilla.PuntoVenta, planilla.IdUsuario, planilla.FechaApertura, planilla.FechaCierre, planilla.Estado, true,
            saldoInicial, 0, 0, 0, saldoInicial, PuedeCerrar: true);
    }

    /// <summary>Sin rol CEO / CTO solo se opera la caja propia (el combo de usuarios del ERP).</summary>
    internal static int ResolverUsuario(ICurrentUser user, int? idUsuario)
    {
        var propio = PlanillaCajaContexto.RequireIdUsuario(user);
        var destino = idUsuario ?? propio;
        if (destino != propio && !PlanillaCajaContexto.EsGlobal(user))
            throw new ForbiddenException("Solo los roles CEO / CTO pueden abrir cajas de otros usuarios.");
        return destino;
    }

    /// <summary>CEO / CTO: todos los puntos de venta; el resto, el de su sucursal (CargarCboPuntosVentas_WS).</summary>
    internal static async Task<IReadOnlyList<string>> PuntosVentaHabilitadosAsync(IPlanillaCajaRepository planillas, ICurrentUser user, CancellationToken ct)
    {
        if (PlanillaCajaContexto.EsGlobal(user))
            return await planillas.GetPuntosVentaAsync(ct);

        var propio = await planillas.GetPuntoVentaSucursalAsync(user.IdSucursal ?? 0, ct);
        return propio is null ? [] : [propio];
    }
}

public interface IModificarPlanillaCajaUseCase
{
    Task<PlanillaCajaResumenDisplay> ExecuteAsync(int idPlanillaCaja, ModificarPlanillaCajaDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Modificación / cierre de planilla (CajasPlanilla_Modificar_Ws). Ingresos y egresos salen del detalle (como los muestra la
/// pantalla) y la diferencia la calcula el servidor; una planilla cerrada no se modifica.
/// </summary>
public sealed class ModificarPlanillaCajaUseCase : IModificarPlanillaCajaUseCase
{
    private readonly IPlanillaCajaRepository _planillas;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public ModificarPlanillaCajaUseCase(
        IPlanillaCajaRepository planillas, IReferenciasRepository referencias, IUnitOfWork unitOfWork, IServerClock clock, ICurrentUser currentUser)
    {
        _planillas = planillas;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<PlanillaCajaResumenDisplay> ExecuteAsync(int idPlanillaCaja, ModificarPlanillaCajaDto dto, CancellationToken cancellationToken = default)
    {
        var previa = await PlanillaCajaContexto.GetAccesibleAsync(_planillas, _currentUser, idPlanillaCaja, cancellationToken);

        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _planillas.BloquearUsuarioAsync(previa.IdUsuario ?? 0, ct);

            var planilla = await _planillas.GetAsync(idPlanillaCaja, ct) ?? previa;
            var cerrada = await PlanillaCajaContexto.CerradaAsync(_referencias, ct);
            if (planilla.Estado == cerrada)
                throw new ConflictException("No es posible modificar esta planilla de caja: ya fue cerrada.");

            var ahora = await _clock.GetNowAsync(ct);
            if (dto.Cerrar && !PlanillaCajaContexto.PuedeCerrar(_currentUser, planilla, ahora))
                throw new ForbiddenException("Solo un administrador puede cerrar una planilla de caja abierta otro día.");

            var (debe, haber) = await _planillas.GetTotalesDetalleAsync(idPlanillaCaja, ct);
            var saldo = PlanillaCajaContexto.Saldo(dto.SaldoInicial, debe, haber, dto.TotalRendido);
            var estado = dto.Cerrar ? cerrada : planilla.Estado ?? 0;

            await _planillas.ModificarAsync(idPlanillaCaja,
                new PlanillaCajaCierre(ahora, dto.SaldoInicial, debe, haber, dto.TotalRendido, saldo, estado), ct);

            return new PlanillaCajaResumenDisplay(
                idPlanillaCaja, planilla.PuntoVenta, planilla.IdUsuario, planilla.FechaApertura, ahora, estado, !dto.Cerrar,
                dto.SaldoInicial, debe, haber, dto.TotalRendido, saldo, !dto.Cerrar && PlanillaCajaContexto.PuedeCerrar(_currentUser, planilla, ahora));
        }, cancellationToken);
    }
}
