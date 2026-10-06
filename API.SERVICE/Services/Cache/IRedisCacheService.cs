namespace API.SERVICE.Services.Cache;

/// <summary>
/// Contrato de cache (estándar GoodApps Redis). Redis es cache, no base de datos:
/// si falla, las lecturas degradan a la base y las escrituras de cache se ignoran.
/// </summary>
public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Patrón preferido: hit → retorna; miss → factory (DB) → setea con TTL → retorna. Con lock por key.</summary>
    Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default);
}
