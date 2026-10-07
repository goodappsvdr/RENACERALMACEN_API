using API.DA.DbContexts;
using API.SERVICE.Interfaces.Sistema;
using Microsoft.EntityFrameworkCore;

namespace API.SERVICE.Repositories.Sistema;

public sealed class SqlServerClock : IServerClock
{
    private readonly ElRenacerDbContext _context;

    public SqlServerClock(ElRenacerDbContext context)
    {
        _context = context;
    }

    public Task<DateTime> GetNowAsync(CancellationToken cancellationToken = default) =>
        _context.Database.SqlQueryRaw<DateTime>("SELECT GETDATE() AS [Value]").SingleAsync(cancellationToken);
}
