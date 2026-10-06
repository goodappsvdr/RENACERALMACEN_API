using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace API.SERVICE.Services.Cache;

/// <summary>
/// Implementación sobre <see cref="IDistributedCache"/> (Redis en ambientes con ConnectionStrings:Redis,
/// memoria en local). Guarda <c>byte[]</c> con System.Text.Json camelCase.
/// </summary>
public sealed class RedisCacheService : IRedisCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Lock por key (no global) para evitar cache stampede sin penalizar el resto de las keys.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> KeyLocks = new();

    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = await _cache.GetAsync(key, cancellationToken);
            return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, JsonOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fallback: un cache caído se comporta como miss. Nunca se loguea el payload.
            _logger.LogWarning(ex, "Cache GET falló para {CacheKey}; se continúa contra la base", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), "El TTL es obligatorio (no se permite cache infinito).");

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
            await _cache.SetAsync(key, bytes, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Cache SET falló para {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Si no se pudo invalidar, el TTL acota cuánto tiempo puede quedar el dato viejo.
            _logger.LogWarning(ex, "Cache REMOVE falló para {CacheKey}", key);
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var keyLock = KeyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check: otro request pudo haber cargado la key mientras esperábamos.
            cached = await GetAsync<T>(key, cancellationToken);
            if (cached is not null)
                return cached;

            var value = await factory(cancellationToken);
            await SetAsync(key, value, ttl, cancellationToken);
            return value;
        }
        finally
        {
            keyLock.Release();
        }
    }
}
