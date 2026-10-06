using API.DA.DbContexts;
using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces.Clientes;
using Microsoft.EntityFrameworkCore;
using Db = global::API.DA.Entities;

namespace API.SERVICE.Repositories.Clientes;

/// <summary>
/// Las actualizaciones usan ExecuteUpdate/ExecuteDelete: generan el mismo UPDATE/DELETE por clave que el SP
/// correspondiente (incluida la aritmética sobre la columna) y corren dentro de la transacción del flujo.
/// </summary>
public sealed class ReciboCobroRepository : IReciboCobroRepository
{
    private const int IdEmpresa = 1;

    private readonly ElRenacerDbContext _context;

    public ReciboCobroRepository(ElRenacerDbContext context)
    {
        _context = context;
    }

    // ---------- Lecturas ----------

    public Task<Db.CajaPlanillas?> GetPlanillaAbiertaAsync(int idUsuario, int idComprobanteTipo, string letra, int estadoAbierta, CancellationToken cancellationToken = default) =>
        (from cp in _context.CajaPlanillas
         join pv in _context.PuntosVenta on cp.PuntoVenta equals pv.Descripcion
         where pv.IdComprobanteTipo == idComprobanteTipo
               && pv.Letra == letra
               && cp.Estado == estadoAbierta
               && cp.IdEmpresa == IdEmpresa
               && cp.IdUsuario == idUsuario
         orderby cp.IdPlanillaCaja
         select cp)
        .AsNoTracking()
        .FirstOrDefaultAsync(cancellationToken);

    public Task<long?> GetProximoNumeroAsync(string puntoVenta, string letra, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        _context.PuntosVenta.AsNoTracking()
            .Where(p => p.Descripcion == puntoVenta && p.IdEmpresa == IdEmpresa && p.Letra == letra && p.IdComprobanteTipo == idComprobanteTipo)
            .OrderByDescending(p => p.Nro)
            .Select(p => p.Nro + 1)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Db.Entidades?> GetEntidadAsync(int idEntidad, CancellationToken cancellationToken = default) =>
        _context.Entidades.AsNoTracking().FirstOrDefaultAsync(e => e.IdEntidad == idEntidad, cancellationToken);

    public Task<List<Db.EntidadesCtaCte>> GetCtaCtePendienteAsync(int idEntidad, int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        _context.EntidadesCtaCte.AsNoTracking()
            .Where(c => c.IdEntidad == idEntidad && c.IdComprobante == idComprobante && c.IdComprobanteTipo == idComprobanteTipo && c.Cancelado == false)
            .OrderBy(c => c.IdEntidadCtaCte)
            .ToListAsync(cancellationToken);

    public Task<List<ComprobantePendienteRow>> GetComprobantesPendientesAsync(int idEntidad, CancellationToken cancellationToken = default)
    {
        int[] invertidos = [4, 12, 9];
        return (from cta in _context.EntidadesCtaCte
                join e in _context.Entidades on cta.IdEntidad equals e.IdEntidad
                where cta.Cancelado == false && cta.IdEntidad == idEntidad && cta.Estado == ImputacionRules.EstadoCtaCtePendiente
                orderby invertidos.Contains(cta.IdComprobanteTipo!.Value) ? -cta.Saldo : cta.Saldo, cta.Fecha
                select new ComprobantePendienteRow(
                    cta.IdComprobante,
                    cta.IdComprobanteTipo,
                    cta.IdEntidad,
                    cta.Concepto,
                    e.RazonSocial,
                    cta.Fecha,
                    cta.FechaVencimiento,
                    cta.Saldo,
                    cta.InteresAplicado,
                    e.InteresCliente,
                    e.DiasInteres))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<Db.EntidadesRecibos?> GetReciboAsync(int idRecibo, CancellationToken cancellationToken = default) =>
        _context.EntidadesRecibos.AsNoTracking().FirstOrDefaultAsync(r => r.IdEntidadRecibo == idRecibo, cancellationToken);

    public Task<List<ImputacionRow>> GetImputacionesAsync(int idRecibo, CancellationToken cancellationToken = default) =>
        _context.EntidadRecibosDocumentosCliente.AsNoTracking()
            .Where(i => i.IdEntidadRecibo == idRecibo)
            .OrderBy(i => i.IdEntidadReciboDocumentoCliente)
            .Select(i => new ImputacionRow(
                i.IdEntidad,
                i.IdDocumentoCliente,
                i.IdComprobanteTipo,
                i.ImporteRecibo,
                _context.EntidadesCtaCte
                    .Where(c => c.IdComprobante == i.IdDocumentoCliente && c.IdComprobanteTipo == i.IdComprobanteTipo)
                    .OrderBy(c => c.IdEntidadCtaCte)
                    .Select(c => c.InteresAplicado)
                    .FirstOrDefault() ?? 0m))
            .ToListAsync(cancellationToken);

    public Task<bool> TieneRelacionAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        _context.DocumentosClienteRelacion.AnyAsync(r => r.IdDocumentoCliente1 == idDocumentoCliente, cancellationToken);

    public Task<bool> TieneImputacionesAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        _context.EntidadRecibosDocumentosCliente.AnyAsync(i => i.IdDocumentoCliente == idDocumentoCliente, cancellationToken);

    public Task<long?> GetIdCtaCteAsync(int idComprobanteTipo, int idComprobante, CancellationToken cancellationToken = default) =>
        _context.EntidadesCtaCte.AsNoTracking()
            .Where(c => c.IdComprobanteTipo == idComprobanteTipo && c.IdComprobante == idComprobante)
            .OrderBy(c => c.IdEntidadCtaCte)
            .Select(c => (long?)c.IdEntidadCtaCte)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<SaldoEntidadRow>> GetSaldosRecibosAutomaticosAsync(CancellationToken cancellationToken = default)
    {
        // Tipos fijos del SP del ERP (los mismos del reporte "clientes principales").
        int[] tipos = [3, 11, 8, 7];
        var saldos = from e in _context.Entidades
                     join cc in _context.EntidadesCtaCte on e.IdEntidad equals cc.IdEntidad
                     where e.EsHijo == false && tipos.Contains(cc.IdComprobanteTipo!.Value)
                     group cc.Total2 by new { e.IdEntidad, e.RazonSocial } into g
                     select new { g.Key.IdEntidad, g.Key.RazonSocial, Saldo = g.Sum() };

        // El filtro sobre la suma se traduce como HAVING (un NULL no pasa, igual que en el SP).
        return saldos
            .Where(s => s.Saldo > 0)
            .OrderByDescending(s => s.Saldo)
            .Select(s => new SaldoEntidadRow(s.IdEntidad, s.RazonSocial, s.Saldo ?? 0m))
            .ToListAsync(cancellationToken);
    }

    public Task<int?> GetIdSucursalLocalAsync(int idUsuario, CancellationToken cancellationToken = default) =>
        // Mismos joins que Sucursales_BuscarActivas_Usuario (incluido el estado 161 = activa, hardcodeado en el ERP).
        (from s in _context.Sucursales
         join p in _context.Provincias on s.IdProvincia equals p.IdProvincia
         join l in _context.Localidades on s.IdLocalidad equals l.IdLocalidad
         join es in _context.Estados on s.Estado equals es.IdEstado
         join u in _context.UsuariosSucursales on s.IdSucursal equals u.IdSucursal
         where s.Estado == 161 && u.IdUsuario == idUsuario && s.Descripcion!.Trim().ToUpper() == "LOCAL"
         orderby s.IdCategoriaIva
         select (int?)s.IdSucursal)
        .FirstOrDefaultAsync(cancellationToken);

    // ---------- Concurrencia ----------

    public async Task BloquearEntidadAsync(int idEntidad, CancellationToken cancellationToken = default)
    {
        var recurso = $"elrenacer:recibos:entidad:{idEntidad}";
        var resultado = await _context.Database
            .SqlQuery<int>($"""
                DECLARE @r int;
                EXEC @r = sp_getapplock @Resource = {recurso}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                SELECT @r AS [Value];
                """)
            .ToListAsync(cancellationToken);

        // 0 = obtenido, 1 = obtenido tras esperar; negativo = timeout / deadlock / error.
        if (resultado.Count > 0 && resultado[0] < 0)
            throw new ConflictException("Otro usuario está generando un recibo para este cliente. Reintentar en unos segundos.");
    }

    // ---------- Altas ----------

    public void Add<TEntity>(TEntity entity) where TEntity : class => _context.Set<TEntity>().Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);

    public async Task<long?> ReservarNumeroAsync(string puntoVenta, string letra, int idComprobanteTipo, CancellationToken cancellationToken = default)
    {
        // UPDATE ... OUTPUT reserva el número de forma atómica: el lock de la fila dura hasta el commit,
        // así que dos recibos simultáneos no pueden obtener el mismo número.
        var numeros = await _context.Database
            .SqlQuery<long?>($"""
                UPDATE PuntosVenta SET Nro = Nro + 1
                OUTPUT inserted.Nro AS [Value]
                WHERE ID_Empresa = {IdEmpresa} AND Letra = {letra} AND ID_ComprobanteTipo = {idComprobanteTipo} AND Descripcion = {puntoVenta}
                """)
            .ToListAsync(cancellationToken);

        return numeros.Max();
    }

    // ---------- Actualizaciones de la cobranza ----------

    public Task ImputarCtaCteAsync(int idComprobante, int idComprobanteTipo, decimal saldo, DateTime fechaPago, bool cancelado, decimal interesAplicado, CancellationToken cancellationToken = default) =>
        _context.EntidadesCtaCte
            .Where(c => c.IdComprobante == idComprobante && c.IdComprobanteTipo == idComprobanteTipo)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Saldo, saldo)
                .SetProperty(c => c.FechaPago, fechaPago.Date) // el SP recibe @FechaPago date
                .SetProperty(c => c.Cancelado, cancelado)
                .SetProperty(c => c.InteresAplicado, c => c.InteresAplicado + interesAplicado)
                .SetProperty(c => c.Total2, c => c.Total2 + interesAplicado), cancellationToken);

    public Task SetEstadoDocumentoClienteAsync(int idDocumentoCliente, int estado, CancellationToken cancellationToken = default) =>
        _context.DocumentosCliente.Where(d => d.IdDocumentoCliente == idDocumentoCliente)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, estado), cancellationToken);

    public Task SetEstadoDocumentoProveedorAsync(int idDocumentoProveedor, int estado, CancellationToken cancellationToken = default) =>
        _context.DocumentosProveedor.Where(d => d.IdDocumentoProveedor == idDocumentoProveedor)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Estado, estado), cancellationToken);

    public Task SetEstadoReciboAsync(int idRecibo, int estado, CancellationToken cancellationToken = default) =>
        _context.EntidadesRecibos.Where(r => r.IdEntidadRecibo == idRecibo)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Estado, estado), cancellationToken);

    public Task SetEstadoOrdenPagoAsync(int idProveedorRecibo, int estado, CancellationToken cancellationToken = default) =>
        _context.ProveedoresRecibos.Where(r => r.IdProveedorRecibo == idProveedorRecibo)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Estado, estado), cancellationToken);

    // ---------- Anulación ----------

    public Task AnularDetalleAsync(int idRecibo, DateTime ahora, CancellationToken cancellationToken = default) =>
        _context.EntidadesRecibosDetalle.Where(d => d.IdEntidadRecibo == idRecibo)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Descripcion, "ANULADO")
                .SetProperty(d => d.Detalle, "ANULADO")
                .SetProperty(d => d.Recepcion, ahora)
                .SetProperty(d => d.Emision, ahora)
                .SetProperty(d => d.Vto, ahora)
                .SetProperty(d => d.Nro, string.Empty), cancellationToken);

    public Task AnularCajaAsync(int idComprobanteTipo, int idComprobante, CancellationToken cancellationToken = default) =>
        _context.CajasPlanillasDetalle.Where(c => c.IdComprobanteTipo == idComprobanteTipo && c.IdComprobante == idComprobante)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Descripcion, "-ANULADA")
                .SetProperty(c => c.Obsevaciones, "ANULADA")
                .SetProperty(c => c.Automatico, true)
                .SetProperty(c => c.Total, 0m)
                .SetProperty(c => c.Debe, 0m)
                .SetProperty(c => c.Haber, 0m)
                .SetProperty(c => c.Total2, 0m), cancellationToken);

    public Task AnularChequesAsync(int idRecibo, int idComprobanteTipo, int estadoAnulado, CancellationToken cancellationToken = default) =>
        _context.EntidadesCheques.Where(c => c.IdEntidadRecibo == idRecibo && c.IdEntidadReciboTipo == idComprobanteTipo)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, estadoAnulado), cancellationToken);

    public Task AnularMovimientosBancoAsync(int idComprobanteTipo, int idComprobante, int estadoAnulado, DateTime ahora, CancellationToken cancellationToken = default) =>
        _context.BancosCuentasMovimientos.Where(m => m.IdComprobanteTipo == idComprobanteTipo && m.IdComprobante == idComprobante)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Debe, 0m)
                .SetProperty(m => m.Haber, 0m)
                .SetProperty(m => m.Importe, 0m)
                .SetProperty(m => m.Fecha, ahora)
                .SetProperty(m => m.IdBancoOrigen, 0)
                .SetProperty(m => m.IdBancoSucursalOrigen, 0)
                .SetProperty(m => m.CuentaTipoOrigen, 0)
                .SetProperty(m => m.NroCuentaOrigen, "0")
                .SetProperty(m => m.IdBancoDestino, 0)
                .SetProperty(m => m.IdBancoSucursalDestino, 0)
                .SetProperty(m => m.CuentaTipoDestino, 0)
                .SetProperty(m => m.NroCuentaDestino, "ANULADO")
                .SetProperty(m => m.Estado, estadoAnulado)
                .SetProperty(m => m.Total, 0m), cancellationToken);

    public Task AnularRetencionesAsync(int idComprobanteTipo, int idComprobante, int estadoAnulado, CancellationToken cancellationToken = default) =>
        _context.Retenciones.Where(r => r.IdComprobanteTipo == idComprobanteTipo && r.IdComprobante == idComprobante)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Descripcion, "ANULADA")
                .SetProperty(r => r.NroComprobante, "ANULADA")
                .SetProperty(r => r.Total, 0m)
                .SetProperty(r => r.Estado, estadoAnulado), cancellationToken);

    public Task RevertirImputacionCtaCteAsync(int idComprobante, int idComprobanteTipo, decimal saldo, decimal interesAplicado, DateTime ahora, CancellationToken cancellationToken = default) =>
        _context.EntidadesCtaCte
            .Where(c => c.IdComprobante == idComprobante && c.IdComprobanteTipo == idComprobanteTipo)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Saldo, c => c.Saldo + saldo)
                .SetProperty(c => c.Total2, c => c.Total2 - interesAplicado)
                .SetProperty(c => c.InteresAplicado, c => c.InteresAplicado - interesAplicado)
                .SetProperty(c => c.Cancelado, false)
                .SetProperty(c => c.FechaAnulacion, ahora), cancellationToken);

    public Task BorrarImputacionesAsync(int idRecibo, CancellationToken cancellationToken = default) =>
        _context.EntidadRecibosDocumentosCliente.Where(i => i.IdEntidadRecibo == idRecibo).ExecuteDeleteAsync(cancellationToken);

    public async Task AnularCtaCteAsync(long idEntidadCtaCte, int estadoAnulado, DateTime ahora, CancellationToken cancellationToken = default)
    {
        await _context.EntidadesCtaCte.Where(c => c.IdEntidadCtaCte == idEntidadCtaCte)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Concepto, "COMPROBANTE ANULADO")
                .SetProperty(c => c.Total, 0m)
                .SetProperty(c => c.Saldo, 0m)
                .SetProperty(c => c.Cancelado, true)
                .SetProperty(c => c.FechaAnulacion, ahora)
                .SetProperty(c => c.Estado, estadoAnulado)
                .SetProperty(c => c.Total2, 0m), cancellationToken);

        await _context.EntidadesCtaCteMovimientos.Where(m => m.IdEntidadCtaCte == idEntidadCtaCte)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Concepto, "COMPROBANTE ANULADO")
                .SetProperty(m => m.AfavorEntidad, 0m)
                .SetProperty(m => m.EnContraEntidad, 0m), cancellationToken);
    }
}
