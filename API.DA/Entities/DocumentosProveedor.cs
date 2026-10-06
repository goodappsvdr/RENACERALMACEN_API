using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class DocumentosProveedor
{
    public int IdDocumentoProveedor { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public int? IdCondicion { get; set; }

    public string? Letra { get; set; }

    public int? IdPuntoVenta { get; set; }

    public string? PuntoVenta { get; set; }

    public string? Numero { get; set; }

    public int? IdProveedor { get; set; }

    public string? RazonSocial { get; set; }

    public int? IdCategoriaIva { get; set; }

    public string? NroDoc { get; set; }

    public int? IdProvincia { get; set; }

    public int? IdLocalidad { get; set; }

    public string? Calle { get; set; }

    public string? Nro { get; set; }

    public decimal? TotalNeto { get; set; }

    public decimal? TotalIva { get; set; }

    public decimal? TotalOtrosImpuestos { get; set; }

    public decimal? TotalGeneral { get; set; }

    public DateTime? FechaEmision { get; set; }

    public int? IdUsuario { get; set; }

    public int? IdEmpresa { get; set; }

    public int? IdPlanillaCaja { get; set; }

    public int? Estado { get; set; }

    public string? Cae { get; set; }

    public DateTime? VtoCae { get; set; }

    public int? IdTransporte { get; set; }

    public string? Transporte { get; set; }

    public int? IdUnidad { get; set; }

    public string? Unidad { get; set; }

    public int? IdChofer { get; set; }

    public string? Chofer { get; set; }

    public int? IdRemito { get; set; }

    public int? IdSucursal { get; set; }

    public decimal? TotalDescuento { get; set; }

    public decimal? PorcentajeDescuento { get; set; }

    public bool? Remitar { get; set; }

    public bool? Facturar { get; set; }

    public bool? Pendiente { get; set; }
}
