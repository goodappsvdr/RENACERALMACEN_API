using System.ComponentModel.DataAnnotations;

namespace API.SERVICE.Models.Ventas;

/// <summary>
/// Remito de venta (RV) — FrmRemitos. Puede ser directo o entregar presupuestos / comprobantes de venta pendientes de remitir.
/// La letra la resuelve el servidor (ComprobantesLetras según la categoría de IVA del cliente). Los importes se graban como llegan.
/// </summary>
public sealed class CreateRemitoDto
{
    [Required] public int? IdEntidad { get; set; }

    [Required] public DateTime? FechaEmision { get; set; }

    [Required] public int? IdSucursal { get; set; }

    /// <summary>El SP del ERP graba la observación en un varchar(50) y el remito no usa DocumentosClienteObservaciones.</summary>
    [MaxLength(50)] public string? Observaciones { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/RV = 1).</summary>
    [MaxLength(50)] public string? PuntoVenta { get; set; }

    /// <summary>Solo con numeración manual (NUMERACION/RV = 1).</summary>
    [MaxLength(50)] public string? Numero { get; set; }

    // Datos del cliente impresos en el remito. Si no se informan, salen de su ficha.
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

    /// <summary>Líneas. Las que vienen de un comprobante pendiente llevan <c>Relacion</c> con su línea.</summary>
    [Required, MinLength(1)]
    public List<VentaItemDto> Items { get; set; } = [];

    /// <summary>Presupuestos / comprobantes de venta que se entregan con este remito.</summary>
    public List<ComprobanteRelacionadoDto> Comprobantes { get; set; } = [];
}

/// <summary>Comprobante del cliente con mercadería pendiente de remitir (DocumentosCliente_FacturasParaRemitarPorID_Entidad).</summary>
public sealed record ComprobanteParaRemitirDisplay(
    int IdDocumentoCliente,
    int IdComprobanteTipo,
    string Comprobante,
    string? RazonSocial,
    DateTime? FechaEmision,
    int? Estado,
    decimal? Total);

/// <summary>
/// Línea con saldo pendiente de remitir / facturar (DocumentosClienteDetalle_BuscarPorID_DocumentoCliente_Pendiente_Remitar).
/// <c>Cantidad</c> es el saldo pendiente; <c>Relacion</c> va tal cual en la línea del remito o de la factura.
/// </summary>
public sealed record LineaPendienteDisplay(
    int? IdItem,
    string? Descripcion,
    decimal Cantidad,
    string? ListaPrecio,
    int? IdListaPrecio,
    decimal? PrecioUnitario,
    decimal? PrecioNeto,
    decimal? IvaAlicuota,
    decimal? Iva,
    decimal? Otros,
    decimal? Total,
    int? IdImpuestoIva,
    decimal? Porcentaje,
    decimal? MontoDescuento,
    LineaRelacionDisplay Relacion);

public sealed record LineaRelacionDisplay(int IdDocumentoCliente, int IdComprobanteTipo, int IdDocumentoClienteDetalle);
