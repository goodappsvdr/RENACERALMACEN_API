using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Bancos
{
    public int IdBanco { get; set; }

    public string? RazonSocial { get; set; }

    public int? IdEstado { get; set; }
}
