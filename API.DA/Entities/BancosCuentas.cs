using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class BancosCuentas
{
    public int IdBancoCuenta { get; set; }

    public int? IdBanco { get; set; }

    public int? IdBancoSucursal { get; set; }

    public int? IdCuentaTipo { get; set; }

    public string? NroCuenta { get; set; }

    public int? Estado { get; set; }
}
