using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Tarjetas
{
    public int IdTarjeta { get; set; }

    public string? Descripcion { get; set; }

    public int? IdEstado { get; set; }
}
