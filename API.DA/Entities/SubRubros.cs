using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class SubRubros
{
    public int IdSubRubro { get; set; }

    public string? Descripcion { get; set; }

    public int? IdRubro { get; set; }

    public int? IdEmpresa { get; set; }

    public int? Estado { get; set; }
}
