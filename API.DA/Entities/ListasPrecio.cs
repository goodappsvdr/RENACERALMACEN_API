using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ListasPrecio
{
    public int IdListaPrecio { get; set; }

    public string? Descripcion { get; set; }

    public int? IdEmpresa { get; set; }

    public string? Detalle { get; set; }

    public decimal? Alicuota { get; set; }

    public bool? Defecto { get; set; }

    public int? Estado { get; set; }
}
