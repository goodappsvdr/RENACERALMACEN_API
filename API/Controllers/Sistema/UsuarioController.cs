using API.Security;
using API.SERVICE.Models.Common;
using API.SERVICE.Models.Sistema;
using API.SERVICE.UseCases.Sistema;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Sistema;

/// <summary>
/// Administración de usuarios (FrmUsuariosABM) y cambio de contraseña propia (FrmCambiarPass). Mantiene sincronizados
/// Usuarios, aspnet_Users / aspnet_Membership / aspnet_UsersInRoles y UsuariosSucursales.
/// </summary>
public sealed partial class UsuarioController
{
    /// <summary>Usuarios del ERP con rol y estado. Solo ADMINISTRADOR.</summary>
    [HttpGet("gestion")]
    [ProducesResponseType(typeof(IReadOnlyList<UsuarioAdminListaDisplay>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<UsuarioAdminListaDisplay>>> Gestion([FromServices] IGetUsuariosAdminUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(cancellationToken));

    /// <summary>Roles y sucursales activas para el alta / modificación. Solo ADMINISTRADOR.</summary>
    [HttpGet("gestion/opciones")]
    [ProducesResponseType(typeof(UsuarioAdminOpcionesDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UsuarioAdminOpcionesDisplay>> GestionOpciones([FromServices] IGetUsuarioAdminOpcionesUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(cancellationToken));

    /// <summary>Usuario con rol y sucursales (nunca la contraseña). Solo ADMINISTRADOR.</summary>
    [HttpGet("gestion/{id:int}")]
    [ProducesResponseType(typeof(UsuarioAdminDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioAdminDisplay>> GestionGetById(int id, [FromServices] IGetUsuarioAdminUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(id, cancellationToken));

    /// <summary>Alta de usuario (Membership + Usuarios + rol + sucursales). 409 si el nombre de usuario está en uso. Solo ADMINISTRADOR.</summary>
    [HttpPost("gestion")]
    [ProducesResponseType(typeof(UsuarioAdminDisplay), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioAdminDisplay>> GestionCreate(
        [FromBody] CreateUsuarioAdminDto dto, [FromServices] ICreateUsuarioAdminUseCase useCase, CancellationToken cancellationToken)
    {
        var usuario = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GestionGetById), new { id = usuario.IdUsuario }, usuario);
    }

    /// <summary>Modificación: datos, estado, rol, sucursales y, si se informa, contraseña. Solo ADMINISTRADOR.</summary>
    [HttpPut("gestion/{id:int}")]
    [ProducesResponseType(typeof(UsuarioAdminDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioAdminDisplay>> GestionUpdate(
        int id, [FromBody] UpdateUsuarioAdminDto dto, [FromServices] IUpdateUsuarioAdminUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(id, dto, cancellationToken));

    /// <summary>Roles de Membership (FrmRolesABM). Solo ADMINISTRADOR.</summary>
    [HttpGet("gestion/roles")]
    [ProducesResponseType(typeof(IReadOnlyList<RolDisplay>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<RolDisplay>>> Roles([FromServices] IGetRolesUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(cancellationToken));

    /// <summary>Alta de rol (nombre en mayúsculas). 409 si ya existe. Solo ADMINISTRADOR.</summary>
    [HttpPost("gestion/roles")]
    [ProducesResponseType(typeof(RolDisplay), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RolDisplay>> CreateRol([FromBody] RolDto dto, [FromServices] ICreateRolUseCase useCase, CancellationToken cancellationToken)
    {
        var rol = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(Roles), null, rol);
    }

    /// <summary>Renombre de rol. 409 si es un rol del sistema (ADMINISTRADOR, CEO, CTO) o el nombre ya existe. Solo ADMINISTRADOR.</summary>
    [HttpPut("gestion/roles/{idRol:guid}")]
    [ProducesResponseType(typeof(RolDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RolDisplay>> RenombrarRol(
        Guid idRol, [FromBody] RolDto dto, [FromServices] IRenombrarRolUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(idRol, dto, cancellationToken));

    /// <summary>Cambio de la contraseña del usuario logueado. 400 si la actual no es correcta.</summary>
    [HttpPost("cambiar-password")]
    [LibreDeArea]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CambiarPassword(
        [FromBody] CambiarPasswordDto dto, [FromServices] ICambiarPasswordUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.ExecuteAsync(dto, cancellationToken);
        return NoContent();
    }
}
