using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Models.Common;
using API.SERVICE.Services.Cache;

namespace API.SERVICE.UseCases.Crud;

/// <summary>Listado paginado y filtrado.</summary>
public abstract class GetListUseCaseBase<TEntity, TFilter, TDisplay>
    where TEntity : class
    where TFilter : PagedQuery
{
    private readonly IQueryRepository<TEntity, TFilter> _repository;

    protected GetListUseCaseBase(IQueryRepository<TEntity, TFilter> repository)
    {
        _repository = repository;
    }

    protected abstract TDisplay ToDisplay(TEntity entity);

    public async Task<PagedResult<TDisplay>> ExecuteAsync(TFilter filter, CancellationToken cancellationToken = default)
    {
        var page = await _repository.GetPagedAsync(filter, cancellationToken);
        return page.Map(ToDisplay);
    }
}

/// <summary>Detalle por Id. Lanza <see cref="NotFoundException"/> si no existe.</summary>
public abstract class GetByIdUseCaseBase<TEntity, TKey, TFilter, TDisplay>
    where TEntity : class
    where TFilter : PagedQuery
{
    private readonly IReadRepository<TEntity, TKey, TFilter> _repository;

    protected GetByIdUseCaseBase(IReadRepository<TEntity, TKey, TFilter> repository)
    {
        _repository = repository;
    }

    protected abstract string EntityName { get; }

    protected abstract TDisplay ToDisplay(TEntity entity);

    public async Task<TDisplay> ExecuteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"{EntityName} {id} no existe.");

        return ToDisplay(entity);
    }
}

/// <summary>Lista completa de un catálogo chico, cacheada (para combos / lookups del front).</summary>
public abstract class GetLookupUseCaseBase<TEntity, TKey, TFilter, TDisplay>
    where TEntity : class
    where TFilter : PagedQuery
{
    private readonly IReadRepository<TEntity, TKey, TFilter> _repository;
    private readonly IRedisCacheService _cache;

    protected GetLookupUseCaseBase(IReadRepository<TEntity, TKey, TFilter> repository, IRedisCacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    protected abstract string CacheKey { get; }

    protected virtual TimeSpan CacheTtl => Services.Cache.CacheTtl.Catalog;

    protected abstract TDisplay ToDisplay(TEntity entity);

    public Task<IReadOnlyList<TDisplay>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        _cache.GetOrSetAsync<IReadOnlyList<TDisplay>>(CacheKey, CacheTtl, async ct =>
        {
            var entities = await _repository.GetAllAsync(ct);
            return entities.Select(ToDisplay).ToList();
        }, cancellationToken);
}
