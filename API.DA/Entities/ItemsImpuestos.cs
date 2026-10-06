using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ItemsImpuestos
{
    public int IdItemImpuesto { get; set; }

    public int? IdItem { get; set; }

    public int? IdImpuesto { get; set; }
}
