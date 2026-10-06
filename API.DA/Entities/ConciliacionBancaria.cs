using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ConciliacionBancaria
{
    public int IdConciliacionBancaria { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public string? Letra { get; set; }

    public int? IdPuntoVenta { get; set; }

    public string? PuntoVenta { get; set; }

    public string? Numero { get; set; }

    public int? IdBancoCuenta { get; set; }

    public string? Cuenta { get; set; }

    public int? IdTipo { get; set; }

    public int? IdBanco { get; set; }

    public int? IdSucursal { get; set; }

    public decimal? TotalGeneral { get; set; }

    public DateTime? FechaEmision { get; set; }

    public int? IdUsuario { get; set; }

    public int? IdEmpresa { get; set; }

    public int? IdPlanillaCaja { get; set; }

    public int? Estado { get; set; }

    public int? IdTipoAjuste { get; set; }
}
