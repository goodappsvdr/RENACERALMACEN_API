using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class EntidadOrdenPagoDocumentosProveedores
{
    public int IdEntidadOrdenPagoDocumentoProveedor { get; set; }

    public int? IdEntidadOrdenPago { get; set; }

    public int? IdEntidad { get; set; }

    public string? NumeroOrdenPago { get; set; }

    public decimal? ImporteOrdenPago { get; set; }

    public int? IdDocumentoProveedor { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public string? NumeroComprobante { get; set; }

    public decimal? ImporteComprobante { get; set; }

    public decimal? Saldo { get; set; }
}
