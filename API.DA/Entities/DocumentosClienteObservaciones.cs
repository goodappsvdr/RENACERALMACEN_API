using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class DocumentosClienteObservaciones
{
    public int IdDocumentoClienteObservacion { get; set; }

    public int? IdDocumentoCliente { get; set; }

    public string? Observaciones { get; set; }
}
