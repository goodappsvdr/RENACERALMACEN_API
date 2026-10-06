using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class DocumentosClienteVencimientos
{
    public int IdDocumentoClienteVencimiento { get; set; }

    public int? IdDocumentoCliente { get; set; }

    public DateTime? FechaVencimiento { get; set; }
}
