using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Entidades
{
    public int IdEntidad { get; set; }

    public string? RazonSocial { get; set; }

    public int? IdCategoriaIva { get; set; }

    public string? Cuit { get; set; }

    public int? IdProvincia { get; set; }

    public int? IdLocalidad { get; set; }

    public string? Direccion { get; set; }

    public string? NroCalle { get; set; }

    public string? Telefono { get; set; }

    public string? Email { get; set; }

    public bool? CtaCte { get; set; }

    public decimal? LimiteCtaCte { get; set; }

    public int? IdEmpresa { get; set; }

    public int? IdEstado { get; set; }

    public string? Imagen { get; set; }

    public string? Lat { get; set; }

    public string? Lng { get; set; }

    public DateOnly? FechaCreacion { get; set; }

    public string? Observaciones { get; set; }

    public bool? EsHijo { get; set; }

    public int? IdPadre { get; set; }

    public string? Fantasia { get; set; }

    public int? IdEntidadCategoria { get; set; }

    public string? Url { get; set; }

    public byte? DiasInteres { get; set; }

    public short? InteresCliente { get; set; }

    public byte? DiasInteresDos { get; set; }
}
