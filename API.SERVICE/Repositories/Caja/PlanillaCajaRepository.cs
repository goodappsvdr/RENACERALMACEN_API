using API.DA.DbContexts;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces.Caja;
using Microsoft.EntityFrameworkCore;
using Db = global::API.DA.Entities;

namespace API.SERVICE.Repositories.Caja;

public sealed class PlanillaCajaRepository : IPlanillaCajaRepository
{
    private readonly ElRenacerDbContext _context;

    public PlanillaCajaRepository(ElRenacerDbContext context)
    {
        _context = context;
    }

    public Task<Db.CajaPlanillas?> GetAsync(int idPlanillaCaja, CancellationToken cancellationToken = default) =>
        _context.CajaPlanillas.AsNoTracking().FirstOrDefaultAsync(p => p.IdPlanillaCaja == idPlanillaCaja, cancellationToken);

    public Task<List<PlanillaCajaRow>> GetDeSucursalesDelUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default) =>
        (from p in _context.CajaPlanillas.AsNoTracking()
         join u in _context.Usuarios.AsNoTracking() on p.IdUsuario equals u.IdUsuario
         join e in _context.Estados.AsNoTracking() on p.Estado equals e.IdEstado
         where _context.UsuariosSucursales.Any(us => us.IdUsuario == idUsuario && us.IdSucursal == p.IdSucursal)
         orderby p.IdPlanillaCaja descending
         select new PlanillaCajaRow(p, u.Nombre, e.Nombre))
        .ToListAsync(cancellationToken);

    public Task<bool> TienePlanillaEnEstadoAsync(int idUsuario, int estado, CancellationToken cancellationToken = default) =>
        _context.CajaPlanillas.AnyAsync(p => p.IdUsuario == idUsuario && p.Estado == estado, cancellationToken);

    public async Task<decimal> GetSaldoAnteriorAsync(int idUsuario, CancellationToken cancellationToken = default) =>
        await _context.CajaPlanillas.AsNoTracking()
            .Where(p => p.IdUsuario == idUsuario)
            .OrderByDescending(p => p.IdPlanillaCaja)
            .Select(p => p.Diferencia)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

    public async Task<(decimal Debe, decimal Haber)> GetTotalesDetalleAsync(int idPlanillaCaja, CancellationToken cancellationToken = default)
    {
        var totales = await _context.CajasPlanillasDetalle.AsNoTracking()
            .Where(d => d.IdCajaPlanilla == idPlanillaCaja)
            .GroupBy(d => 1)
            .Select(g => new { Debe = g.Sum(d => (decimal?)d.Debe), Haber = g.Sum(d => d.Haber) })
            .FirstOrDefaultAsync(cancellationToken);
        return (totales?.Debe ?? 0m, totales?.Haber ?? 0m);
    }

    public Task<List<string>> GetPuntosVentaAsync(CancellationToken cancellationToken = default) =>
        _context.PuntosVenta.AsNoTracking()
            .Where(p => p.Descripcion != null)
            .Select(p => p.Descripcion!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(cancellationToken);

    public Task<string?> GetPuntoVentaSucursalAsync(int idSucursal, CancellationToken cancellationToken = default) =>
        _context.Sucursales.AsNoTracking()
            .Where(s => s.IdSucursal == idSucursal && _context.PuntosVenta.Any(p => p.Descripcion == s.PuntoVenta))
            .Select(s => s.PuntoVenta)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Db.Usuarios?> GetUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default) =>
        _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.IdUsuario == idUsuario, cancellationToken);

    public Task<bool> OperaSucursalAsync(int idUsuario, int idSucursal, CancellationToken cancellationToken = default) =>
        _context.UsuariosSucursales.AnyAsync(us => us.IdUsuario == idUsuario && us.IdSucursal == idSucursal, cancellationToken);

    public void Add(Db.CajaPlanillas planilla) => _context.CajaPlanillas.Add(planilla);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);

    public Task ModificarAsync(int idPlanillaCaja, PlanillaCajaCierre datos, CancellationToken cancellationToken = default) =>
        _context.CajaPlanillas
            .Where(p => p.IdPlanillaCaja == idPlanillaCaja)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.FechaCierre, datos.FechaCierre)
                .SetProperty(p => p.SaldoInicial, datos.SaldoInicial)
                .SetProperty(p => p.TotalIngresos, datos.TotalIngresos)
                .SetProperty(p => p.TotalEgresos, datos.TotalEgresos)
                .SetProperty(p => p.TotalRendido, datos.TotalRendido)
                .SetProperty(p => p.Diferencia, datos.Diferencia)
                .SetProperty(p => p.Estado, datos.Estado), cancellationToken);

    public async Task BloquearUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default)
    {
        var recurso = $"elrenacer:caja:usuario:{idUsuario}";
        var resultado = await _context.Database
            .SqlQuery<int>($"""
                DECLARE @r int;
                EXEC @r = sp_getapplock @Resource = {recurso}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
                SELECT @r AS [Value];
                """)
            .ToListAsync(cancellationToken);

        if (resultado.Count > 0 && resultado[0] < 0)
            throw new ConflictException("Otra operación está abriendo o cerrando la caja de este usuario. Reintentar en unos segundos.");
    }
}
