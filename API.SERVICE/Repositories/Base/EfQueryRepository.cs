using API.DA.DbContexts;
using API.SERVICE.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace API.SERVICE.Repositories.Base;

/// <summary>
/// Consulta paginada EF Core. Base de todos los repositorios; las tablas sin clave
/// (solo lectura) heredan directamente de acá.
/// </summary>
public abstract class EfQueryRepository<TEntity, TFilter>
    where TEntity : class
    where TFilter : PagedQuery
{
    protected EfQueryRepository(ElRenacerDbContext context)
    {
        Context = context;
    }

    protected ElRenacerDbContext Context { get; }

    protected DbSet<TEntity> Set => Context.Set<TEntity>();

    protected abstract IQueryable<TEntity> ApplyFilter(IQueryable<TEntity> query, TFilter filter);

    protected abstract IOrderedQueryable<TEntity> ApplyDefaultOrder(IQueryable<TEntity> query);

    public virtual async Task<PagedResult<TEntity>> GetPagedAsync(TFilter filter, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(Set.AsNoTracking(), filter);

        var total = await query.CountAsync(cancellationToken);
        var items = await ApplyDefaultOrder(query)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TEntity>(items, filter.Page, filter.PageSize, total);
    }
}
