using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ItemsNroSeries
{
    public int IdItemNroSerie { get; set; }

    public int? IdItem { get; set; }

    public string? NroSerie { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public int? IdComprobante { get; set; }

    public int? IdDocumentoClienteDetalle { get; set; }

    public int? IdSucursal { get; set; }

    public int? Estado { get; set; }
}
