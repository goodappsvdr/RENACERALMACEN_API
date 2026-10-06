using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Vehiculos
{
    public int IdVehiculo { get; set; }

    public string? Dominio { get; set; }

    public int? IdVehiculoTipo { get; set; }

    public int? IdTransporte { get; set; }

    public int? IdVehiculoPadre { get; set; }

    public int IdSujeto { get; set; }

    public int? Marca { get; set; }

    public int? Modelo { get; set; }

    public int? Anio { get; set; }

    public int? Color { get; set; }

    public string? Observaciones { get; set; }

    public DateTime? FechaAlta { get; set; }

    public DateTime? FechaBaja { get; set; }

    public int? Estado { get; set; }

    public int? IdCentro { get; set; }

    public DateTime? FechaRuta { get; set; }

    public DateTime? SeguroCarga { get; set; }

    public DateTime? InspTecnica { get; set; }

    public DateTime? RespCivil { get; set; }

    public int? Seguimiento { get; set; }
}
