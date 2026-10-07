using System.ComponentModel.DataAnnotations;

namespace API.SERVICE.Models.Sistema;

/// <summary>Alta de usuario (Usuarios_Agregar_Ws de FrmUsuariosABM).</summary>
public sealed class CreateUsuarioAdminDto : UsuarioAdminDtoBase
{
    /// <summary>Nombre de usuario para el login (aspnet_Users.UserName). No se puede cambiar después.</summary>
    [Required, MaxLength(256), RegularExpression(@"^\S+$", ErrorMessage = "El usuario no puede tener espacios.")]
    public string Usuario { get; set; } = string.Empty;

    [Required, MinLength(UsuarioAdminReglas.LargoMinimoPassword), MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Modificación (Usuarios_Modificar_Ws). La contraseña solo cambia si se informa.</summary>
public sealed class UpdateUsuarioAdminDto : UsuarioAdminDtoBase
{
    [MinLength(UsuarioAdminReglas.LargoMinimoPassword), MaxLength(128)]
    public string? Password { get; set; }
}

public abstract class UsuarioAdminDtoBase
{
    [Required, MaxLength(500)] public string Nombre { get; set; } = string.Empty;

    /// <summary>aspnet_Membership.Email es nvarchar(256); el SP de modificación del ERP lo recibía como varchar(50).</summary>
    [Required, MaxLength(256), EmailAddress] public string Email { get; set; } = string.Empty;

    [Required] public Guid? IdRol { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>Sucursales que opera (UsuariosSucursales). La primera es su sucursal principal (Usuarios.ID_Sucursal).</summary>
    [Required, MinLength(1)] public List<int> Sucursales { get; set; } = [];
}

/// <summary>Cambio de la contraseña propia (CambiarPass_Ws de FrmCambiarPass).</summary>
public sealed class CambiarPasswordDto
{
    [Required] public string PasswordActual { get; set; } = string.Empty;

    [Required, MinLength(UsuarioAdminReglas.LargoMinimoPassword), MaxLength(128)]
    public string PasswordNueva { get; set; } = string.Empty;
}

public static class UsuarioAdminReglas
{
    /// <summary>El Web.config del ERP pide 2; la API exige 8 para contraseñas nuevas (las existentes siguen valiendo).</summary>
    public const int LargoMinimoPassword = 8;
}

public sealed record UsuarioAdminListaDisplay(int IdUsuario, string? Nombre, string? Usuario, string? Email, bool Activo, string? Rol);

/// <summary>Detalle para editar. Nunca incluye la contraseña (el ERP la devolvía en claro).</summary>
public sealed record UsuarioAdminDisplay(
    int IdUsuario, string? Nombre, string? Usuario, string? Email, bool Activo, bool Bloqueado, Guid? IdRol, string? Rol, int? IdSucursal, IReadOnlyList<int> Sucursales);

public sealed record RolDisplay(Guid IdRol, string Nombre);

public sealed record SucursalOpcionDisplay(int IdSucursal, string? Descripcion, string? PuntoVenta);

public sealed record UsuarioAdminOpcionesDisplay(IReadOnlyList<RolDisplay> Roles, IReadOnlyList<SucursalOpcionDisplay> Sucursales);

/// <summary>Alta / renombre de rol (Roles_Agregar_Ws / Roles_Modificar_Ws). El nombre se guarda en mayúsculas.</summary>
public sealed class RolDto
{
    [Required, MaxLength(256)] public string Nombre { get; set; } = string.Empty;
}
