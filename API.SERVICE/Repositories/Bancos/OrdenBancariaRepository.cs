using API.DA.DbContexts;
using API.SERVICE.Interfaces.Bancos;
using Microsoft.EntityFrameworkCore;
using Db = global::API.DA.Entities;

namespace API.SERVICE.Repositories.Bancos;

public sealed class OrdenBancariaRepository : IOrdenBancariaRepository
{
    private readonly ElRenacerDbContext _context;

    public OrdenBancariaRepository(ElRenacerDbContext context)
    {
        _context = context;
    }

    public Task<Db.BancosCuentas?> GetCuentaAsync(int idBancoCuenta, CancellationToken cancellationToken = default) =>
        _context.BancosCuentas.AsNoTracking().FirstOrDefaultAsync(c => c.IdBancoCuenta == idBancoCuenta, cancellationToken);

    public Task<Db.OrdenesDepositos?> GetDepositoAsync(int idOrdenDeposito, CancellationToken cancellationToken = default) =>
        _context.OrdenesDepositos.AsNoTracking().FirstOrDefaultAsync(o => o.IdOrdenDepostio == idOrdenDeposito, cancellationToken);

    public Task<Db.OrdenesExrtacciones?> GetExtraccionAsync(int idOrdenExtraccion, CancellationToken cancellationToken = default) =>
        _context.OrdenesExrtacciones.AsNoTracking().FirstOrDefaultAsync(o => o.IdOrdenExtraccion == idOrdenExtraccion, cancellationToken);

    public async Task<bool> AnularDepositoAsync(int idOrdenDeposito, int estado, int estadoEsperado, DateTime ahora, CancellationToken cancellationToken = default) =>
        await _context.OrdenesDepositos.Where(o => o.IdOrdenDepostio == idOrdenDeposito && o.Estado == estadoEsperado)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.FechaEmision, ahora)
                .SetProperty(o => o.Observaciones, "ANULADO")
                .SetProperty(o => o.Total, 0m)
                .SetProperty(o => o.Estado, estado), cancellationToken) == 1;

    public async Task<bool> AnularExtraccionAsync(int idOrdenExtraccion, int estado, int estadoEsperado, DateTime ahora, CancellationToken cancellationToken = default) =>
        await _context.OrdenesExrtacciones.Where(o => o.IdOrdenExtraccion == idOrdenExtraccion && o.Estado == estadoEsperado)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.FechaEmision, ahora)
                .SetProperty(o => o.Observaciones, "ANULADO")
                .SetProperty(o => o.Total, 0m)
                .SetProperty(o => o.Estado, estado), cancellationToken) == 1;

    public Task AnularDetalleDepositoAsync(int idOrdenDeposito, DateTime ahora, CancellationToken cancellationToken = default) =>
        _context.OrdenesDepositosDetalle.Where(d => d.IdOrdendeposito == idOrdenDeposito)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Descripcion, "ANULADO")
                .SetProperty(d => d.Detalle, "ANULADO")
                .SetProperty(d => d.Recepcion, ahora)
                .SetProperty(d => d.Emision, ahora)
                .SetProperty(d => d.Vto, ahora)
                .SetProperty(d => d.IdBanco, 0)
                .SetProperty(d => d.IdSucursal, 0)
                .SetProperty(d => d.Banco, string.Empty)
                .SetProperty(d => d.Sucursal, string.Empty)
                .SetProperty(d => d.Nro, string.Empty)
                .SetProperty(d => d.Total, 0m), cancellationToken);

    public Task AnularDetalleExtraccionAsync(int idOrdenExtraccion, DateTime ahora, CancellationToken cancellationToken = default) =>
        _context.OrdenesExtraccionesDetalle.Where(d => d.IdOrdenExtraccion == idOrdenExtraccion)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Descripcion, "ANULADO")
                .SetProperty(d => d.Detalle, "ANULADO")
                .SetProperty(d => d.Recepcion, ahora)
                .SetProperty(d => d.Emision, ahora)
                .SetProperty(d => d.Vto, ahora)
                .SetProperty(d => d.IdBanco, 0)
                .SetProperty(d => d.IdSucursal, 0)
                .SetProperty(d => d.Banco, string.Empty)
                .SetProperty(d => d.Sucursal, string.Empty)
                .SetProperty(d => d.Nro, string.Empty)
                .SetProperty(d => d.Total, 0m), cancellationToken);

    public void Add<TEntity>(TEntity entity) where TEntity : class => _context.Set<TEntity>().Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
