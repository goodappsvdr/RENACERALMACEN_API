using Db = global::API.DA.Entities;

namespace API.SERVICE.Interfaces.Caja;

/// <summary>Apertura y cierre de planillas de caja (FrmPlanillasCajaABM). Se usa dentro de <see cref="IUnitOfWork"/>.</summary>
public interface IPlanillaCajaRepository
{
    Task<Db.CajaPlanillas?> GetAsync(int idPlanillaCaja, CancellationToken cancellationToken = default);

    /// <summary>Planillas de las sucursales que opera el usuario (CajaPlanillas_BuscarPorID_Usuario), más nuevas primero.</summary>
    Task<List<PlanillaCajaRow>> GetDeSucursalesDelUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Si el usuario ya tiene una planilla en el estado indicado (abierta).</summary>
    Task<bool> TienePlanillaEnEstadoAsync(int idUsuario, int estado, CancellationToken cancellationToken = default);

    /// <summary>Diferencia de la última planilla del usuario: saldo inicial sugerido (CajaPlanillas_BuscarSaldoInicial).</summary>
    Task<decimal> GetSaldoAnteriorAsync(int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Suma de Debe (ingresos) y Haber (egresos) del detalle de la planilla (CajaPlanillas_BuscarPorID).</summary>
    Task<(decimal Debe, decimal Haber)> GetTotalesDetalleAsync(int idPlanillaCaja, CancellationToken cancellationToken = default);

    /// <summary>Puntos de venta existentes (PuntosVenta_BuscarParaSucursales).</summary>
    Task<List<string>> GetPuntosVentaAsync(CancellationToken cancellationToken = default);

    /// <summary>Punto de venta de la sucursal (PuntosVenta_BuscarPorID_Sucursal).</summary>
    Task<string?> GetPuntoVentaSucursalAsync(int idSucursal, CancellationToken cancellationToken = default);

    Task<Db.Usuarios?> GetUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Si el usuario opera la sucursal (UsuariosSucursales).</summary>
    Task<bool> OperaSucursalAsync(int idUsuario, int idSucursal, CancellationToken cancellationToken = default);

    void Add(Db.CajaPlanillas planilla);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>CajaPlanillas_Modificar.</summary>
    Task ModificarAsync(int idPlanillaCaja, PlanillaCajaCierre datos, CancellationToken cancellationToken = default);

    /// <summary>Lock exclusivo por usuario (sp_getapplock, dueño la transacción): una sola apertura a la vez.</summary>
    Task BloquearUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default);
}

public sealed record PlanillaCajaRow(Db.CajaPlanillas Planilla, string? Usuario, string? Estado);

public sealed record PlanillaCajaCierre(
    DateTime FechaCierre, decimal SaldoInicial, decimal TotalIngresos, decimal TotalEgresos, decimal TotalRendido, decimal Diferencia, int Estado);
