using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ItemsImagenes
{
    public int IdItemImagen { get; set; }

    public int? IdItem { get; set; }

    public string? CodFabrica { get; set; }

    public string? Imagen { get; set; }

    public int? Orden { get; set; }

    public int? Estado { get; set; }
}
