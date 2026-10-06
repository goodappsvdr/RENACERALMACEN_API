using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ProveedoresRecibos
{
    public int IdProveedorRecibo { get; set; }

    public int? IdProveedor { get; set; }

    public string? Letra { get; set; }

    public string? PuntoVenta { get; set; }

    public string? Numero { get; set; }

    public string? RazonSocial { get; set; }

    public int? IdCategoriaIva { get; set; }

    public string? NroDoc { get; set; }

    public DateTime? FechaEmision { get; set; }

    public int? IdUsuario { get; set; }

    public int? IdEmpresa { get; set; }

    public int? IdPlanillaCaja { get; set; }

    public decimal? Total { get; set; }

    public int? Estado { get; set; }

    public int? IdOrdenPagoTipo { get; set; }

    public string? Observaciones { get; set; }

    public int? IdSucursal { get; set; }
}
