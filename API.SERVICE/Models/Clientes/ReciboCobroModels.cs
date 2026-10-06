using System.ComponentModel.DataAnnotations;

namespace API.SERVICE.Models.Clientes;

/// <summary>Alta de un recibo de cobro (FrmRecibos).</summary>
public sealed class CreateReciboDto
{
    /// <summary>Cliente (ID_Entidad). Razón social, categoría de IVA y CUIT se toman de su ficha.</summary>
    [Required] public int? IdEntidad { get; set; }

    /// <summary>Fecha de emisión del recibo.</summary>
    [Required] public DateTime? FechaEmision { get; set; }

    /// <summary>Sucursal de la empresa que emite el recibo.</summary>
    [Required] public int? IdSucursal { get; set; }

    [MaxLength(500)]
    public string? Observaciones { get; set; }

    /// <summary>Recargo por pago con tarjeta: se suma al total del recibo y al de los comprobantes (como en el formulario).</summary>
    [Range(0, double.MaxValue)]
    public decimal RecargoTarjeta { get; set; }

    /// <summary>Solo si el parámetro NUMERACION/REC = 1 (numeración manual). Si no, lo asigna el servidor.</summary>
    [MaxLength(50)]
    public string? PuntoVenta { get; set; }

    /// <summary>Solo si el parámetro NUMERACION/REC = 1 (numeración manual). Si no, lo asigna el servidor.</summary>
    [MaxLength(50)]
    public string? Numero { get; set; }

    /// <summary>Comprobantes que cancela el recibo, en el orden en que se imputan.</summary>
    public List<ImputacionDto> Imputaciones { get; set; } = [];

    /// <summary>Formas de pago (efectivo, cheques, depósitos, tarjetas, retenciones...).</summary>
    public List<ElementoCobroDto> Elementos { get; set; } = [];
}

public sealed class ImputacionDto
{
    /// <summary>ID del comprobante en la cta. cte. (ID_Comprobante / ID_DocumentoCliente).</summary>
    [Required] public int? IdComprobante { get; set; }

    [Required] public int? IdComprobanteTipo { get; set; }

    /// <summary>Saldo del comprobante + interés. Tiene que coincidir con el saldo pendiente más <see cref="InteresAplicado"/>.</summary>
    [Range(0, double.MaxValue)]
    public decimal ImporteComprobante { get; set; }

    /// <summary>Interés por mora que se cobra sobre este comprobante.</summary>
    [Range(0, double.MaxValue)]
    public decimal InteresAplicado { get; set; }
}

public sealed class ElementoCobroDto
{
    /// <summary>ID_ElementoCobro (efectivo, cheque, depósito bancario, tarjeta, retención, ...).</summary>
    [Required] public int? IdElementoCobro { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Importe { get; set; }

    [MaxLength(50)] public string? Descripcion { get; set; }

    /// <summary>Nro. de cheque, de cupón de tarjeta, de cuenta origen o de comprobante de retención.</summary>
    [MaxLength(50)] public string? Numero { get; set; }

    [MaxLength(50)] public string? Banco { get; set; }

    [MaxLength(50)] public string? Sucursal { get; set; }

    /// <summary>Si no se informan, se usa la fecha del recibo.</summary>
    public DateTime? FechaRecepcion { get; set; }

    public DateTime? FechaEmision { get; set; }

    public DateTime? FechaVencimiento { get; set; }

    public int IdBancoOrigen { get; set; }

    public int IdSucursalOrigen { get; set; }

    public int IdTipoOrigen { get; set; }

    /// <summary>Obligatorio para depósitos bancarios y tarjetas.</summary>
    public int IdBancoCuenta { get; set; }

    public int IdBancoDestino { get; set; }

    public int IdSucursalDestino { get; set; }

    public int IdTipoDestino { get; set; }

    [MaxLength(50)] public string? NroCuentaDestino { get; set; }

    /// <summary>Obligatorio para retenciones.</summary>
    public int IdRetencionTipo { get; set; }
}

/// <summary>Datos para empezar un recibo: planilla de caja abierta del usuario y numeración.</summary>
public sealed record NuevoReciboDisplay(
    int IdPlanillaCaja,
    string PuntoVenta,
    string Letra,
    bool NumeracionManual,
    string? NumeroSugerido);

/// <summary>Comprobante pendiente de la cta. cte. del cliente, listo para imputar en un recibo.</summary>
public sealed record ComprobantePendienteDisplay(
    int IdComprobante,
    int IdComprobanteTipo,
    int IdEntidad,
    string? Comprobante,
    string? RazonSocial,
    DateTime? FechaEmision,
    DateTime? FechaVencimiento,
    decimal Saldo,
    decimal InteresAplicado,
    int InteresCliente,
    int DiasInteres,
    bool? Vencido,
    int DiasVencidos);
