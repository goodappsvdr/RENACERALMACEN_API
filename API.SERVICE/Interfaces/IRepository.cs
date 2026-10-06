using API.SERVICE.Models.Common;

namespace API.SERVICE.Interfaces;

/// <summary>Consulta paginada con filtros tipados. Lo implementan todas las tablas expuestas.</summary>
public interface IQueryRepository<TEntity, in TFilter>
    where TEntity : class
    where TFilter : PagedQuery
{
    Task<PagedResult<TEntity>> GetPagedAsync(TFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>Lectura de tablas con clave.</summary>
public interface IReadRepository<TEntity, in TKey, in TFilter> : IQueryRepository<TEntity, TFilter>
    where TEntity : class
    where TFilter : PagedQuery
{
    /// <summary>Devuelve la entidad trackeada (lista para modificar) o null.</summary>
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista completa sin paginar. Solo para catálogos chicos (lookups cacheados);
    /// tiene un tope de seguridad para no traer tablas grandes por error.
    /// </summary>
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>Lectura + escritura atómica (una tabla, un SaveChanges).</summary>
public interface IRepository<TEntity, in TKey, in TFilter> : IReadRepository<TEntity, TKey, TFilter>
    where TEntity : class
    where TFilter : PagedQuery
{
    Task CreateAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
}
