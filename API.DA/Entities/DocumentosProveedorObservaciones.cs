using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class DocumentosProveedorObservaciones
{
    public int IdDocumentoProveedorObservacion { get; set; }

    public int? IdDocumentoProveedor { get; set; }

    public string? Observaciones { get; set; }
}
