using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class EntidadRecibosDocumentosCliente
{
    public int IdEntidadReciboDocumentoCliente { get; set; }

    public int? IdEntidadRecibo { get; set; }

    public int? IdEntidad { get; set; }

    public string? NumeroRecibo { get; set; }

    public decimal? ImporteRecibo { get; set; }

    public int? IdDocumentoCliente { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public string? NumeroComprobante { get; set; }

    public decimal? ImporteComprobante { get; set; }

    public decimal? Saldo { get; set; }
}
