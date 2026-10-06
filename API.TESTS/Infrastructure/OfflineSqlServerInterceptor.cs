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

    // ExecuteUpdate / ExecuteDelete: se captura el SQL y se informan 0 filas afectadas.
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Commands.Enqueue(command.CommandText);
        return InterceptionResult<int>.SuppressWithResult(0);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Commands.Enqueue(command.CommandText);
        return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
    }

    private DbDataReader Respond(DbCommand command)
    {
        Commands.Enqueue(command.CommandText);

        var table = new DataTable();
        var sql = command.CommandText.TrimStart();
        if (sql.StartsWith("SELECT CASE", StringComparison.OrdinalIgnoreCase))
        {
            // AnyAsync: EF espera una fila con el resultado del EXISTS.
            table.Columns.Add("value", typeof(bool));
            table.Rows.Add(false);
        }
        else if (sql.StartsWith("SELECT COUNT(*)", StringComparison.OrdinalIgnoreCase))
        {
            table.Columns.Add("count", typeof(int));
            table.Rows.Add(0);
        }
        return table.CreateDataReader();
    }
}
