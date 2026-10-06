using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class DocumentosClienteFotosApp
{
    public int IdDocumentoClienteFotoApp { get; set; }

    public int? IdDocumentoCliente { get; set; }

    public string? FotoTransporte { get; set; }

    public string? FotoBalanza { get; set; }
}
