using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class DocumentosProveedorDetalle
{
    public long IdDocumentoProveedorDetalle { get; set; }

    public long? IdDocumentoProveedor { get; set; }

    public int? IdItem { get; set; }

    public string? Descripcion { get; set; }

    public decimal? Cantidad { get; set; }

    public string? ListaPrecio { get; set; }

    public decimal? PrecioUnitario { get; set; }

    public decimal? Neto { get; set; }

    public decimal? Ivaalic { get; set; }

    public decimal? Iva { get; set; }

    public decimal? Otros { get; set; }

    public decimal? Total { get; set; }

    public int? IdImpuestoIva { get; set; }

    public int? IdListaPrecio { get; set; }

    public decimal? Metros { get; set; }

    public int? EstadoLibroIva { get; set; }

    public string? Observaciones { get; set; }

    public decimal? Bonificacion { get; set; }
}
