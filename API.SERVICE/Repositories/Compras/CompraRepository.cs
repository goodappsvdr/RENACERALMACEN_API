using API.DA.DbContexts;
using API.SERVICE.Interfaces.Compras;
using Microsoft.EntityFrameworkCore;
using Db = global::API.DA.Entities;

namespace API.SERVICE.Repositories.Compras;

/// <summary>Igual que en ventas: ExecuteUpdate/ExecuteDelete por clave, como cada SP, dentro de la transacción del flujo.</summary>
public sealed class CompraRepository : ICompraRepository
{
    private const int IdEmpresa = 1;

    private readonly ElRenacerDbContext _context;

    public CompraRepository(ElRenacerDbContext context)
    {
        _context = context;
    }

    public Task<Db.DocumentosProveedor?> GetDocumentoAsync(int idDocumentoProveedor, CancellationToken cancellationToken = default) =>
        _context.DocumentosProveedor.AsNoTracking().FirstOrDefaultAsync(d => d.IdDocumentoProveedor == idDocumentoProveedor, cancellationToken);

    public Task<List<Db.DocumentosProveedorDetalle>> GetDetallesAsync(int idDocumentoProveedor, CancellationToken cancellationToken = default) =>
        _context.DocumentosProveedorDetalle.AsNoTracking()
            .Where(d => d.IdDocumentoProveedor == idDocumentoProveedor)
            .OrderBy(d => d.IdDocumentoProveedorDetalle)
            .ToListAsync(cancellationToken);

    public Task<bool> ExisteDuplicadoAsync(int idProveedor, int idComprobanteTipo, string puntoVenta, string numero, int estadoAnulado, CancellationToken cancellationToken = default) =>
        _context.DocumentosProveedor.AnyAsync(d =>
            d.IdProveedor == idProveedor && d.IdComprobanteTipo == idComprobanteTipo && d.PuntoVenta == puntoVenta && d.Numero == numero && d.Estado != estadoAnulado,
            cancellationToken);

    public Task<List<string>> GetLetrasAsync(int idComprobanteTipo, int idCategoriaIvaEmpresa, int idCategoriaIvaProveedor, CancellationToken cancellationToken = default) =>
        _context.ComprobantesLetras.AsNoTracking()
            // En ComprobantesLetras la columna "Proveedor" es la categoría de la empresa y "Cliente" la de la contraparte.
            .Where(l => l.IdComprobanteTipo == idComprobanteTipo && l.IdCategoriaIvaproveedor == idCategoriaIvaEmpresa
                        && l.IdCategoriaIvacliente == idCategoriaIvaProveedor && l.IdEmpresa == IdEmpresa && l.Letra != null)
            .Select(l => l.Letra!)
            .Distinct()
            .ToListAsync(cancellationToken);

    public Task<Db.CajaPlanillas?> GetPlanillaAbiertaAsync(int idUsuario, int estadoAbierta, CancellationToken cancellationToken = default) =>
        _context.CajaPlanillas.AsNoTracking()
            .Where(p => p.IdUsuario == idUsuario && p.Estado == estadoAbierta)
            .OrderByDescending(p => p.IdPlanillaCaja)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<Db.DocumentosProveedor>> GetComprobantesConPendienteAsync(
        int idProveedor, IReadOnlyCollection<int> tipos, IReadOnlyCollection<int> estadosExcluidos, int? idSucursal, CancellationToken cancellationToken = default) =>
        _context.DocumentosProveedor.AsNoTracking()
            .Where(d => d.IdProveedor == idProveedor && tipos.Contains(d.IdComprobanteTipo!.Value) && !estadosExcluidos.Contains(d.Estado!.Value)
                        && (idSucursal == null || d.IdSucursal == idSucursal)
                        && _context.EntidadesCtaCteStockMovimientosDetalle.Any(s =>
                            s.IdComprobante == d.IdDocumentoProveedor && s.IdComprobanteTipo == d.IdComprobanteTipo && s.Saldo != 0))
            .OrderByDescending(d => d.IdDocumentoProveedor)
            .ToListAsync(cancellationToken);

    public Task<List<LineaPendienteCompraRow>> GetLineasPendientesAsync(int idDocumentoProveedor, IReadOnlyCollection<int> tipos, CancellationToken cancellationToken = default) =>
        (from s in _context.EntidadesCtaCteStockMovimientosDetalle.AsNoTracking()
         join d in _context.DocumentosProveedorDetalle.AsNoTracking()
             on new { Doc = (long?)s.IdComprobante, Det = (long?)s.IdComprobanteDetalle }
             equals new { Doc = d.IdDocumentoProveedor, Det = (long?)d.IdDocumentoProveedorDetalle }
         where s.IdComprobante == idDocumentoProveedor && tipos.Contains(s.IdComprobanteTipo!.Value) && s.Saldo != 0
         orderby d.IdDocumentoProveedorDetalle
         select new LineaPendienteCompraRow(d, s.IdComprobanteTipo!.Value, s.Saldo ?? 0m))
        .ToListAsync(cancellationToken);

    public Task<List<Db.DocumentosProveedorRemitos>> GetRelacionesComoDestinoAsync(int idDocumentoProveedor, CancellationToken cancellationToken = default) =>
        _context.DocumentosProveedorRemitos.AsNoTracking()
            .Where(r => r.IdRemito == idDocumentoProveedor)
            .OrderBy(r => r.IdDocumentoProveedorRemito)
            .ToListAsync(cancellationToken);

    public Task BorrarRelacionAsync(int idDocumentoProveedorRemito, CancellationToken cancellationToken = default) =>
        _context.DocumentosProveedorRemitos.Where(r => r.IdDocumentoProveedorRemito == idDocumentoProveedorRemito).ExecuteDeleteAsync(cancellationToken);

    public Task SetEstadoPendienteAsync(int idDocumentoProveedor, int estado, bool pendiente, CancellationToken cancellationToken = default) =>
        _context.DocumentosProveedor.Where(d => d.IdDocumentoProveedor == idDocumentoProveedor)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, estado).SetProperty(d => d.Pendiente, pendiente), cancellationToken);

    public Task SetPendienteAsync(int idDocumentoProveedor, bool pendiente, CancellationToken cancellationToken = default) =>
        _context.DocumentosProveedor.Where(d => d.IdDocumentoProveedor == idDocumentoProveedor)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Pendiente, pendiente), cancellationToken);

    public Task<bool> TieneRelacionesComoOrigenAsync(int idDocumentoProveedor, CancellationToken cancellationToken = default) =>
        _context.DocumentosProveedorRemitos.AnyAsync(r => r.IdDocumentoProveedor == idDocumentoProveedor, cancellationToken);

    public Task DeterminarRemitarFacturarAsync(int idDocumentoProveedor, bool remitar, bool facturar, bool pendiente, CancellationToken cancellationToken = default) =>
        _context.DocumentosProveedor.Where(d => d.IdDocumentoProveedor == idDocumentoProveedor)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Remitar, remitar)
                .SetProperty(d => d.Facturar, facturar)
                .SetProperty(d => d.Pendiente, pendiente), cancellationToken);

    public Task AnularDocumentoAsync(int idDocumentoProveedor, int estado, DateTime ahora, CancellationToken cancellationToken = default) =>
        _context.DocumentosProveedor.Where(d => d.IdDocumentoProveedor == idDocumentoProveedor)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.FechaEmision, ahora).SetProperty(d => d.Estado, estado), cancellationToken);

    public async Task BorrarLibroIvaAsync(int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default)
    {
        await _context.LibroIvaCompra.Where(l => l.IdComprobante == idComprobante && l.IdComprobanteTipo == idComprobanteTipo).ExecuteDeleteAsync(cancellationToken);
        await _context.TxtComprasAlicuotas.Where(t => t.IdComprobante == idComprobante && t.IdComprobanteTipo == idComprobanteTipo).ExecuteDeleteAsync(cancellationToken);
    }

    public Task BorrarOtrosTributosAsync(int idDocumentoProveedor, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        _context.DocumentosProveedorOtrosTributos
            .Where(t => t.IdDocumentoProveedor == idDocumentoProveedor && t.IdComprobanteTipo == idComprobanteTipo)
            .ExecuteDeleteAsync(cancellationToken);

    public Task<Db.EntidadesCtaCte?> GetCtaCteAsync(int idComprobanteTipo, int idComprobante, CancellationToken cancellationToken = default) =>
        _context.EntidadesCtaCte.AsNoTracking()
            .Where(c => c.IdComprobanteTipo == idComprobanteTipo && c.IdComprobante == idComprobante)
            .OrderBy(c => c.IdEntidadCtaCte)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add<TEntity>(TEntity entity) where TEntity : class => _context.Set<TEntity>().Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
