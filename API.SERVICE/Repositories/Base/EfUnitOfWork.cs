using API.DA.DbContexts;
using API.SERVICE.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.SERVICE.Repositories.Base;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ElRenacerDbContext _context;

    public EfUnitOfWork(ElRenacerDbContext context)
    {
        _context = context;
    }

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        // Con EnableRetryOnFailure, EF exige envolver la transacción manual en la estrategia de ejecución:
        // si falla por un error transitorio, se reintenta el bloque entero.
        var strategy = _context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(async ct =>
        {
            // Un reintento arranca limpio: descarta lo trackeado en el intento fallido.
            _context.ChangeTracker.Clear();

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            var result = await operation(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }
}
