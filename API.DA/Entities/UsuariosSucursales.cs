using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class UsuariosSucursales
{
    public int IdUsuarioSucursal { get; set; }

    public int? IdUsuario { get; set; }

    public int? IdSucursal { get; set; }
}
