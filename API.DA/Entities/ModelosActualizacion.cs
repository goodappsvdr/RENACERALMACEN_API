using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ModelosActualizacion
{
    public int IdModeloActualizacion { get; set; }

    public int? IdModelo { get; set; }

    public decimal? Alicuota { get; set; }

    public int? IdUsuario { get; set; }

    public DateTime? FechaActualizacion { get; set; }
}
