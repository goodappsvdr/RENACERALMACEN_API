using API.DA.DbContexts;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces.Ventas;
using Microsoft.EntityFrameworkCore;
using Db = global::API.DA.Entities;

namespace API.SERVICE.Repositories.Ventas;

/// <summary>
/// Las actualizaciones usan ExecuteUpdate/ExecuteDelete (mismo UPDATE/DELETE por clave que cada SP, aritmética
/// sobre la columna incluida) y corren dentro de la transacción del flujo.
/// </summary>
public sealed class VentaRepository : IVentaRepository
{
    private readonly ElRenacerDbContext _context;

    public VentaRepository(ElRenacerDbContext context)
    {
        _context = context;
    }

    // ---------- Lecturas ----------

    public async Task<decimal> GetSaldoCtaCteAsync(int idEntidad, CancellationToken cancellationToken = default) =>
        await _context.EntidadesCtaCte.Where(c => c.IdEntidad == idEntidad).SumAsync(c => c.Total2, cancellationToken) ?? 0m;

    public Task<OfertaActivaRow?> GetOfertaActivaAsync(int idSucursal, int idItem, CancellationToken cancellationToken = default) =>
        _context.Ofertas.AsNoTracking()
            .Where(o => o.IdItem == idItem && o.IdEstado == VentaRules.EstadoOfertaActiva
                        && _context.OfertasSucursales.Any(s => s.IdSucursal == idSucursal && s.IdOferta == o.IdOferta))
            .OrderBy(o => o.IdOferta)
            .Select(o => new OfertaActivaRow(o.IdOferta, o.TipoOferta))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> TienePendienteRemitarAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        (from s in _context.EntidadesCtaCteStockMovimientosDetalle
         join d in _context.DocumentosClienteDetalle
             on new { Doc = (long?)s.IdComprobante, Det = (long?)s.IdComprobanteDetalle }
             equals new { Doc = d.IdDocumentoCliente, Det = (long?)d.IdDocumentoClienteDetalle }
         where s.IdComprobante == idDocumentoCliente
               && VentaRules.TiposConStockPendiente.Contains(s.IdComprobanteTipo!.Value)
               && s.Saldo != 0
         select s.IdEntidadCtaCteStockMovimientoDetalle)
        .AnyAsync(cancellationToken);

    public Task<Db.DocumentosCliente?> GetDocumentoAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        _context.DocumentosCliente.AsNoTracking().FirstOrDefaultAsync(d => d.IdDocumentoCliente == idDocumentoCliente, cancellationToken);

    public Task<List<Db.DocumentosClienteDetalle>> GetDetallesAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        _context.DocumentosClienteDetalle.AsNoTracking()
            .Where(d => d.IdDocumentoCliente == idDocumentoCliente)
            .OrderBy(d => d.IdDocumentoClienteDetalle)
            .ToListAsync(cancellationToken);

    public Task<List<Db.DocumentosClienteRemitos>> GetRemitosAsociadosAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        _context.DocumentosClienteRemitos.AsNoTracking()
            .Where(r => r.IdDocumentoCliente == idDocumentoCliente)
            .OrderBy(r => r.IdDocumentoClienteRemito)
            .ToListAsync(cancellationToken);

    public Task<List<Db.EntidadesCtaCteStockMovimientosDetalle>> GetMovimientosStockAsync(int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        _context.EntidadesCtaCteStockMovimientosDetalle.AsNoTracking()
            .Where(s => s.IdComprobante == idComprobante && s.IdComprobanteTipo == idComprobanteTipo)
            .OrderBy(s => s.IdEntidadCtaCteStockMovimientoDetalle)
            .ToListAsync(cancellationToken);

    public async Task<decimal> GetSaldoStockAsync(int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default)
    {
        var filas = _context.EntidadesCtaCteStockMovimientosDetalle
            .Where(s => s.IdComprobante == idComprobante && s.IdComprobanteTipo == idComprobanteTipo);
        var total = await filas.SumAsync(s => s.Total, cancellationToken) ?? 0m;
        var saldo = await filas.SumAsync(s => s.Saldo, cancellationToken) ?? 0m;
        return total - saldo;
    }

    public Task<int?> GetReciboImputadoAsync(int idDocumentoCliente, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        _context.EntidadRecibosDocumentosCliente.AsNoTracking()
            .Where(i => i.IdDocumentoCliente == idDocumentoCliente && i.IdComprobanteTipo == idComprobanteTipo)
            .OrderBy(i => i.IdEntidadReciboDocumentoCliente)
            .Select(i => i.IdEntidadRecibo)
            .FirstOrDefaultAsync(cancellationToken);

    // ---------- Altas ----------

    public void Add<TEntity>(TEntity entity) where TEntity : class => _context.Set<TEntity>().Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);

    // ---------- Stock ----------

    public async Task RestarStockAsync(int idItem, int idSucursal, decimal cantidad, DateTime ahora, CancellationToken cancellationToken = default)
    {
        await _context.Items.Where(i => i.IdItem == idItem && i.MueveStock == true)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.StockActual, i => i.StockActual - cantidad), cancellationToken);

        var fecha = DateOnly.FromDateTime(ahora);
        await _context.ItemsSucursales.Where(i => i.IdItem == idItem && i.IdSucursal == idSucursal && i.MueveStock == true)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Stock, i => i.Stock - cantidad)
                .SetProperty(i => i.FechaVenta, fecha), cancellationToken);
    }

    public async Task SumarStockAsync(int idItem, int idSucursal, decimal cantidad, DateTime ahora, CancellationToken cancellationToken = default)
    {
        await _context.Items.Where(i => i.IdItem == idItem && i.MueveStock == true)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.StockActual, i => i.StockActual + cantidad), cancellationToken);

        var fecha = DateOnly.FromDateTime(ahora);
        await _context.ItemsSucursales.Where(i => i.IdItem == idItem && i.IdSucursal == idSucursal && i.MueveStock == true)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Stock, i => i.Stock + cantidad)
                .SetProperty(i => i.FechaCompra, fecha), cancellationToken);
    }

    public Task AjustarSaldoStockAsync(int idComprobante, int idComprobanteDetalle, int idComprobanteTipo, int idItem, decimal delta, CancellationToken cancellationToken = default) =>
        _context.EntidadesCtaCteStockMovimientosDetalle
            .Where(s => s.IdComprobante == idComprobante && s.IdComprobanteTipo == idComprobanteTipo
                        && s.IdComprobanteDetalle == idComprobanteDetalle && s.IdItem == idItem)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Saldo, x => x.Saldo + delta), cancellationToken);

    public Task AnularMovimientosStockAsync(int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        _context.EntidadesCtaCteStockMovimientosDetalle
            .Where(s => s.IdComprobante == idComprobante && s.IdComprobanteTipo == idComprobanteTipo)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Total, 0m)
                .SetProperty(x => x.Saldo, 0m)
                .SetProperty(x => x.Saldo2, 0m), cancellationToken);

    // ---------- Ofertas y números de serie ----------

    public Task AjustarOfertaDisponibleAsync(int idOferta, decimal delta, CancellationToken cancellationToken = default) =>
        // OfertasAgotamiento no tiene clave en el modelo: SQL directo, igual que el SP (afecta todas las sucursales de la oferta).
        _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE OfertasAgotamiento SET CantidadDisponible = CantidadDisponible + {delta} WHERE ID_Oferta = {idOferta}",
            cancellationToken);

    public Task AsignarNroSerieAsync(int idItemNroSerie, int idComprobanteTipo, int idComprobante, long idDetalle, int estado, CancellationToken cancellationToken = default) =>
        _context.ItemsNroSeries.Where(n => n.IdItemNroSerie == idItemNroSerie)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IdComprobanteTipo, idComprobanteTipo)
                .SetProperty(n => n.IdComprobante, idComprobante)
                .SetProperty(n => n.IdDocumentoClienteDetalle, (int)idDetalle)
                .SetProperty(n => n.Estado, estado), cancellationToken);

    public Task LiberarNrosSerieAsync(int idComprobanteTipo, int idComprobante, int estado, CancellationToken cancellationToken = default) =>
        _context.ItemsNroSeries.Where(n => n.IdComprobanteTipo == idComprobanteTipo && n.IdComprobante == idComprobante)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IdComprobanteTipo, 0)
                .SetProperty(n => n.IdComprobante, 0)
                .SetProperty(n => n.IdDocumentoClienteDetalle, 0)
                .SetProperty(n => n.Estado, estado), cancellationToken);

    // ---------- Estado de comprobantes ----------

    public Task SetEstadoPendienteAsync(int idDocumentoCliente, int estado, bool pendiente, CancellationToken cancellationToken = default) =>
        _context.DocumentosCliente.Where(d => d.IdDocumentoCliente == idDocumentoCliente)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, estado).SetProperty(d => d.Pendiente, pendiente), cancellationToken);

    public Task DeterminarRemitarFacturarAsync(int idDocumentoCliente, bool remitar, bool facturar, bool pendiente, CancellationToken cancellationToken = default) =>
        _context.DocumentosCliente.Where(d => d.IdDocumentoCliente == idDocumentoCliente)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Remitar, remitar)
                .SetProperty(d => d.Facturar, facturar)
                .SetProperty(d => d.Pendiente, pendiente), cancellationToken);

    public Task AnularDetalleAsync(long idDetalle, int estadoLibroIva, CancellationToken cancellationToken = default) =>
        _context.DocumentosClienteDetalle.Where(d => d.IdDocumentoClienteDetalle == idDetalle)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Descripcion, "COMPROBANTE ANULADO")
                .SetProperty(d => d.EstadoLibroIva, estadoLibroIva)
                .SetProperty(d => d.Observaciones, "COMPROBANTE ANULADO"), cancellationToken);

    public Task BorrarRemitoAsociadoAsync(int idDocumentoClienteRemito, CancellationToken cancellationToken = default) =>
        _context.DocumentosClienteRemitos.Where(r => r.IdDocumentoClienteRemito == idDocumentoClienteRemito).ExecuteDeleteAsync(cancellationToken);

    // ---------- Factura electrónica ----------

    public Task<Db.Sucursales?> GetSucursalAsync(int idSucursal, CancellationToken cancellationToken = default) =>
        _context.Sucursales.AsNoTracking().FirstOrDefaultAsync(s => s.IdSucursal == idSucursal, cancellationToken);

    public Task ModificarDatosAfipAsync(int idDocumentoCliente, string puntoVenta, string numero, string cae, string codigoBarras, CancellationToken cancellationToken = default) =>
        _context.DocumentosCliente.Where(d => d.IdDocumentoCliente == idDocumentoCliente)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.PuntoVenta, puntoVenta)
                .SetProperty(d => d.Numero, numero)
                .SetProperty(d => d.Cae, cae)
                .SetProperty(d => d.BarCode, codigoBarras), cancellationToken);

    public Task<List<Db.DocumentosCliente>> GetPendientesAfipAsync(int idComprobanteTipo, int estadoAnulado, CancellationToken cancellationToken = default) =>
        _context.DocumentosCliente.AsNoTracking()
            .Where(d => d.IdComprobanteTipo == idComprobanteTipo && d.Cae == AfipCaePendiente && d.Estado != estadoAnulado)
            .OrderBy(d => d.IdDocumentoCliente)
            .ToListAsync(cancellationToken);

    // ---------- Nota de crédito ----------

    public Task<List<int>> GetRecibosImputadosAsync(int idDocumentoCliente, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        _context.EntidadRecibosDocumentosCliente.AsNoTracking()
            .Where(i => i.IdDocumentoCliente == idDocumentoCliente && i.IdComprobanteTipo == idComprobanteTipo && i.IdEntidadRecibo != null)
            .OrderBy(i => i.IdEntidadReciboDocumentoCliente)
            .Select(i => i.IdEntidadRecibo!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<decimal> GetTotalNotasCreditoAsync(int idFactura, int idTipoNotaCredito, int estadoAnulado, int excluirId, CancellationToken cancellationToken = default) =>
        await (from r in _context.DocumentosClienteRelacion
               join nc in _context.DocumentosCliente on r.IdDocumentoCliente2 equals nc.IdDocumentoCliente
               where r.IdDocumentoCliente1 == idFactura && nc.IdComprobanteTipo == idTipoNotaCredito
                     && nc.Estado != estadoAnulado && nc.IdDocumentoCliente != excluirId
               select nc.TotalGeneral)
            .SumAsync(cancellationToken) ?? 0m;

    public Task<int?> GetFacturaDeNotaCreditoAsync(int idNotaCredito, CancellationToken cancellationToken = default) =>
        _context.DocumentosClienteRelacion.AsNoTracking()
            .Where(r => r.IdDocumentoCliente2 == idNotaCredito)
            .OrderBy(r => r.IdDocumentoClienteRelacion)
            .Select(r => r.IdDocumentoCliente1)
            .FirstOrDefaultAsync(cancellationToken);

    public Task BorrarRelacionAsync(int idFactura, int idNotaCredito, CancellationToken cancellationToken = default) =>
        _context.DocumentosClienteRelacion
            .Where(r => r.IdDocumentoCliente1 == idFactura && r.IdDocumentoCliente2 == idNotaCredito)
            .ExecuteDeleteAsync(cancellationToken);

    // ---------- Remitos ----------

    public Task<string?> GetLetraAsync(int idComprobanteTipo, int idCategoriaIvaCliente, CancellationToken cancellationToken = default) =>
        _context.ComprobantesLetras.AsNoTracking()
            .Where(l => l.IdComprobanteTipo == idComprobanteTipo && l.IdCategoriaIvacliente == idCategoriaIvaCliente && l.IdCategoriaIvaproveedor == 1)
            .OrderBy(l => l.IdComprobanteLetra)
            .Select(l => l.Letra)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<Db.DocumentosClienteRemitos>> GetRelacionesComoRemitoAsync(int idRemito, CancellationToken cancellationToken = default) =>
        _context.DocumentosClienteRemitos.AsNoTracking()
            .Where(r => r.IdRemito == idRemito)
            .OrderBy(r => r.IdDocumentoClienteRemito)
            .ToListAsync(cancellationToken);

    public Task<List<Db.DocumentosCliente>> GetComprobantesParaRemitirAsync(
        int idEntidad, IReadOnlyCollection<int> tipos, IReadOnlyCollection<int> estadosExcluidos, CancellationToken cancellationToken = default) =>
        _context.DocumentosCliente.AsNoTracking()
            .Where(d => d.IdCliente == idEntidad && tipos.Contains(d.IdComprobanteTipo!.Value) && d.Remitar == true && d.Pendiente == true
                        && !estadosExcluidos.Contains(d.Estado!.Value))
            .OrderByDescending(d => d.IdDocumentoCliente)
            .ToListAsync(cancellationToken);

    public Task<List<LineaPendienteRow>> GetLineasPendientesAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        (from s in _context.EntidadesCtaCteStockMovimientosDetalle.AsNoTracking()
         join d in _context.DocumentosClienteDetalle.AsNoTracking()
             on new { Doc = (long?)s.IdComprobante, Det = (long?)s.IdComprobanteDetalle }
             equals new { Doc = d.IdDocumentoCliente, Det = (long?)d.IdDocumentoClienteDetalle }
         where s.IdComprobante == idDocumentoCliente
               && VentaRules.TiposConStockPendiente.Contains(s.IdComprobanteTipo!.Value)
               && s.Saldo != 0
         orderby d.IdDocumentoClienteDetalle
         select new LineaPendienteRow(d, s.IdComprobanteTipo!.Value, s.Saldo ?? 0m))
        .ToListAsync(cancellationToken);

    // ---------- Presupuestos ----------

    public Task ModificarPresupuestoAsync(int idDocumentoCliente, PresupuestoCabeceraRow datos, CancellationToken cancellationToken = default) =>
        _context.DocumentosCliente
            .Where(d => d.IdDocumentoCliente == idDocumentoCliente)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.IdCliente, datos.IdCliente)
                .SetProperty(d => d.RazonSocial, datos.RazonSocial)
                .SetProperty(d => d.IdCategoriaIva, datos.IdCategoriaIva)
                .SetProperty(d => d.NroDoc, datos.NroDoc)
                .SetProperty(d => d.IdProvincia, datos.IdProvincia)
                .SetProperty(d => d.IdLocalidad, datos.IdLocalidad)
                .SetProperty(d => d.Calle, datos.Calle)
                .SetProperty(d => d.TotalNeto, datos.Neto)
                .SetProperty(d => d.TotalIva, datos.Iva)
                .SetProperty(d => d.TotalOtrosImpuestos, datos.Otros)
                .SetProperty(d => d.TotalGeneral, datos.Total)
                .SetProperty(d => d.Observaciones, datos.Observaciones), cancellationToken);

    public Task BorrarDetallesAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        _context.DocumentosClienteDetalle.Where(d => d.IdDocumentoCliente == idDocumentoCliente).ExecuteDeleteAsync(cancellationToken);

    public Task BorrarMovimientosStockAsync(int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        _context.EntidadesCtaCteStockMovimientosDetalle
            .Where(s => s.IdComprobante == idComprobante && s.IdComprobanteTipo == idComprobanteTipo)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<IAsyncDisposable> BloquearAutorizacionAsync(int idDocumentoCliente, CancellationToken cancellationToken = default)
    {
        var recurso = $"elrenacer:afip:documento:{idDocumentoCliente}";
        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var resultado = await _context.Database
                .SqlQuery<int>($"""
                    DECLARE @r int;
                    EXEC @r = sp_getapplock @Resource = {recurso}, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0;
                    SELECT @r AS [Value];
                    """)
                .ToListAsync(cancellationToken);

            if (resultado.Count > 0 && resultado[0] < 0)
                throw new ConflictException($"El comprobante {idDocumentoCliente} se está autorizando en este momento. Reintentar en unos segundos.");

            return new LiberarLock(_context, recurso);
        }
        catch
        {
            await _context.Database.CloseConnectionAsync();
            throw;
        }
    }

    private const string AfipCaePendiente = "0";

    /// <summary>Libera el applock de sesión y devuelve la conexión.</summary>
    private sealed class LiberarLock(ElRenacerDbContext context, string recurso) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"EXEC sp_releaseapplock @Resource = {recurso}, @LockOwner = 'Session'");
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }

    public Task AnularDocumentoAsync(int idDocumentoCliente, int estado, DateTime ahora, CancellationToken cancellationToken = default) =>
        _context.DocumentosCliente.Where(d => d.IdDocumentoCliente == idDocumentoCliente)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.FechaEmision, ahora)
                .SetProperty(d => d.IdPlanillaCaja, 0)
                .SetProperty(d => d.Estado, estado), cancellationToken);
}
