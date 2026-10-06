using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Usuarios
{
    public int IdUsuario { get; set; }

    public string? Nombre { get; set; }

    public string? Email { get; set; }

    public string? Usuario { get; set; }

    public string? Pass { get; set; }

    public Guid? UserId { get; set; }

    public string? Token { get; set; }

    public string? Imagen { get; set; }

    public int? IdSucursal { get; set; }

    public int? IdEstado { get; set; }
}
