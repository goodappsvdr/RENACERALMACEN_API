using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Parametros
{
    public int IdParametro { get; set; }

    public string? Categoria { get; set; }

    public string? Nombre { get; set; }

    public string? Valor { get; set; }

    public string? Descripcion { get; set; }

    public int? Orden { get; set; }

    public int? IdEmpresa { get; set; }

    public bool? Activo { get; set; }
}
