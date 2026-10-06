using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ComprobantesLetras
{
    public int IdComprobanteLetra { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public int? IdCategoriaIvacliente { get; set; }

    public int? IdCategoriaIvaproveedor { get; set; }

    public string? Letra { get; set; }

    public int? IdEmpresa { get; set; }
}
