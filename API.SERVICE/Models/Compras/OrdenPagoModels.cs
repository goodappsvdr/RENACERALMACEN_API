using System.ComponentModel.DataAnnotations;

namespace API.SERVICE.Models.Compras;

/// <summary>Orden de pago a proveedor (FrmOrdendePago). Letra X; razón social, categoría de IVA y CUIT salen de la ficha del proveedor.</summary>
public sealed class CreateOrdenPagoDto
{
    [Required] public int? IdProveedor { get; set; }

    [Required] public DateTime? FechaEmision { get; set; }

    [Required] public int? IdSucursal { get; set; }

    [MaxLength(500)] public string? Observaciones { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/OP = 1).</summary>
    [MaxLength(50)] public string? PuntoVenta { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/OP = 1).</summary>
    [MaxLength(50)] public string? Numero { get; set; }

    /// <summary>Comprobantes que paga / compensa, en el orden en que se imputan.</summary>
    public List<ImputacionPagoDto> Imputaciones { get; set; } = [];

    /// <summary>Formas de pago.</summary>
    public List<ElementoPagoDto> Elementos { get; set; } = [];
}

public sealed class ImputacionPagoDto
{
    [Required] public int? IdComprobante { get; set; }

    [Required] public int? IdComprobanteTipo { get; set; }

    /// <summary>Saldo del comprobante tal como lo devuelve comprobantes-pendientes (negativo para lo que está a favor de la empresa).</summary>
    public decimal ImporteComprobante { get; set; }
}

public sealed class ElementoPagoDto
{
    /// <summary>ID_ElementoCobro (efectivo, cheque de terceros, cheque propio, depósito bancario, tarjeta, retención…).</summary>
    [Required] public int? IdElementoCobro { get; set; }

    [Range(0.01, double.MaxValue)] public decimal Importe { get; set; }

    /// <summary>Cheque de terceros en cartera (ID_EntidadCheque) o cheque propio de una chequera (ID_BancoCheque). El ERP lo mandaba en ID_BancoCuenta.</summary>
    public int IdCheque { get; set; }

    [MaxLength(50)] public string? Descripcion { get; set; }
    [MaxLength(50)] public string? Numero { get; set; }
    [MaxLength(50)] public string? Banco { get; set; }
    [MaxLength(50)] public string? Sucursal { get; set; }
    public DateTime? FechaRecepcion { get; set; }
    public DateTime? FechaEmision { get; set; }
    public DateTime? FechaVencimiento { get; set; }

    public int IdBancoOrigen { get; set; }
    public int IdSucursalOrigen { get; set; }
    public int IdTipoOrigen { get; set; }
    [MaxLength(50)] public string? NroCuentaOrigen { get; set; }

    /// <summary>Cuenta propia de la que sale el pago (depósito / transferencia / tarjeta).</summary>
    public int IdBancoCuenta { get; set; }

    public int IdBancoDestino { get; set; }
    public int IdSucursalDestino { get; set; }
    public int IdTipoDestino { get; set; }
    [MaxLength(50)] public string? NroCuentaDestino { get; set; }

    public int IdRetencionTipo { get; set; }
}

public sealed record NuevaOrdenPagoDisplay(int IdPlanillaCaja, string PuntoVenta, string Letra, bool NumeracionManual, string? NumeroSugerido);

/// <summary>Comprobante pendiente en la cta. cte. del proveedor, listo para imputar.</summary>
public sealed record ComprobantePendientePagoDisplay(int IdComprobante, int IdComprobanteTipo, string? Comprobante, DateTime? Fecha, decimal Saldo);

/// <summary>
/// Compra con pago en el momento (COM) — FrmCompras. Suma stock y carga la deuda con el proveedor; si se informan formas de pago,
/// genera en la misma transacción una orden de pago que la cancela (total o parcialmente). Letra X y numeración propia (NUMERACION/COM).
/// </summary>
public sealed class CreateCompraContadoDto : ComprobanteCompraDtoBase
{
    /// <summary>Solo con numeración manual (NUMERACION/COM = 1).</summary>
    [MaxLength(50)] public string? PuntoVenta { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/COM = 1).</summary>
    [MaxLength(50)] public string? Numero { get; set; }

    /// <summary>Formas de pago de la orden de pago. Vacío: la compra queda en cta. cte.</summary>
    public List<ElementoPagoDto> Elementos { get; set; } = [];
}

public sealed record CompraContadoResultado(DocumentoProveedorDisplay Compra, API.SERVICE.Models.Proveedores.ProveedorReciboDisplay? OrdenPago);
