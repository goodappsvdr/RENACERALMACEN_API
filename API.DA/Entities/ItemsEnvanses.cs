using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ItemsEnvanses
{
    public int IdItemEnvase { get; set; }

    public int? IdItem { get; set; }

    public int? IdEnvase { get; set; }

    public decimal? CantidadKg { get; set; }

    public decimal? CantidadEnvases { get; set; }

    public decimal? PesoEnvase { get; set; }

    public bool? Activo { get; set; }
}
