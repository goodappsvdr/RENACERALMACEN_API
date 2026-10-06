using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ItemsOfertas
{
    public int IdItemOferta { get; set; }

    public int? IdItems { get; set; }

    public decimal? Precio { get; set; }

    public bool? Web { get; set; }
}
