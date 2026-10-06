using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ItemsPreciosActualizacion
{
    public int IdItemPrecioActulizacion { get; set; }

    public int? IdItem { get; set; }

    public decimal? PrecioActual { get; set; }

    public decimal? PrecioNuevo { get; set; }

    public decimal? Rentabilidad { get; set; }

    public DateTime? FechaActualizacion { get; set; }

    public int? IdUsario { get; set; }
}
