using System.ComponentModel.DataAnnotations;

namespace API.SERVICE.Models.Items;

/// <summary>Datos comunes del alta y la modificación de un ítem (formulario de FrmItemsABM).</summary>
public abstract class ItemAbmDtoBase
{
    /// <summary>Se guarda en mayúsculas. Máximo 50: el SP de modificación del ERP trunca a 50 en silencio.</summary>
    [Required, MaxLength(50)]
    public string Descripcion { get; set; } = string.Empty;

    [Required] public int? IdItemTipo { get; set; }
    [Required] public int? IdRubro { get; set; }
    [Required] public int? IdSubRubro { get; set; }
    [Required] public int? IdMarca { get; set; }
    [Required] public int? IdModelo { get; set; }
    [Required] public int? IdUbicacion { get; set; }
    [Required] public int? IdUnidadMedida { get; set; }

    /// <summary>ID_Impuesto: 1 = IVA 21%, 2 = 10,5%, 3 = 27%, 4 = 0%.</summary>
    [Required] public int? IdImpuesto { get; set; }

    [Required] public int? IdEstado { get; set; }

    [MaxLength(50)]
    public string? CodigoFabrica { get; set; }

    /// <summary>Los códigos de balanza (empiezan con "2") se guardan con sus primeros 7 dígitos.</summary>
    [MaxLength(50)]
    public string? CodigoBarras { get; set; }

    public bool MueveStock { get; set; }

    [Range(0, double.MaxValue)] public decimal StockActual { get; set; }
    [Range(0, double.MaxValue)] public decimal StockMinimo { get; set; }
    [Range(0, double.MaxValue)] public decimal StockMaximo { get; set; }

    [Range(0, double.MaxValue)] public decimal Costo { get; set; }
    [Range(0, double.MaxValue)] public decimal Neto { get; set; }
    public decimal Rentabilidad { get; set; }

    /// <summary>Precio de venta final (ItemsSucursales.PrecioVenta).</summary>
    [Range(0, double.MaxValue)] public decimal PrecioUnitario { get; set; }

    public bool MostrarWeb { get; set; }

    public string? Detalle { get; set; }
}

public sealed class CreateItemDto : ItemAbmDtoBase
{
    /// <summary>Sucursales donde se da de alta el ítem (una fila de ItemsSucursales por cada una).</summary>
    [Required, MinLength(1)]
    public List<ItemSucursalAltaDto> Sucursales { get; set; } = [];
}

public sealed class UpdateItemDto : ItemAbmDtoBase
{
    /// <summary>
    /// Sucursales a actualizar. Como en el ERP, solo se modifican las que el ítem ya tiene;
    /// el stock informado se aplica únicamente si el parámetro CAMBIASTOCK vale 1.
    /// </summary>
    [Required, MinLength(1)]
    public List<ItemSucursalStockDto> Sucursales { get; set; } = [];
}

public sealed class ItemSucursalAltaDto
{
    [Required] public int? IdSucursal { get; set; }

    [Range(0, double.MaxValue)] public decimal Stock { get; set; }

    [Required] public int? Estado { get; set; }
}

public sealed class ItemSucursalStockDto
{
    [Required] public int? IdSucursal { get; set; }

    [Range(0, double.MaxValue)] public decimal Stock { get; set; }
}
