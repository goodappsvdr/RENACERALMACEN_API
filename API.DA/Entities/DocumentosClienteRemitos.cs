using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class DocumentosClienteRemitos
{
    public int IdDocumentoClienteRemito { get; set; }

    public int? IdDocumentoCliente { get; set; }

    public int? IdRemito { get; set; }

    public int? IdComprobanteTipo { get; set; }
}
