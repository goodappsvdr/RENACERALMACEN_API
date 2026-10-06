using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ItemsSucursales
{
    public int IdItemSucursal { get; set; }

    public int? IdItem { get; set; }

    public string? CodFabrica { get; set; }

    public int? IdSucursal { get; set; }

    public int? IdItemTipo { get; set; }

    public decimal? Stock { get; set; }

    public decimal? StockMinimo { get; set; }

    public decimal? StockMaximo { get; set; }

    public bool? MueveStock { get; set; }

    public decimal? Costo { get; set; }

    public decimal? Neto { get; set; }

    public decimal? Rentabilidad { get; set; }

    public int? IdImpuesto { get; set; }

    public decimal? Alicuota { get; set; }

    public decimal? PrecioVenta { get; set; }

    public string? Barcode { get; set; }

    public DateOnly? FechaCompra { get; set; }

    public DateOnly? FechaVenta { get; set; }

    public int? Estado { get; set; }

    public bool? EstaOferta { get; set; }

    public decimal? PrecioOferta { get; set; }

    public int? IdUbicacion { get; set; }

    public int? IdEmpresa { get; set; }

    public decimal? StockInicial { get; set; }
}
