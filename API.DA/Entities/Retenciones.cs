using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Retenciones
{
    public int IdRetencion { get; set; }

    public int? IdRetencionTipo { get; set; }

    public string? Descripcion { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public int? IdComprobante { get; set; }

    public string? NroComprobante { get; set; }

    public DateTime? FechaEmision { get; set; }

    public DateTime? FechaRecepcion { get; set; }

    public decimal? Total { get; set; }

    public int? Estado { get; set; }

    public int? IdEntidad { get; set; }
}
