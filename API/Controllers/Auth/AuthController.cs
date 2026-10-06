using API.SERVICE.Models.Auth;
using API.SERVICE.Models.Common;
using API.SERVICE.UseCases.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Auth;

/// <summary>Autenticación contra los usuarios del ERP (ASP.NET Membership).</summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Login: devuelve un JWT para usar como Bearer en el resto de los endpoints.</summary>
    // [AllowAnonymous] justificado: es el endpoint que emite el token.
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginDto dto, [FromServices] ILoginUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(dto, cancellationToken));
}
