using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class EntidadesAreasDeContacto
{
    public int IdEntidadAreaDeContacto { get; set; }

    public int? IdEntidad { get; set; }

    public int? IdAreaDeContacto { get; set; }

    public int? IdMedioDeContacto { get; set; }

    public string? EmailNumero { get; set; }

    public string? NombreDeContacto { get; set; }
}
