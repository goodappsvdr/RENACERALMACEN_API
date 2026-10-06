using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class LibroIvaVenta
{
    public int IdLibroIvaVenta { get; set; }

    public DateOnly? FechaEmision { get; set; }

    public string? TipoComprobante { get; set; }

    public string? Letra { get; set; }

    public string? PuntoVenta { get; set; }

    public string? Numero { get; set; }

    public string? RazonSocial { get; set; }

    public string? NroDocumento { get; set; }

    public decimal? TotalGeneral { get; set; }

    public decimal? TotalNeto { get; set; }

    public decimal? TotalIva { get; set; }

    public decimal? Neto21 { get; set; }

    public decimal? Neto10 { get; set; }

    public decimal? Neto27 { get; set; }

    public decimal? NetoExento { get; set; }

    public decimal? Iva21 { get; set; }

    public decimal? Iva10 { get; set; }

    public decimal? Iva27 { get; set; }

    public decimal? IvaExcento { get; set; }

    public decimal? IngBruto { get; set; }

    public decimal? Percepciones { get; set; }

    public decimal? ImpNacional { get; set; }

    public decimal? ImpMunicipal { get; set; }

    public decimal? ImpInterno { get; set; }

    public decimal? OtrosTributo { get; set; }

    public int? Mes { get; set; }

    public int? Anio { get; set; }

    public int? IdComprobante { get; set; }

    public int? IdComprobanteTipo { get; set; }
}
