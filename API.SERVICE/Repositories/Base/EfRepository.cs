using API.DA.DbContexts;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Models.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace API.SERVICE.Repositories.Base;

/// <summary>
/// Persistencia EF Core de una tabla con clave: lectura por Id, lookup y escritura atómica
/// (una tabla, un SaveChanges). Los repositorios concretos solo agregan filtros y orden.
/// </summary>
public abstract class EfRepository<TEntity, TKey, TFilter> : EfQueryRepository<TEntity, TFilter>
    where TEntity : class
    where TFilter : PagedQuery
{
    /// <summary>Tope de <see cref="GetAllAsync"/>: los lookups son para catálogos chicos.</summary>
    protected const int MaxLookupRows = 5000;

    protected EfRepository(ElRenacerDbContext context) : base(context) { }

    public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default) =>
        await Set.FindAsync([id], cancellationToken);

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await ApplyDefaultOrder(Set.AsNoTracking()).Take(MaxLookupRows).ToListAsync(cancellationToken);

    public virtual async Task CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Set.Add(entity);
        await SaveAsync(cancellationToken);
    }

    public virtual async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (Context.Entry(entity).State == EntityState.Detached)
            Set.Update(entity);

        await SaveAsync(cancellationToken);
    }

    public virtual async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Set.Remove(entity);
        await SaveAsync(cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 or 2601 or 2627 })
        {
            // FK / UNIQUE: registro referenciado o duplicado. Se informa como 409 sin exponer el SQL.
            throw new ConflictException("La operación viola una restricción de la base (registro duplicado o con datos asociados).");
        }
    }
}
