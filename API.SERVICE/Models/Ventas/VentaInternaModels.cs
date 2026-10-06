using System.ComponentModel.DataAnnotations;
using API.SERVICE.Models.Clientes;

namespace API.SERVICE.Models.Ventas;

/// <summary>
/// Alta de comprobante interno de venta (VEN, letra X, sin AFIP) — FrmFacturas.
/// Los importes de líneas y totales se graban como llegan, igual que en el ERP (los calcula la pantalla).
/// </summary>
public sealed class CreateVentaInternaDto : VentaDtoBase
{
    /// <summary>Solo con numeración manual (NUMERACION/RV = 1).</summary>
    [MaxLength(50)] public string? PuntoVenta { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/RV = 1).</summary>
    [MaxLength(50)] public string? Numero { get; set; }
}

/// <summary>Datos comunes a todo comprobante de venta (interno o factura electrónica).</summary>
public abstract class VentaDtoBase
{
    [Required] public int? IdEntidad { get; set; }

    /// <summary>Fecha del comprobante.</summary>
    [Required] public DateTime? FechaEmision { get; set; }

    [Required] public int? IdSucursal { get; set; }

    [MaxLength(5000)]
    public string? Observaciones { get; set; }

    // Datos del cliente impresos en el comprobante. Si no se informan, salen de su ficha
    // (permite, por ejemplo, cargar el nombre de un consumidor final genérico).
    [MaxLength(50)] public string? RazonSocial { get; set; }
    public int? IdCategoriaIva { get; set; }
    [MaxLength(50)] public string? Cuit { get; set; }
    public int? IdProvincia { get; set; }
    public int? IdLocalidad { get; set; }
    [MaxLength(50)] public string? Calle { get; set; }

    // Totales
    public decimal Neto { get; set; }
    public decimal Iva { get; set; }
    public decimal Otros { get; set; }
    [Range(0, double.MaxValue)] public decimal Total { get; set; }
    public decimal PorcentajeTotal { get; set; }
    public decimal TotalRecargo { get; set; }
    public decimal TotalDescuento { get; set; }

    [Required, MinLength(1)]
    public List<VentaItemDto> Items { get; set; } = [];

    /// <summary>Presupuestos / remitos que se facturan con este comprobante.</summary>
    public List<ComprobanteRelacionadoDto> Remitos { get; set; } = [];

    /// <summary>Cobro en el momento: si hay formas de pago se genera un recibo que imputa este comprobante.</summary>
    public List<ElementoCobroDto> Elementos { get; set; } = [];

    /// <summary>Confirma la venta aunque el cliente supere su límite de cta. cte. (el ERP lo preguntaba en pantalla).</summary>
    public bool ConfirmarExcesoLimite { get; set; }
}

public sealed class VentaItemDto
{
    [Required] public int? IdItem { get; set; }

    [Required, MaxLength(500)] public string Descripcion { get; set; } = string.Empty;

    [Range(0.0001, double.MaxValue)] public decimal Cantidad { get; set; }

    [MaxLength(50)] public string? ListaPrecio { get; set; }
    public int IdListaPrecio { get; set; }

    public decimal PrecioUnitario { get; set; }
    public decimal PrecioNeto { get; set; }
    public decimal IvaAlicuota { get; set; }
    public decimal Iva { get; set; }
    public decimal Otros { get; set; }
    public decimal Total { get; set; }
    public int IdImpuestoIva { get; set; }

    /// <summary>Porcentaje de descuento de la línea.</summary>
    public decimal Porcentaje { get; set; }

    /// <summary>Importe de descuento de la línea.</summary>
    public decimal MontoDescuento { get; set; }

    /// <summary>Número de serie vendido (ItemsNroSeries), si el ítem lo usa.</summary>
    public int? IdNroSerie { get; set; }

    /// <summary>Línea del presupuesto / remito que se factura (0 o null si es una venta directa).</summary>
    public ComprobanteRelacionadoDetalleDto? Relacion { get; set; }
}

public sealed class ComprobanteRelacionadoDto
{
    [Required] public int? IdDocumentoCliente { get; set; }
    [Required] public int? IdComprobanteTipo { get; set; }
}

public sealed class ComprobanteRelacionadoDetalleDto
{
    [Required] public int? IdDocumentoCliente { get; set; }
    [Required] public int? IdComprobanteTipo { get; set; }
    [Required] public int? IdDocumentoClienteDetalle { get; set; }
}

/// <summary>Datos para empezar un comprobante interno: planilla de caja abierta, punto de venta y número sugerido.</summary>
public sealed record NuevaVentaInternaDisplay(
    int IdPlanillaCaja,
    string PuntoVenta,
    string Letra,
    bool NumeracionManual,
    string? NumeroSugerido);

public sealed record VentaInternaResultado(DocumentoClienteDisplay Comprobante, int? IdRecibo);
