using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class EntidadesCtaCteMovimientos
{
    public long IdEntidadCtaCteMovimiento { get; set; }

    public long? IdEntidadCtaCte { get; set; }

    public string? Concepto { get; set; }

    public decimal? AfavorEntidad { get; set; }

    public decimal? EnContraEntidad { get; set; }

    public DateTime? Fecha { get; set; }

    public int? IdElementoCobroPago { get; set; }

    public int? IdElemento { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public int? IdComprobante { get; set; }
}
