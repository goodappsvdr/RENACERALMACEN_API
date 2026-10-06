namespace API.SERVICE.Services.Cache;

/// <summary>
/// Keys con formato estándar <c>{app}:{service}:{domain}:{feature}:{identifier}:v{n}</c>.
/// Subir la versión cuando cambie el shape del DTO cacheado.
/// </summary>
public static class CacheKeys
{
    private const string Prefix = "goodapps:elrenacer";

    public static string For(string domain, string feature, string identifier, int version = 1) =>
        $"{Prefix}:{Normalize(domain)}:{Normalize(feature)}:{identifier}:v{version}";

    private static string Normalize(string value) => value.Replace(' ', '-').ToLowerInvariant();
}

/// <summary>TTL de referencia del estándar (sección 7).</summary>
public static class CacheTtl
{
    /// <summary>Catálogos estáticos: 6–24 h.</summary>
    public static readonly TimeSpan Catalog = TimeSpan.FromHours(12);

    /// <summary>Config / parámetros: 5–30 min.</summary>
    public static readonly TimeSpan Config = TimeSpan.FromMinutes(15);
}
