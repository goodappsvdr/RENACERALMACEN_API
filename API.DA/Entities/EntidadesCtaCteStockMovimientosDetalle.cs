using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class EntidadesCtaCteStockMovimientosDetalle
{
    public int IdEntidadCtaCteStockMovimientoDetalle { get; set; }

    public int? IdEntidad { get; set; }

    public int? IdComprobante { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public string? Concepto { get; set; }

    public int? IdItem { get; set; }

    public decimal? Total { get; set; }

    public decimal? Saldo { get; set; }

    public decimal? Saldo2 { get; set; }

    public DateTime? Fecha { get; set; }

    public int? IdSucursal { get; set; }

    public int? IdComprobanteDetalle { get; set; }

    public int? IdComprobanteRelacion { get; set; }

    public int? IdComprobanteRelacionTipo { get; set; }

    public int? IdComprobanteRelacionDetalle { get; set; }
}
