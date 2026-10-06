using System.Globalization;

namespace API.SERVICE.Domain;

/// <summary>Formato de importes para mensajes al usuario: siempre es-AR, sin depender de la cultura del servidor.</summary>
public static class Formato
{
    private static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");

    /// <summary>1234.5 → "1.234,50".</summary>
    public static string Importe(decimal importe) => importe.ToString("N2", Argentina);
}
