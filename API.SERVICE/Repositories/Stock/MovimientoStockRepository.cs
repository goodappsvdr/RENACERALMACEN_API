using API.DA.DbContexts;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces.Stock;
using Microsoft.EntityFrameworkCore;
using Db = global::API.DA.Entities;

namespace API.SERVICE.Repositories.Stock;

public sealed class MovimientoStockRepository : IMovimientoStockRepository
{
    private readonly ElRenacerDbContext _context;

    public MovimientoStockRepository(ElRenacerDbContext context)
    {
        _context = context;
    }

    public Task<Db.Sucursales?> GetSucursalAsync(int idSucursal, CancellationToken cancellationToken = default) =>
        _context.Sucursales.AsNoTracking().FirstOrDefaultAsync(s => s.IdSucursal == idSucursal, cancellationToken);

    public Task<bool> OperaSucursalAsync(int idUsuario, int idSucursal, CancellationToken cancellationToken = default) =>
        _context.UsuariosSucursales.AnyAsync(us => us.IdUsuario == idUsuario && us.IdSucursal == idSucursal, cancellationToken);

    public Task<List<ItemStockSucursalRow>> GetItemsAsync(IReadOnlyCollection<int> idItems, int idSucursal, CancellationToken cancellationToken = default) =>
        _context.Items.AsNoTracking()
            .Where(i => idItems.Contains(i.IdItem))
            .Select(i => new ItemStockSucursalRow(
                i.IdItem,
                i.Descripcion ?? string.Empty,
                i.MueveStock == true,
                _context.ItemsSucursales.Where(s => s.IdItem == i.IdItem && s.IdSucursal == idSucursal).Select(s => (decimal?)(s.Stock ?? 0)).FirstOrDefault()))
            .ToListAsync(cancellationToken);

    public Task<Db.DocumentosCliente?> GetMovimientoAsync(int idDocumentoCliente, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        _context.DocumentosCliente.AsNoTracking()
            .FirstOrDefaultAsync(d => d.IdDocumentoCliente == idDocumentoCliente && d.IdComprobanteTipo == idComprobanteTipo, cancellationToken);

    public Task<List<Db.DocumentosClienteDetalle>> GetDetallesAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        _context.DocumentosClienteDetalle.AsNoTracking()
            .Where(d => d.IdDocumentoCliente == idDocumentoCliente)
            .OrderBy(d => d.IdDocumentoClienteDetalle)
            .ToListAsync(cancellationToken);

    public Task<List<Db.DocumentosCliente>> GetEnTransitoAsync(int idSucursalDestino, int idComprobanteTipo, int estado, CancellationToken cancellationToken = default) =>
        _context.DocumentosCliente.AsNoTracking()
            .Where(d => d.IdComprobanteTipo == idComprobanteTipo && d.IdCliente == idSucursalDestino && d.Estado == estado)
            .OrderBy(d => d.FechaEmision).ThenBy(d => d.IdDocumentoCliente)
            .ToListAsync(cancellationToken);

    public async Task<bool> CambiarEstadoAsync(int idDocumentoCliente, int estado, int estadoEsperado, DateTime? fechaAnulacion, CancellationToken cancellationToken = default)
    {
        var query = _context.DocumentosCliente.Where(d => d.IdDocumentoCliente == idDocumentoCliente && d.Estado == estadoEsperado);
        var filas = fechaAnulacion is { } fecha
            ? await query.ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, estado).SetProperty(d => d.FechaEmision, fecha), cancellationToken)
            : await query.ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, estado), cancellationToken);
        return filas == 1;
    }

    public Task MoverStockSucursalAsync(int idItem, int idSucursal, decimal delta, DateTime ahora, CancellationToken cancellationToken = default)
    {
        var fecha = DateOnly.FromDateTime(ahora);
        return _context.ItemsSucursales.Where(i => i.IdItem == idItem && i.IdSucursal == idSucursal && i.MueveStock == true)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Stock, i => i.Stock + delta)
                .SetProperty(i => i.FechaVenta, i => delta < 0 ? fecha : i.FechaVenta)
                .SetProperty(i => i.FechaCompra, i => delta > 0 ? fecha : i.FechaCompra), cancellationToken);
    }

    public Task AnularDetallesAsync(int idDocumentoCliente, int estadoLibroIva, CancellationToken cancellationToken = default) =>
        _context.DocumentosClienteDetalle.Where(d => d.IdDocumentoCliente == idDocumentoCliente)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Descripcion, "COMPROBANTE ANULADO")
                .SetProperty(d => d.EstadoLibroIva, estadoLibroIva)
                .SetProperty(d => d.Observaciones, "COMPROBANTE ANULADO"), cancellationToken);


    public async Task BloquearSucursalAsync(int idSucursal, CancellationToken cancellationToken = default)
    {
        var recurso = $"elrenacer:stock:sucursal:{idSucursal}";
        var resultado = await _context.Database
            .SqlQuery<int>($"""
                DECLARE @r int;
                EXEC @r = sp_getapplock @Resource = {recurso}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                SELECT @r AS [Value];
                """)
            .ToListAsync(cancellationToken);

        if (resultado.Count > 0 && resultado[0] < 0)
            throw new ConflictException("Otra operación está moviendo stock de esta sucursal. Reintentar en unos segundos.");
    }

    public void Add<TEntity>(TEntity entity) where TEntity : class => _context.Set<TEntity>().Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
