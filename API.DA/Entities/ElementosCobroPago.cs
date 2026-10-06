using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ElementosCobroPago
{
    public int IdElementroCobroPago { get; set; }

    public int? IdTipo { get; set; }

    public string? Descripcion { get; set; }

    public string? Reducida { get; set; }

    public bool? HabilitaMoneda { get; set; }

    public int? IdEmpresa { get; set; }

    public int? CuentaDebe { get; set; }

    public int? CuentaHaber { get; set; }

    public int? Estado { get; set; }
}
