using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class DocumentosClienteQr
{
    public int IdDocumentoClienteQr { get; set; }

    public int? IdDocumentoCliente { get; set; }

    public string? CodigoQr { get; set; }

    public string? Url { get; set; }
}
