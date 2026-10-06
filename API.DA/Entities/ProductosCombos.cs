using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ProductosCombos
{
    public int IdComboProducto { get; set; }

    public int? IdCombo { get; set; }

    public int? IdItem { get; set; }

    public int? Cantidad { get; set; }

    public virtual Combos? IdComboNavigation { get; set; }

    public virtual Items? IdItemNavigation { get; set; }
}
