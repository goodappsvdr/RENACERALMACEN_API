using System.ComponentModel.DataAnnotations;
using API.SERVICE.Models.Ventas;

namespace API.SERVICE.Models.Compras;

/// <summary>
/// Factura de compra (FC) — FrmFacturasCompras. Registra la factura que emitió el proveedor: punto de venta y número son los
/// del proveedor. Suma stock (salvo lo que ya ingresó con un remito de compra), carga la deuda en la cta. cte. del proveedor
/// y, si la sucursal es Responsable Inscripto, el libro IVA compras. Los importes se graban como llegan.
/// </summary>
public sealed class CreateFacturaCompraDto : ComprobanteCompraDtoBase
{
    /// <summary>Remitos de compra / órdenes de compra que se facturan.</summary>
    public List<ComprobanteRelacionadoDto> Comprobantes { get; set; } = [];
}

/// <summary>
/// Nota de crédito de proveedor (NCP) — FrmNotaCreditoProveedor. Mercadería devuelta o descuento del proveedor: resta stock, deja saldo
/// a favor en la cta. cte. del proveedor (se usa en una orden de pago) y va al libro IVA compras como nota de crédito.
/// </summary>
public sealed class CreateNotaCreditoCompraDto : ComprobanteCompraDtoBase;

/// <summary>Datos comunes a los comprobantes que emite el proveedor (factura y nota de crédito).</summary>
public abstract class ComprobanteCompraDtoBase
{
    [Required] public int? IdProveedor { get; set; }

    [Required] public int? IdSucursal { get; set; }

    [Required, MaxLength(1)] public string Letra { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{1,5}$", ErrorMessage = "Punto de venta: hasta 5 dígitos.")]
    public string PuntoVenta { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{1,8}$", ErrorMessage = "Número: hasta 8 dígitos.")]
    public string Numero { get; set; } = string.Empty;

    [Required] public DateTime? FechaEmision { get; set; }

    [MaxLength(5000)] public string? Observaciones { get; set; }

    // Datos del proveedor impresos en la factura. Si no se informan, salen de su ficha.
    [MaxLength(50)] public string? RazonSocial { get; set; }
    public int? IdCategoriaIva { get; set; }
    [MaxLength(50)] public string? Cuit { get; set; }
    public int? IdProvincia { get; set; }
    public int? IdLocalidad { get; set; }
    [MaxLength(50)] public string? Calle { get; set; }

    public int IdTransporte { get; set; }
    [MaxLength(50)] public string? Transporte { get; set; }
    public int IdUnidad { get; set; }
    [MaxLength(50)] public string? Unidad { get; set; }
    public int IdChofer { get; set; }
    [MaxLength(50)] public string? Chofer { get; set; }

    public decimal Neto { get; set; }
    public decimal Iva { get; set; }
    public decimal Otros { get; set; }
    [Range(0.01, double.MaxValue)] public decimal Total { get; set; }
    public decimal PorcentajeDescuento { get; set; }
    public decimal TotalDescuento { get; set; }

    [Required, MinLength(1)]
    public List<FacturaCompraItemDto> Items { get; set; } = [];

    /// <summary>Percepciones, impuestos internos, IIBB, etc. (DocumentosProveedorOtrosTributos).</summary>
    public List<OtroTributoDto> OtrosTributos { get; set; } = [];
}

public sealed class FacturaCompraItemDto
{
    [Required] public int? IdItem { get; set; }
    [Required, MaxLength(500)] public string Descripcion { get; set; } = string.Empty;
    [Range(0.0001, double.MaxValue)] public decimal Cantidad { get; set; }
    [MaxLength(50)] public string? ListaPrecio { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal PrecioNeto { get; set; }
    public decimal IvaAlicuota { get; set; }
    public decimal Iva { get; set; }
    public decimal Otros { get; set; }
    public decimal Total { get; set; }
    public int IdImpuestoIva { get; set; }
    public decimal Bonificacion { get; set; }

    /// <summary>Número de serie que ingresa con la compra (alta en ItemsNroSeries).</summary>
    [MaxLength(50)] public string? NroSerie { get; set; }

    /// <summary>Línea del remito de compra / orden de compra que se factura.</summary>
    public ComprobanteRelacionadoDetalleDto? Relacion { get; set; }
}

public sealed class OtroTributoDto
{
    /// <summary>ID_OtrosTributos (1 nacionales, 2 provinciales, 3 municipales, 4 internos, 5 IIBB, 6–9 percepciones, 18 otros…).</summary>
    [Required] public int? IdTributo { get; set; }
    [MaxLength(50)] public string? Detalle { get; set; }
    public decimal BaseImponible { get; set; }
    public decimal Alicuota { get; set; }
    public decimal Importe { get; set; }
}

public sealed record NuevaFacturaCompraDisplay(int IdPlanillaCaja, IReadOnlyList<string> Letras);

public sealed record ComprobanteCompraPendienteDisplay(
    int IdDocumentoProveedor, int IdComprobanteTipo, string Comprobante, string? RazonSocial, DateTime? FechaEmision, int? Estado, decimal? Total);

/// <summary>Línea con saldo pendiente de un comprobante de proveedor, con su <c>Relacion</c> lista para la factura.</summary>
public sealed record LineaPendienteCompraDisplay(
    int? IdItem, string? Descripcion, decimal Cantidad, string? ListaPrecio, decimal? PrecioUnitario, decimal? PrecioNeto, decimal? IvaAlicuota,
    decimal? Iva, decimal? Otros, decimal? Total, int? IdImpuestoIva, decimal? Bonificacion, LineaRelacionDisplay Relacion);

