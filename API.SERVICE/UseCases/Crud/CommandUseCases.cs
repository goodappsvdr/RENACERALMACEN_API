using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Models.Common;
using API.SERVICE.Services.Cache;

namespace API.SERVICE.UseCases.Crud;

/// <summary>
/// Base de las escrituras atómicas. Invalida la key de lookup del catálogo (si tiene)
/// y expone <see cref="ValidateAsync"/> para reglas de negocio propias de cada tabla
/// (se overridea en un archivo <c>partial</c> no generado).
/// </summary>
public abstract class CommandUseCaseBase<TEntity, TKey, TFilter, TDto>
    where TEntity : class
    where TFilter : PagedQuery
{
    protected CommandUseCaseBase(IRepository<TEntity, TKey, TFilter> repository, IRedisCacheService cache)
    {
        Repository = repository;
        Cache = cache;
    }

    protected IRepository<TEntity, TKey, TFilter> Repository { get; }

    protected IRedisCacheService Cache { get; }

    protected abstract string EntityName { get; }

    /// <summary>Key de lookup a invalidar tras escribir; null si la tabla no se cachea.</summary>
    protected virtual string? CacheKey => null;

    /// <summary>Reglas de negocio previas a persistir. <paramref name="current"/> es null en altas.</summary>
    protected virtual Task ValidateAsync(TDto dto, TEntity? current, CancellationToken cancellationToken) => Task.CompletedTask;

    protected async Task<TEntity> FindOrThrowAsync(TKey id, CancellationToken cancellationToken) =>
        await Repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"{EntityName} {id} no existe.");

    protected async Task InvalidateCacheAsync(CancellationToken cancellationToken)
    {
        if (CacheKey is not null)
            await Cache.RemoveAsync(CacheKey, cancellationToken);
    }
}

public abstract class CreateUseCaseBase<TEntity, TKey, TFilter, TDto, TDisplay> : CommandUseCaseBase<TEntity, TKey, TFilter, TDto>
    where TEntity : class
    where TFilter : PagedQuery
{
    protected CreateUseCaseBase(IRepository<TEntity, TKey, TFilter> repository, IRedisCacheService cache)
        : base(repository, cache) { }

    protected abstract TEntity ToEntity(TDto dto);

    protected abstract TDisplay ToDisplay(TEntity entity);

    public async Task<TDisplay> ExecuteAsync(TDto dto, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(dto, null, cancellationToken);

        var entity = ToEntity(dto);
        await Repository.CreateAsync(entity, cancellationToken);
        await InvalidateCacheAsync(cancellationToken);

        return ToDisplay(entity);
    }
}

public abstract class UpdateUseCaseBase<TEntity, TKey, TFilter, TDto, TDisplay> : CommandUseCaseBase<TEntity, TKey, TFilter, TDto>
    where TEntity : class
    where TFilter : PagedQuery
{
    protected UpdateUseCaseBase(IRepository<TEntity, TKey, TFilter> repository, IRedisCacheService cache)
        : base(repository, cache) { }

    protected abstract void Apply(TDto dto, TEntity entity);

    protected abstract TDisplay ToDisplay(TEntity entity);

    public async Task<TDisplay> ExecuteAsync(TKey id, TDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await FindOrThrowAsync(id, cancellationToken);
        await ValidateAsync(dto, entity, cancellationToken);

        Apply(dto, entity);
        await Repository.UpdateAsync(entity, cancellationToken);
        await InvalidateCacheAsync(cancellationToken);

        return ToDisplay(entity);
    }
}

public abstract class DeleteUseCaseBase<TEntity, TKey, TFilter> : CommandUseCaseBase<TEntity, TKey, TFilter, TKey>
    where TEntity : class
    where TFilter : PagedQuery
{
    protected DeleteUseCaseBase(IRepository<TEntity, TKey, TFilter> repository, IRedisCacheService cache)
        : base(repository, cache) { }

    public async Task ExecuteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await FindOrThrowAsync(id, cancellationToken);
        await ValidateAsync(id, entity, cancellationToken);

        await Repository.DeleteAsync(entity, cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
    }
}
