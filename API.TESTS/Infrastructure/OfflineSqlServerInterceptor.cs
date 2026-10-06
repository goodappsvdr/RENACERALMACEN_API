using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace API.TESTS.Infrastructure;

/// <summary>
/// Hace que EF Core con el proveedor SQL Server genere el SQL real pero sin conectarse:
/// suprime la apertura de la conexión y responde cada comando con un resultado vacío
/// (o 0 para los COUNT). Sirve para validar que todas las consultas LINQ se traducen.
/// </summary>
public sealed class OfflineSqlServerInterceptor : DbCommandInterceptor, IDbConnectionInterceptor
{
    public ConcurrentQueue<string> Commands { get; } = new();

    public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
        InterceptionResult.Suppress();

    public ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(InterceptionResult.Suppress());

    public InterceptionResult ConnectionClosing(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
        InterceptionResult.Suppress();

    public ValueTask<InterceptionResult> ConnectionClosingAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
        ValueTask.FromResult(InterceptionResult.Suppress());

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) =>
        InterceptionResult<DbDataReader>.SuppressWithResult(Respond(command));

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(Respond(command)));

    private DbDataReader Respond(DbCommand command)
    {
        Commands.Enqueue(command.CommandText);

        var table = new DataTable();
        if (command.CommandText.TrimStart().StartsWith("SELECT COUNT(*)", StringComparison.OrdinalIgnoreCase))
        {
            table.Columns.Add("count", typeof(int));
            table.Rows.Add(0);
        }
        return table.CreateDataReader();
    }
}
