namespace API.SERVICE.Security;

/// <summary>Sección "Jwt" de configuración. La Key NUNCA va en appsettings: user-secrets / variables de entorno.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ELRENACERALMACEN_API";

    public string Audience { get; set; } = "ELRENACERALMACEN";

    public string Key { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = 480;
}
