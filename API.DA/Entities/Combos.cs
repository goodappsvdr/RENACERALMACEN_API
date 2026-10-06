using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Combos
{
    public int IdCombo { get; set; }

    public string? Nombre { get; set; }

    public decimal? PrecioCombo { get; set; }

    public DateTime? FechaInicio { get; set; }

    public DateTime? FechaFin { get; set; }

    public int? IdSucursal { get; set; }

    public virtual Sucursales? IdSucursalNavigation { get; set; }

    public virtual ICollection<ProductosCombos> ProductosCombos { get; set; } = new List<ProductosCombos>();
}
