using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using API.SERVICE.Interfaces;
using API.SERVICE.Security;

namespace API.Security;

/// <summary>Lee el usuario del JWT del request actual.</summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(Claim(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public string? UserName => Claim(JwtRegisteredClaimNames.UniqueName);

    public int? IdUsuario => int.TryParse(Claim(JwtTokenGenerator.IdUsuarioClaim), out var id) ? id : null;

    public int? IdSucursal => int.TryParse(Claim(JwtTokenGenerator.IdSucursalClaim), out var id) ? id : null;

    private string? Claim(string type) => Principal?.FindFirst(type)?.Value;
}
