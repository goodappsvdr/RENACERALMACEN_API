using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class DocumentosProveedorRemitos
{
    public int IdDocumentoProveedorRemito { get; set; }

    public int? IdDocumentoProveedor { get; set; }

    public int? IdRemito { get; set; }

    public int? IdComprobanteTipo { get; set; }
}
