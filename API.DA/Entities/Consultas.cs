using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Consultas
{
    public int IdConsulta { get; set; }

    public string Nombre { get; set; } = null!;

    public string Telefono { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string NombreEmpresa { get; set; } = null!;

    public string RubroEmpresa { get; set; } = null!;

    public string Sucursales { get; set; } = null!;

    public int IdLocalidad { get; set; }

    public int IdProvincia { get; set; }

    public string Observaciones { get; set; } = null!;

    public DateTime Fecha { get; set; }
}
