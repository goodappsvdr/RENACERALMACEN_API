using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ComprobantesCarga
{
    public int IdComprobanteCarga { get; set; }

    public int? IdComprobante { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public DateTime? FechaCarga { get; set; }

    public int? IdUsuario { get; set; }
}
