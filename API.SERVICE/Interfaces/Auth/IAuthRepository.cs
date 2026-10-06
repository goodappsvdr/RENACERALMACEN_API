using API.DA.Entities;

namespace API.SERVICE.Interfaces.Auth;

/// <summary>Acceso a las tablas de ASP.NET Membership (aspnet_*) y Usuarios para el login.</summary>
public interface IAuthRepository
{
    /// <summary>Usuario de Membership con su registro de membership y roles, trackeado (se actualizan contadores).</summary>
    Task<AspnetUsers?> GetMembershipUserAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>Perfil de la tabla Usuarios del ERP (nombre, imagen, sucursal).</summary>
    Task<Usuarios?> GetUsuarioAsync(string userName, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
