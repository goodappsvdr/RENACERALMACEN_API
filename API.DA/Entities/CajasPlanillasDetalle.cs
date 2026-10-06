using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class CajasPlanillasDetalle
{
    public long IdCajaPlanillaDetalle { get; set; }

    public int? IdCajaPlanilla { get; set; }

    public int? IdElementoCobro { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public int? IdComprobante { get; set; }

    public string? Reducida { get; set; }

    public string? Descripcion { get; set; }

    public string? Obsevaciones { get; set; }

    public bool? Automatico { get; set; }

    public DateTime? Fecha { get; set; }

    public decimal? Total { get; set; }

    public decimal Debe { get; set; }

    public decimal? Haber { get; set; }

    public decimal? Total2 { get; set; }
}
