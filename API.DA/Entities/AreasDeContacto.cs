using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class AreasDeContacto
{
    public int IdAreaDeContacto { get; set; }

    public string? Descripcion { get; set; }

    public string? Observaciones { get; set; }

    public int? IdEstado { get; set; }
}
