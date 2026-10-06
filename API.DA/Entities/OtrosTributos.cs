using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class OtrosTributos
{
    public int IdOtrosTributos { get; set; }

    public string? Codigo { get; set; }

    public string? Descripcion { get; set; }

    public string? Observaciones { get; set; }

    public int? Estado { get; set; }
}
