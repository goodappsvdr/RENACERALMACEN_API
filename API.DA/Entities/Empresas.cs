using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Empresas
{
    public int IdEmpresa { get; set; }

    public string? RazonSocial { get; set; }

    public string? Fantasia { get; set; }

    public int? IdCategoriaIva { get; set; }

    public string? NroCuit { get; set; }

    public int? IdProvincia { get; set; }

    public int? IdLocalidad { get; set; }

    public string? Calle { get; set; }

    public string? Nro { get; set; }

    public DateTime? FechaAlta { get; set; }

    public bool? Admin { get; set; }

    public string? Imagen { get; set; }

    public int? IdEmpresaTipo { get; set; }

    public string? Email { get; set; }

    public string? Telefono { get; set; }

    public int? Estado { get; set; }

    public string? Iibb { get; set; }
}
