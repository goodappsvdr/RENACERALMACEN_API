using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class SubRubrosActualizacion
{
    public int IdSubRubroActualizacion { get; set; }

    public int? IdSubRubro { get; set; }

    public decimal? Alicuota { get; set; }

    public int? IdUsuario { get; set; }

    public DateTime? FechaActualizacion { get; set; }
}
