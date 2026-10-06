using Db = global::API.DA.Entities;

namespace API.SERVICE.Interfaces.Sistema;

/// <summary>
/// Usuarios del ERP: fila de Usuarios + usuario de ASP.NET Membership (aspnet_Users / aspnet_Membership / aspnet_UsersInRoles)
/// + sucursales que opera (UsuariosSucursales). Se usa dentro de <see cref="IUnitOfWork"/>.
/// </summary>
public interface IUsuarioAdminRepository
{
    Task<List<UsuarioListaRow>> GetUsuariosAsync(CancellationToken cancellationToken = default);

    /// <summary>Usuario del ERP (con seguimiento de cambios).</summary>
    Task<Db.Usuarios?> GetUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Usuario de Membership con su membresía y roles (con seguimiento de cambios).</summary>
    Task<Db.AspnetUsers?> GetMembershipAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ExisteUserNameAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>aspnet_Roles_BuscarTodos.</summary>
    Task<List<Db.AspnetRoles>> GetRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>Rol con seguimiento de cambios (para asignarlo).</summary>
    Task<Db.AspnetRoles?> GetRolAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Sucursales_BuscarActivas.</summary>
    Task<List<Db.Sucursales>> GetSucursalesActivasAsync(CancellationToken cancellationToken = default);

    Task<List<int>> GetSucursalesDeUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>UsuariosSucursales_Eliminar + UsuariosSucursales_Agregar.</summary>
    Task ReemplazarSucursalesAsync(int idUsuario, IReadOnlyCollection<int> sucursales, CancellationToken cancellationToken = default);

    void Add<TEntity>(TEntity entity) where TEntity : class;

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record UsuarioListaRow(int IdUsuario, string? Nombre, string? Usuario, string? Email, int? IdEstado, string? Estado, string? Rol);
