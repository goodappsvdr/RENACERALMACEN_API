using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class EntidadesFacturacion
{
    public int IdEntidadFacturacion { get; set; }

    public int? IdEntidad { get; set; }

    public string? RazonSocial { get; set; }

    public string? Fantasia { get; set; }

    public string? Cuit { get; set; }

    public int? IdCategoriaIva { get; set; }

    public int? IdProvincia { get; set; }

    public int? IdLocalidad { get; set; }

    public string? Domicilio { get; set; }

    public string? DomicilioNro { get; set; }

    public string? GeoLatitud { get; set; }

    public string? GeoLongitud { get; set; }
}
