using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Motivos
{
    public int IdMotivo { get; set; }

    public string? Descripcion { get; set; }

    public string? IdEmpresa { get; set; }

    public int? Estado { get; set; }
}
