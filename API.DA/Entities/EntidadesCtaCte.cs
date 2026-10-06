using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class EntidadesCtaCte
{
    public long IdEntidadCtaCte { get; set; }

    public int? IdEntidad { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public int? IdComprobante { get; set; }

    public string? Concepto { get; set; }

    public int? NroCuota { get; set; }

    public decimal? Total { get; set; }

    public decimal? Saldo { get; set; }

    public bool? Cancelado { get; set; }

    public DateTime? Fecha { get; set; }

    public DateTime? FechaVencimiento { get; set; }

    public DateTime? FechaAnulacion { get; set; }

    public DateTime? FechaPago { get; set; }

    public decimal? InteresAplicado { get; set; }

    public int? Estado { get; set; }

    public decimal? Total2 { get; set; }

    public int? IdEmpresa { get; set; }

    public int? IdSucursal { get; set; }

    public int? IdUsuario { get; set; }
}
