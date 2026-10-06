using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Transportes
{
    public int IdTransporte { get; set; }

    public string? RazonSocial { get; set; }

    public int? IdCategoriaIva { get; set; }

    public string? NroDoc { get; set; }

    public int? IdProvincia { get; set; }

    public int? IdLocalidad { get; set; }

    public string? Calle { get; set; }

    public string? Nro { get; set; }

    public string? Telefono { get; set; }

    public string? Mail { get; set; }

    public int? IdEmpresa { get; set; }

    public int? Estado { get; set; }
}
