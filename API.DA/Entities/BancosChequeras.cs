using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class BancosChequeras
{
    public int IdBancosChequeras { get; set; }

    public int? IdBancoCuenta { get; set; }

    public decimal? Cantidad { get; set; }

    public long? Desde { get; set; }

    public long? Hasta { get; set; }

    public int? Estado { get; set; }
}
