using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ComprobantesTipo
{
    public int IdComprobanteTipo { get; set; }

    public string? Descripcion { get; set; }

    public string? Reducida { get; set; }

    public bool? LibroIva { get; set; }

    public int? IdEmpresa { get; set; }

    public int? Estado { get; set; }

    public int? Columna { get; set; }
}
