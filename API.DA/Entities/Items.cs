using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class Items
{
    public int IdItem { get; set; }

    public string? Descripcion { get; set; }

    public int? IdItemTipo { get; set; }

    public int? IdEmpresa { get; set; }

    public int? IdRubro { get; set; }

    public int? IdSubRubro { get; set; }

    public int? IdMarca { get; set; }

    public int? IdModelo { get; set; }

    public DateTime? FechaCompra { get; set; }

    public DateTime? FechaVenta { get; set; }

    public int? IdUnidadMedida { get; set; }

    public string? CodFabrica { get; set; }

    public bool? TieneDetalle { get; set; }

    public bool? MueveStock { get; set; }

    public decimal? StockMaximo { get; set; }

    public decimal? StockMinimo { get; set; }

    public decimal? StockActual { get; set; }

    public int? IdUbicacion { get; set; }

    public decimal? UnidadesXbulto { get; set; }

    public string? Barcode { get; set; }

    public decimal? Neto { get; set; }

    public int? CuentaDebe { get; set; }

    public int? CuentaHaber { get; set; }

    public decimal? MtsKgs { get; set; }

    public int? Estado { get; set; }

    public bool? EsDolar { get; set; }

    public decimal? Rentabilidad { get; set; }

    public decimal? StockInicial { get; set; }

    public bool? MostrarWeb { get; set; }

    public string? Detalle { get; set; }

    public virtual ICollection<ProductosCombos> ProductosCombos { get; set; } = new List<ProductosCombos>();
}
