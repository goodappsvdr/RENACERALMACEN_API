using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Impuestos
{
    public int IdImpuesto { get; set; }

    public string? Descripcion { get; set; }

    public string? Reducida { get; set; }

    public bool? EsIva { get; set; }

    public bool? AplicaAlNeto { get; set; }

    public bool? AplicaAlTotal { get; set; }

    public bool? EsAlicuota { get; set; }

    public decimal? Alicuota { get; set; }

    public bool? EsMontoFijo { get; set; }

    public decimal? Importe { get; set; }

    public bool? EsPorCantidad { get; set; }

    public bool? EsPorItem { get; set; }

    public int? CuentaDebe { get; set; }

    public int? CuentaHaber { get; set; }

    public int? Estado { get; set; }
}
