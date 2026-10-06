using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Ofertas
{
    public int IdOferta { get; set; }

    public int IdItem { get; set; }

    public string Nombre { get; set; } = null!;

    public int TipoOferta { get; set; }

    public decimal Descuento { get; set; }

    public DateTime FechaInicio { get; set; }

    public DateTime FechaFin { get; set; }

    public int? CantidadRequerida { get; set; }

    public int? CantidadGratis { get; set; }

    public int IdEstado { get; set; }

    public decimal? CantidadStock { get; set; }

    public decimal? PrecioConDescuento { get; set; }
}
