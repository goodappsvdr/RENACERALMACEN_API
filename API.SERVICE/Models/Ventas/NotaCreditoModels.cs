using System.ComponentModel.DataAnnotations;

namespace API.SERVICE.Models.Ventas;

/// <summary>
/// Nota de crédito electrónica sobre una factura autorizada (FrmNotasCreditoAFIP).
/// Cliente, letra, sucursal y datos del ítem salen de la factura; los importes se graban como llegan (como en el ERP).
/// </summary>
public sealed class CreateNotaCreditoDto
{
    /// <summary>Factura electrónica (FV) autorizada que se acredita.</summary>
    [Required] public int? IdFactura { get; set; }

    [Required] public DateTime? FechaEmision { get; set; }

    [MaxLength(5000)] public string? Observaciones { get; set; }

    public decimal Neto { get; set; }
    public decimal Iva { get; set; }
    public decimal Otros { get; set; }
    [Range(0.01, double.MaxValue)] public decimal Total { get; set; }

    /// <summary>Total de envases devueltos; el ERP lo graba en DocumentosCliente.Porcentaje.</summary>
    public decimal TotalEnvases { get; set; }

    [Required, MinLength(1)]
    public List<NotaCreditoItemDto> Items { get; set; } = [];
}

/// <summary>Línea acreditada: referencia a la línea de la factura más la cantidad e importes acreditados.</summary>
public sealed class NotaCreditoItemDto
{
    /// <summary>Línea de la factura (ID_DocumentoClienteDetalle). Ítem, descripción, impuesto y lista salen de ella.</summary>
    [Required] public long? IdDocumentoClienteDetalle { get; set; }

    [Range(0.0001, double.MaxValue)] public decimal Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }
    public decimal PrecioNeto { get; set; }
    public decimal IvaAlicuota { get; set; }
    public decimal Iva { get; set; }
    public decimal Otros { get; set; }
    public decimal Total { get; set; }
    public decimal Porcentaje { get; set; }
    public decimal MontoDescuento { get; set; }
}

public sealed record NotaCreditoResultado(
    DocumentoClienteDisplay Comprobante,
    EstadoAutorizacionAfip Estado,
    string? Mensaje);
