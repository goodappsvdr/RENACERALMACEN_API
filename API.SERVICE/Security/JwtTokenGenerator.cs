using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace API.SERVICE.Security;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    public const string IdUsuarioClaim = "id_usuario";
    public const string IdSucursalClaim = "id_sucursal";

    private readonly JwtOptions _options;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAtUtc) Generate(AuthenticatedUser user)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (user.IdUsuario is not null)
            claims.Add(new Claim(IdUsuarioClaim, user.IdUsuario.Value.ToString()));
        if (user.IdSucursal is not null)
            claims.Add(new Claim(IdSucursalClaim, user.IdSucursal.Value.ToString()));
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
