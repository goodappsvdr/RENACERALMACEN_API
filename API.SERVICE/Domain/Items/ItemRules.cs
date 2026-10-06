using API.SERVICE.Domain.Exceptions;

namespace API.SERVICE.Domain.Items;

/// <summary>Reglas del ABM de ítems, tal como las aplica FrmItemsABM del WebForms.</summary>
public static class ItemRules
{
    /// <summary>Empresa fija: el ERP siempre graba ID_Empresa = 1.</summary>
    public const int IdEmpresa = 1;

    /// <summary>Parámetro que habilita pisar el stock al modificar un ítem.</summary>
    public const string CambiaStockCategoria = "CAMBIASTOCK";
    public const string CambiaStockNombre = "CAMBIASTOCK";

    // ID_Impuesto -> alícuota de IVA. Hardcodeado igual que en el WebForms.
    private static readonly IReadOnlyDictionary<int, decimal> AlicuotasPorImpuesto = new Dictionary<int, decimal>
    {
        [1] = 21m,
        [2] = 10.5m,
        [3] = 27m,
        [4] = 0m,
    };

    /// <summary>
    /// Alícuota de IVA del impuesto. El WebForms usaba 0 para cualquier impuesto desconocido;
    /// acá se rechaza para no grabar precios con IVA incorrecto.
    /// </summary>
    public static decimal AlicuotaDe(int idImpuesto) =>
        AlicuotasPorImpuesto.TryGetValue(idImpuesto, out var alicuota)
            ? alicuota
            : throw new BusinessException($"Impuesto {idImpuesto} inválido. Valores admitidos: {string.Join(", ", AlicuotasPorImpuesto.Keys)}.");

    /// <summary>
    /// Códigos de balanza (empiezan con "2"): se guardan solo los primeros 7 dígitos (prefijo + PLU),
    /// porque el resto del código impreso trae peso/precio y cambia en cada etiqueta.
    /// </summary>
    public static string? NormalizarCodigoBarras(string? codigoBarras)
    {
        if (string.IsNullOrEmpty(codigoBarras))
            return codigoBarras;

        return codigoBarras.StartsWith('2') && codigoBarras.Length > 7
            ? codigoBarras[..7]
            : codigoBarras;
    }

    /// <summary>El precio cambió si difiere a 2 decimales (el WebForms comparaba con FormatNumber(x, 2)).</summary>
    public static bool PrecioCambio(decimal? precioAnterior, decimal precioNuevo) =>
        Math.Round(precioAnterior ?? 0m, 2, MidpointRounding.AwayFromZero) != Math.Round(precioNuevo, 2, MidpointRounding.AwayFromZero);

    public static void ValidarSucursales(IReadOnlyCollection<int> idsSucursal)
    {
        if (idsSucursal.Count == 0)
            throw new BusinessException("El ítem tiene que estar al menos en una sucursal.");

        var repetidas = idsSucursal.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repetidas.Count > 0)
            throw new BusinessException($"Sucursales repetidas: {string.Join(", ", repetidas)}.");
    }
}
