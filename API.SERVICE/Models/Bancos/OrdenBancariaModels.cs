using System.ComponentModel.DataAnnotations;
using API.SERVICE.Models.Compras;

namespace API.SERVICE.Models.Bancos;

/// <summary>
/// Orden de depósito (OD) — FrmOrdendeDeposito. Ingresa a una cuenta bancaria propia efectivo, cheques de terceros en cartera,
/// transferencias o cupones de tarjeta. Letra X y numeración propia.
/// </summary>
public sealed class CreateOrdenDepositoDto
{
    [Required] public int? IdBancoCuenta { get; set; }

    [Required] public DateTime? FechaEmision { get; set; }

    [MaxLength(500)] public string? Observaciones { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/OD = 1).</summary>
    [MaxLength(50)] public string? PuntoVenta { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/OD = 1).</summary>
    [MaxLength(50)] public string? Numero { get; set; }

    /// <summary>Lo que se deposita: efectivo, cheque de terceros (IdCheque), depósito bancario / transferencia o tarjeta.</summary>
    [Required, MinLength(1)] public List<ElementoPagoDto> Elementos { get; set; } = [];
}

/// <summary>Orden de extracción (OE) — FrmOrdendeExtraccion. Retira efectivo de una cuenta bancaria propia. Letra X y numeración propia.</summary>
public sealed class CreateOrdenExtraccionDto
{
    [Required] public int? IdBancoCuenta { get; set; }

    [Required] public DateTime? FechaEmision { get; set; }

    [Range(0.01, double.MaxValue)] public decimal Importe { get; set; }

    [MaxLength(50)] public string? Descripcion { get; set; }

    [MaxLength(500)] public string? Observaciones { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/OE = 1).</summary>
    [MaxLength(50)] public string? PuntoVenta { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/OE = 1).</summary>
    [MaxLength(50)] public string? Numero { get; set; }
}
