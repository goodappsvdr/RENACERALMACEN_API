namespace API.SERVICE.Domain.Ventas;

/// <summary>Valores fijos del ERP para los comprobantes de venta (FrmFacturas y SPs asociados).</summary>
public static class VentaRules
{
    /// <summary>Los comprobantes internos (VEN) se emiten con letra X.</summary>
    public const string LetraInterna = "X";

    /// <summary>ID_Condicion con el que el ERP graba todo comprobante de venta (hardcodeado).</summary>
    public const int Condicion = 14;

    /// <summary>Estado con el que el ERP graba la venta nueva ("ESTADO GENERADO", hardcodeado).</summary>
    public const int EstadoVentaNueva = 42;

    /// <summary>Estados desde los que el ERP permite anular (hardcodeados en Editar_Ws).</summary>
    public static readonly int[] EstadosAnulables = [42, 104];

    /// <summary>Tipo de oferta "por agotamiento": descuenta unidades disponibles de la oferta.</summary>
    public const int OfertaPorAgotamiento = 72;

    /// <summary>Estado de oferta activa (hardcodeado en ItemsOfertas_BuscarPorSucursalItem).</summary>
    public const int EstadoOfertaActiva = 198;

    /// <summary>Tipos de comprobante cuyo stock pendiente de remitar/facturar mira el SP (..._Pendiente_Remitar).</summary>
    public static readonly int[] TiposConStockPendiente = [2, 3, 11, 1];

    /// <summary>Concepto con el que el ERP identifica el comprobante en cta. cte. y movimientos: "VEN -X-0001-00000123".</summary>
    public static string Concepto(string prefijo, string letra, string puntoVenta, string numero) =>
        $"{prefijo} -{letra}-{puntoVenta}-{numero}";

    public static string? Truncar(string? valor, int largo) =>
        valor is { Length: > 0 } && valor.Length > largo ? valor[..largo] : valor;
}
