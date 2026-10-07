using System.ComponentModel.DataAnnotations;

namespace API.SERVICE.Models.Ventas;

/// <summary>
/// Presupuesto (PV) — FrmPresupuestosABM. No mueve stock ni cta. cte.: deja cada línea con saldo pendiente para remitir o facturar.
/// La letra la resuelve el servidor (ComprobantesLetras según la categoría de IVA del cliente). Los importes se graban como llegan.
/// </summary>
public sealed class CreatePresupuestoDto : PresupuestoDtoBase
{
    [Required] public int? IdSucursal { get; set; }

    [Required] public DateTime? FechaEmision { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/PV = 1).</summary>
    [MaxLength(50)] public string? PuntoVenta { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/PV = 1).</summary>
    [MaxLength(50)] public string? Numero { get; set; }

    public decimal PorcentajeTotal { get; set; }
    public decimal TotalRecargo { get; set; }
    public decimal TotalDescuento { get; set; }
}

/// <summary>Modificación (Modificar_Ws): cliente, datos impresos, totales, observación y líneas (se reemplazan todas).</summary>
public sealed class UpdatePresupuestoDto : PresupuestoDtoBase;

public abstract class PresupuestoDtoBase
{
    [Required] public int? IdEntidad { get; set; }

    [MaxLength(5000)] public string? Observaciones { get; set; }

    // Datos del cliente impresos en el presupuesto. Si no se informan, salen de su ficha.
    [MaxLength(50)] public string? RazonSocial { get; set; }
    public int? IdCategoriaIva { get; set; }
    [MaxLength(50)] public string? Cuit { get; set; }
    public int? IdProvincia { get; set; }
    public int? IdLocalidad { get; set; }
    [MaxLength(50)] public string? Calle { get; set; }

    public decimal Neto { get; set; }
    public decimal Iva { get; set; }
    public decimal Otros { get; set; }
    [Range(0, double.MaxValue)] public decimal Total { get; set; }

    /// <summary>Líneas. <c>Relacion</c> e <c>IdNroSerie</c> no aplican a presupuestos.</summary>
    [Required, MinLength(1)]
    public List<VentaItemDto> Items { get; set; } = [];
}
