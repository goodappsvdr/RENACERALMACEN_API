using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class BancosSucursales
{
    public int IdBancoSucursal { get; set; }

    public int? IdBanco { get; set; }

    public string? Descripcion { get; set; }

    public int? IdEstado { get; set; }
}
