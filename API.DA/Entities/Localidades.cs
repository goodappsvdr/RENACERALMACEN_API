using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Localidades
{
    public int IdLocalidad { get; set; }

    public string? Descripcion { get; set; }

    public int? IdProvincia { get; set; }

    public string? Cp { get; set; }

    public int? Orden { get; set; }

    public int? Estado { get; set; }

    public virtual Estados? EstadoNavigation { get; set; }
}
