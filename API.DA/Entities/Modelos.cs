using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Modelos
{
    public int IdModelo { get; set; }

    public string? Descripcion { get; set; }

    public int? IdMarca { get; set; }

    public int? IdEmpresa { get; set; }

    public int? Estado { get; set; }
}
