using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class EntidadesContacto
{
    public int IdEntidadContacto { get; set; }

    public int? IdEntidad { get; set; }

    public string? Descripcion { get; set; }

    public string? Valor { get; set; }
}
