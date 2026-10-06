using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ItemsMovimientosDetalles
{
    public int IdItemMovientoDetalle { get; set; }

    public int? IdItem { get; set; }

    public int? IdComprobante { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public int? IdComprobanteDetalle { get; set; }

    public DateTime? FechaAlta { get; set; }

    public int? IdUsuario { get; set; }

    public int? IdSucursal { get; set; }

    public string? Concepto { get; set; }

    public string? Item { get; set; }

    public decimal? Total { get; set; }

    public decimal? Debe { get; set; }

    public decimal? Haber { get; set; }

    public decimal? Total2 { get; set; }

    public bool? Automatico { get; set; }
}
