using API.DA.DbContexts;
using API.SERVICE.Interfaces.Sistema;
using Microsoft.EntityFrameworkCore;
using Db = global::API.DA.Entities;

namespace API.SERVICE.Repositories.Sistema;

public sealed class UsuarioAdminRepository : IUsuarioAdminRepository
{
    /// <summary>Estado "ACTIVA" de Sucursales (hardcodeado en Sucursales_BuscarActivas).</summary>
    private const int SucursalActiva = 161;

    private readonly ElRenacerDbContext _context;

    public UsuarioAdminRepository(ElRenacerDbContext context)
    {
        _context = context;
    }

    public Task<List<UsuarioListaRow>> GetUsuariosAsync(CancellationToken cancellationToken = default) =>
        (from u in _context.Usuarios.AsNoTracking()
         join e in _context.Estados.AsNoTracking() on u.IdEstado equals e.IdEstado
         orderby u.Nombre
         select new UsuarioListaRow(
             u.IdUsuario, u.Nombre, u.Usuario, u.Email, u.IdEstado, e.Nombre,
             _context.AspnetUsers.Where(a => a.UserId == u.UserId).SelectMany(a => a.Role).Select(r => r.RoleName).FirstOrDefault()))
        .ToListAsync(cancellationToken);

    public Task<Db.Usuarios?> GetUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default) =>
        _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario, cancellationToken);

    public Task<Db.AspnetUsers?> GetMembershipAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.AspnetUsers
            .Include(u => u.AspnetMembership)
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

    public Task<bool> ExisteUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        var lowered = userName.ToLowerInvariant();
        return _context.AspnetUsers.AnyAsync(u => u.LoweredUserName == lowered, cancellationToken);
    }

    public Task<List<Db.AspnetRoles>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        _context.AspnetRoles.AsNoTracking().OrderBy(r => r.RoleName).ToListAsync(cancellationToken);

    public Task<Db.AspnetRoles?> GetRolAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        _context.AspnetRoles.FirstOrDefaultAsync(r => r.RoleId == roleId, cancellationToken);

    public Task<List<Db.Sucursales>> GetSucursalesActivasAsync(CancellationToken cancellationToken = default) =>
        _context.Sucursales.AsNoTracking().Where(s => s.Estado == SucursalActiva).OrderBy(s => s.IdSucursal).ToListAsync(cancellationToken);

    public Task<List<int>> GetSucursalesDeUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default) =>
        _context.UsuariosSucursales.AsNoTracking()
            .Where(us => us.IdUsuario == idUsuario && us.IdSucursal != null)
            .Select(us => us.IdSucursal!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task ReemplazarSucursalesAsync(int idUsuario, IReadOnlyCollection<int> sucursales, CancellationToken cancellationToken = default)
    {
        await _context.UsuariosSucursales.Where(us => us.IdUsuario == idUsuario).ExecuteDeleteAsync(cancellationToken);
        foreach (var idSucursal in sucursales)
            _context.UsuariosSucursales.Add(new Db.UsuariosSucursales { IdUsuario = idUsuario, IdSucursal = idSucursal });
    }

    public void Add<TEntity>(TEntity entity) where TEntity : class => _context.Set<TEntity>().Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
