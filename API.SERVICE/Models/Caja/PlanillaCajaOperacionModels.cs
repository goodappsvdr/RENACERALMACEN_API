using System.ComponentModel.DataAnnotations;

namespace API.SERVICE.Models.Caja;

/// <summary>Apertura de planilla de caja (CajasPlanilla_Agregar_Ws).</summary>
public sealed class AbrirPlanillaCajaDto
{
    /// <summary>Usuario dueño de la caja. Por defecto el del token; otro usuario solo con rol CEO / CTO.</summary>
    public int? IdUsuario { get; set; }

    [Required, MaxLength(50)] public string PuntoVenta { get; set; } = string.Empty;

    /// <summary>Por defecto, la diferencia de la última planilla del usuario.</summary>
    public decimal? SaldoInicial { get; set; }
}

/// <summary>
/// Modificación / cierre (CajasPlanilla_Modificar_Ws). Ingresos y egresos los calcula el servidor con el detalle de la planilla.
/// </summary>
public sealed class ModificarPlanillaCajaDto
{
    public decimal SaldoInicial { get; set; }

    /// <summary>Efectivo rendido al cerrar.</summary>
    [Range(0, double.MaxValue)] public decimal TotalRendido { get; set; }

    /// <summary>true: la planilla pasa a CERRADA.</summary>
    public bool Cerrar { get; set; }
}

public sealed record PlanillaCajaListaDisplay(
    int IdPlanillaCaja, string? PuntoVenta, DateTime? FechaApertura, DateTime? FechaCierre, int? IdUsuario, string? Usuario, int? Estado, string? EstadoNombre);

/// <summary>Planilla con los totales del detalle y el saldo calculado = inicial + ingresos − egresos − rendido.</summary>
public sealed record PlanillaCajaResumenDisplay(
    int IdPlanillaCaja,
    string? PuntoVenta,
    int? IdUsuario,
    DateTime? FechaApertura,
    DateTime? FechaCierre,
    int? Estado,
    bool Abierta,
    decimal SaldoInicial,
    decimal Ingresos,
    decimal Egresos,
    decimal TotalRendido,
    decimal Saldo,
    /// <summary>Si el usuario puede cambiarle el estado (cerrarla): administrador, o planilla abierta hoy.</summary>
    bool PuedeCerrar);

public sealed record NuevaPlanillaCajaDisplay(int IdUsuario, decimal SaldoInicialSugerido, IReadOnlyList<string> PuntosVenta, bool TieneAbierta);
