using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class DocumentosClienteDetalleEnvases
{
    public int IdDocumentoClienteDetalleEnvase { get; set; }

    public int? IdDocumentoClienteDetalle { get; set; }

    public int? IdEnvase { get; set; }

    public decimal? Cantidad { get; set; }

    public decimal? PesoEnvase { get; set; }

    public decimal? Total { get; set; }
}
