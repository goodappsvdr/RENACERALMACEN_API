namespace API.SERVICE.Security;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) Generate(AuthenticatedUser user);
}

public sealed record AuthenticatedUser(Guid UserId, string UserName, int? IdUsuario, int? IdSucursal, IReadOnlyList<string> Roles);
