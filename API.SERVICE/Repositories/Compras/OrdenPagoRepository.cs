using API.DA.DbContexts;
using API.SERVICE.Interfaces.Compras;
using Microsoft.EntityFrameworkCore;
using Db = global::API.DA.Entities;

namespace API.SERVICE.Repositories.Compras;

public sealed class OrdenPagoRepository : IOrdenPagoRepository
{
    private readonly ElRenacerDbContext _context;

    public OrdenPagoRepository(ElRenacerDbContext context)
    {
        _context = context;
    }

    public Task<Db.ProveedoresRecibos?> GetOrdenAsync(int idOrdenPago, CancellationToken cancellationToken = default) =>
        _context.ProveedoresRecibos.AsNoTracking().FirstOrDefaultAsync(o => o.IdProveedorRecibo == idOrdenPago, cancellationToken);

    public Task<List<Db.EntidadesCtaCte>> GetComprobantesPendientesAsync(int idEntidad, int estadoCtaCte, CancellationToken cancellationToken = default) =>
        _context.EntidadesCtaCte.AsNoTracking()
            .Where(c => c.Cancelado == false && c.IdEntidad == idEntidad && c.Estado == estadoCtaCte)
            .OrderBy(c => c.Fecha)
            .ToListAsync(cancellationToken);

    public Task<List<Db.EntidadOrdenPagoDocumentosProveedores>> GetImputacionesAsync(int idOrdenPago, CancellationToken cancellationToken = default) =>
        _context.EntidadOrdenPagoDocumentosProveedores.AsNoTracking()
            .Where(i => i.IdEntidadOrdenPago == idOrdenPago)
            .OrderBy(i => i.IdEntidadOrdenPagoDocumentoProveedor)
            .ToListAsync(cancellationToken);

    public Task BorrarImputacionesAsync(int idOrdenPago, CancellationToken cancellationToken = default) =>
        _context.EntidadOrdenPagoDocumentosProveedores.Where(i => i.IdEntidadOrdenPago == idOrdenPago).ExecuteDeleteAsync(cancellationToken);

    public Task<bool> TieneImputacionesAsync(int idDocumento, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        _context.EntidadOrdenPagoDocumentosProveedores.AnyAsync(i => i.IdDocumentoProveedor == idDocumento && i.IdComprobanteTipo == idComprobanteTipo, cancellationToken);

    public Task<Db.EntidadesCheques?> GetChequeTerceroAsync(int idEntidadCheque, CancellationToken cancellationToken = default) =>
        _context.EntidadesCheques.AsNoTracking().FirstOrDefaultAsync(c => c.IdEntidadCheque == idEntidadCheque, cancellationToken);

    public async Task<bool> AsignarChequeTerceroAsync(
        int idEntidadCheque, int idOrdenPago, int idComprobanteTipo, int estado, int estadoEsperado, CancellationToken cancellationToken = default) =>
        await _context.EntidadesCheques.Where(c => c.IdEntidadCheque == idEntidadCheque && c.Estado == estadoEsperado)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IdProveedorRecibo, idOrdenPago)
                .SetProperty(c => c.IdComprobanteTipo, idComprobanteTipo)
                .SetProperty(c => c.Estado, estado), cancellationToken) == 1;

    public Task DevolverChequesTercerosAsync(int idOrdenPago, int idComprobanteTipo, int estadoEnCartera, CancellationToken cancellationToken = default) =>
        _context.EntidadesCheques.Where(c => c.IdProveedorRecibo == idOrdenPago && c.IdComprobanteTipo == idComprobanteTipo)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IdProveedorRecibo, 0)
                .SetProperty(c => c.IdComprobanteTipo, 0)
                .SetProperty(c => c.Estado, estadoEnCartera), cancellationToken);

    public Task<ChequePropioRow?> GetChequePropioAsync(int idBancoCheque, CancellationToken cancellationToken = default) =>
        (from c in _context.BancosCheques.AsNoTracking()
         join cu in _context.BancosCuentas.AsNoTracking() on c.IdBancoCuenta equals cu.IdBancoCuenta
         where c.IdBancoCheque == idBancoCheque
         select new ChequePropioRow(c, cu.IdBanco, cu.IdBancoSucursal, cu.IdCuentaTipo, cu.NroCuenta))
        .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> EntregarChequePropioAsync(
        int idBancoCheque, string nroCheque, int idOrdenPago, int idComprobanteTipo, DateTime fechaEmision, DateTime fechaVencimiento, decimal importe, int estado,
        int estadoEsperado, CancellationToken cancellationToken = default) =>
        await _context.BancosCheques.Where(c => c.IdBancoCheque == idBancoCheque && c.Estado == estadoEsperado)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.NroCheque, nroCheque)
                .SetProperty(c => c.IdComprobante, idOrdenPago)
                .SetProperty(c => c.IdComprobanteTipo, idComprobanteTipo)
                .SetProperty(c => c.FechaEmision, fechaEmision)
                .SetProperty(c => c.FechaVencimiento, fechaVencimiento)
                .SetProperty(c => c.FechaImpresion, fechaEmision)
                .SetProperty(c => c.Importe, importe)
                .SetProperty(c => c.Estado, estado), cancellationToken) == 1;

    public Task AnularChequesPropiosAsync(int idOrdenPago, int idComprobanteTipo, int estado, CancellationToken cancellationToken = default) =>
        _context.BancosCheques.Where(c => c.IdComprobante == idOrdenPago && c.IdComprobanteTipo == idComprobanteTipo)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Importe, 0m)
                .SetProperty(c => c.IdComprobante, 0)
                .SetProperty(c => c.IdComprobanteTipo, 0)
                .SetProperty(c => c.Estado, estado), cancellationToken);

    public Task AnularChequesProveedorAsync(int idOrdenPago, int idComprobanteTipo, int estado, CancellationToken cancellationToken = default) =>
        _context.ProveedoresCheques.Where(c => c.IdProveedorRecibo == idOrdenPago && c.IdComprobanteTipo == idComprobanteTipo)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, estado), cancellationToken);

    public Task AnularDetalleAsync(int idOrdenPago, DateTime ahora, CancellationToken cancellationToken = default) =>
        _context.ProveedoresRecibosDetalle.Where(d => d.IdProveedorRecibo == idOrdenPago)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Descripcion, "ANULADO")
                .SetProperty(d => d.Detalle, "ANULADO")
                .SetProperty(d => d.Recepcion, ahora)
                .SetProperty(d => d.Emision, ahora)
                .SetProperty(d => d.Vto, ahora), cancellationToken);

    public void Add<TEntity>(TEntity entity) where TEntity : class => _context.Set<TEntity>().Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
