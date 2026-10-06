using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class OrdenesDepositosDetalle
{
    public int IdOrdenDepositoDetalle { get; set; }

    public int? IdOrdendeposito { get; set; }

    public int? IdElementoCobroPago { get; set; }

    public string? Descripcion { get; set; }

    public string? Detalle { get; set; }

    public int? IdBanco { get; set; }

    public int? IdSucursal { get; set; }

    public string? Banco { get; set; }

    public string? Sucursal { get; set; }

    public DateTime? Recepcion { get; set; }

    public DateTime? Emision { get; set; }

    public DateTime? Vto { get; set; }

    public string? Nro { get; set; }

    public int? IdElemento { get; set; }

    public decimal? Total { get; set; }
}
