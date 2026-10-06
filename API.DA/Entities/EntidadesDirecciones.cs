using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class EntidadesDirecciones
{
    public int IdEntidadDireccion { get; set; }

    public int? IdEntidad { get; set; }

    public string? Descripcion { get; set; }

    public string? Domicilio { get; set; }

    public int? IdLocalidad { get; set; }

    public int? IdProvincia { get; set; }

    public string? GeoLatitud { get; set; }

    public string? GeoLongitud { get; set; }
}
