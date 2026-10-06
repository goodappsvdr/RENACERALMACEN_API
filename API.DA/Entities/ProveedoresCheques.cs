using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ProveedoresCheques
{
    public int IdProveedorCheque { get; set; }

    public int? IdProveedor { get; set; }

    public int? IdBanco { get; set; }

    public int? IdSucursal { get; set; }

    public string? Nro { get; set; }

    public decimal? Importe { get; set; }

    public DateTime? FechaRecepcion { get; set; }

    public DateTime? FechaEmision { get; set; }

    public DateTime? FechaVencimiento { get; set; }

    public decimal? ValorToma { get; set; }

    public int? IdEmpresa { get; set; }

    public int? IdUsuario { get; set; }

    public int? Tipo { get; set; }

    public int? IdProveedorRecibo { get; set; }

    public int? Estado { get; set; }

    public int? IdClienteRecibo { get; set; }

    public int? IdComprobanteTipo { get; set; }
}
