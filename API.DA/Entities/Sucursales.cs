using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Sucursales
{
    public int IdSucursal { get; set; }

    public string? Descripcion { get; set; }

    public string? RazonSocial { get; set; }

    public string? Fantasia { get; set; }

    public string? PuntoVenta { get; set; }

    public int? IdEmpresa { get; set; }

    public DateTime? FechaAlta { get; set; }

    public string? Direccion { get; set; }

    public string? Nro { get; set; }

    public string? Telefono { get; set; }

    public string? Email { get; set; }

    public string? Cuit { get; set; }

    public string? Iibb { get; set; }

    public string? PuntoVentaAfip { get; set; }

    public int? IdProvincia { get; set; }

    public int? IdLocalidad { get; set; }

    public int? IdCategoriaIva { get; set; }

    public string? GeoLatitud { get; set; }

    public string? GeoLongitud { get; set; }

    public int? Estado { get; set; }

    public string? Logo { get; set; }

    public virtual ICollection<Combos> Combos { get; set; } = new List<Combos>();
}
