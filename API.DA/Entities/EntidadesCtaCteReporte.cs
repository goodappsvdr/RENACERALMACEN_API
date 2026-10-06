using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class EntidadesCtaCteReporte
{
    public int IdEntidadCtaCteReporte { get; set; }

    public string? Nro { get; set; }

    public string? Concepto { get; set; }

    public DateTime? Fecha { get; set; }

    public DateTime? Vencimiento { get; set; }

    public decimal? Debe { get; set; }

    public decimal? Haber { get; set; }

    public decimal? Total { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public int? IdComprobante { get; set; }

    public string? Observaciones { get; set; }

    public string? Estado { get; set; }
}
