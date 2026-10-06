using System.ComponentModel.DataAnnotations;

namespace API.SERVICE.Models.Ventas;

/// <summary>Alta de factura electrónica (FV) — FrmFacturasAFIP. Mismos datos que el comprobante interno más la letra.</summary>
public sealed class CreateFacturaElectronicaDto : VentaDtoBase
{
    /// <summary>A, B o C.</summary>
    [Required, RegularExpression("^[ABC]$", ErrorMessage = "La letra tiene que ser A, B o C.")]
    public string Letra { get; set; } = string.Empty;
}

public enum EstadoAutorizacionAfip
{
    /// <summary>AFIP aprobó: la factura tiene CAE, número definitivo, código de barras y QR.</summary>
    Autorizada,

    /// <summary>AFIP no respondió: la factura quedó grabada sin CAE y hay que reintentar la autorización.</summary>
    PendienteAfip,
}

/// <summary>Datos para empezar una factura electrónica: planilla, punto de venta AFIP y próximo número según AFIP.</summary>
public sealed record NuevaFacturaElectronicaDisplay(
    int IdPlanillaCaja,
    string PuntoVenta,
    string Letra,
    int CbteTipo,
    string NumeroSugerido);

public sealed record AutorizacionAfipResultado(
    int IdDocumentoCliente,
    EstadoAutorizacionAfip Estado,
    string? Cae,
    string? Numero,
    string? Mensaje);

public sealed record FacturaElectronicaResultado(
    DocumentoClienteDisplay Comprobante,
    int? IdRecibo,
    EstadoAutorizacionAfip Estado,
    string? Mensaje);
