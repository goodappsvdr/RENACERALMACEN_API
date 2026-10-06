using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class OfertasAgotamiento
{
    public int IdOferta { get; set; }

    public int IdItem { get; set; }

    public int IdSucursal { get; set; }

    public int? CantidadObjetivo { get; set; }

    public int? CantidadDisponible { get; set; }
}
