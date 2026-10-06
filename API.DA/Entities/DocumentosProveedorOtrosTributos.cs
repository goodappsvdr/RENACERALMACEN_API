using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class DocumentosProveedorOtrosTributos
{
    public int IdDocumentoProveedorOtroTributo { get; set; }

    public int? IdDocumentoProveedor { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public int? IdOtroTributo { get; set; }

    public string? Detalle { get; set; }

    public decimal? BaseImponible { get; set; }

    public decimal? Alicuota { get; set; }

    public decimal? Total { get; set; }
}
