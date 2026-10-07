using System.ComponentModel.DataAnnotations;

namespace API.SERVICE.Models.Stock;

/// <summary>Transferencia de mercadería entre sucursales (comprobante MS, letra M) — FrmMovimientoStockABM.</summary>
public sealed class CreateMovimientoStockDto
{
    /// <summary>Sucursal que envía. Si se omite, la del usuario.</summary>
    public int? IdSucursalOrigen { get; set; }

    [Required] public int? IdSucursalDestino { get; set; }

    [Required] public DateTime? FechaEmision { get; set; }

    [MaxLength(50)] public string? Observaciones { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/MS = 1).</summary>
    [MaxLength(50)] public string? PuntoVenta { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/MS = 1).</summary>
    [MaxLength(50)] public string? Numero { get; set; }

    [Required, MinLength(1)] public List<MovimientoStockItemDto> Items { get; set; } = [];
}

public sealed class MovimientoStockItemDto
{
    [Required] public int? IdItem { get; set; }

    [Range(0.0001, double.MaxValue)] public decimal Cantidad { get; set; }
}

public sealed record NuevoMovimientoStockDisplay(int IdSucursalOrigen, string PuntoVenta, string Letra, bool NumeracionManual, string? NumeroSugerido);

public sealed record MovimientoStockItemDisplay(int IdItem, string Descripcion, decimal Cantidad);

public sealed record MovimientoStockDisplay(
    int IdMovimientoStock,
    string Letra,
    string PuntoVenta,
    string Numero,
    DateTime? FechaEmision,
    int IdSucursalOrigen,
    int IdSucursalDestino,
    string SucursalDestino,
    int IdUsuario,
    int Estado,
    string? Observaciones,
    IReadOnlyList<MovimientoStockItemDisplay> Items);
