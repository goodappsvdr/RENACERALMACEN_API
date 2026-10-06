using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class MarcasActualizacion
{
    public int IdMarcaActualizacion { get; set; }

    public int? IdMarca { get; set; }

    public decimal? Alicuota { get; set; }

    public int? IdUsuario { get; set; }

    public DateTime? FechaActualizacion { get; set; }
}
