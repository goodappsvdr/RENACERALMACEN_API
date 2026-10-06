using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Estados
{
    public int IdEstado { get; set; }

    public string? Categoria { get; set; }

    public string? Nombre { get; set; }

    public string? Descripcion { get; set; }

    public string? Imagen { get; set; }

    public bool? Activo { get; set; }

    public virtual ICollection<Localidades> Localidades { get; set; } = new List<Localidades>();

    public virtual ICollection<Paises> Paises { get; set; } = new List<Paises>();

    public virtual ICollection<Provincias> Provincias { get; set; } = new List<Provincias>();
}
