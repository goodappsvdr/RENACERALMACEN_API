namespace API.SERVICE.Interfaces;

/// <summary>Usuario autenticado del request (sale del JWT). Lo implementa la capa API.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    string? UserName { get; }

    /// <summary>ID_Usuario de la tabla Usuarios del ERP; null si el usuario de Membership no tiene fila ahí.</summary>
    int? IdUsuario { get; }

    int? IdSucursal { get; }

    /// <summary>Rol de ASP.NET Membership (aspnet_Roles), incluido en el JWT.</summary>
    bool IsInRole(string role);
}
