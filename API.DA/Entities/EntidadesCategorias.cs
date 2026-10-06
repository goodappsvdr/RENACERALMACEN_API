using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class EntidadesCategorias
{
    public int IdEntidadCategoria { get; set; }

    public string? Descripcion { get; set; }

    public int? IdEmpresa { get; set; }

    public int? Estado { get; set; }
}
