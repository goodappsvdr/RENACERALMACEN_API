using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces.Auth;
using API.SERVICE.Models.Auth;
using API.SERVICE.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.SERVICE.UseCases.Auth;

public interface ILoginUseCase
{
    Task<LoginResponse> ExecuteAsync(LoginDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Login contra ASP.NET Membership replicando SqlMembershipProvider.ValidateUser:
/// usuario aprobado y no bloqueado, contador de intentos fallidos con ventana, y bloqueo al superar el máximo.
/// </summary>
public sealed class LoginUseCase : ILoginUseCase
{
    // Valor "sin fecha" que usa Membership en las columnas datetime NOT NULL.
    private static readonly DateTime MembershipMinDate = new(1754, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string InvalidCredentialsMessage = "Usuario o contraseña inválidos.";

    private readonly IAuthRepository _repository;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly MembershipOptions _membershipOptions;
    private readonly ILogger<LoginUseCase> _logger;
    private readonly TimeProvider _timeProvider;

    public LoginUseCase(
        IAuthRepository repository,
        IJwtTokenGenerator tokenGenerator,
        IOptions<MembershipOptions> membershipOptions,
        ILogger<LoginUseCase> logger,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
        _membershipOptions = membershipOptions.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<LoginResponse> ExecuteAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetMembershipUserAsync(dto.Usuario, cancellationToken);
        var membership = user?.AspnetMembership;

        // Mismo mensaje para todos los casos: no revelar si el usuario existe o está bloqueado.
        if (user is null || membership is null || !membership.IsApproved || membership.IsLockedOut)
        {
            _logger.LogInformation("Login rechazado para {UserName}: inexistente, no aprobado o bloqueado", dto.Usuario);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        if (!MembershipPasswordHasher.Verify(dto.Password, membership.Password, membership.PasswordSalt, membership.PasswordFormat))
        {
            RegisterFailedAttempt(membership, nowUtc);
            await _repository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Login fallido para {UserName} (intentos: {Attempts})", user.UserName, membership.FailedPasswordAttemptCount);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        membership.FailedPasswordAttemptCount = 0;
        membership.FailedPasswordAttemptWindowStart = MembershipMinDate;
        membership.LastLoginDate = nowUtc;
        user.LastActivityDate = nowUtc;
        await _repository.SaveChangesAsync(cancellationToken);

        var usuario = await _repository.GetUsuarioAsync(user.UserName, cancellationToken);
        var roles = user.Role.Select(r => r.RoleName).OrderBy(r => r).ToList();

        var (token, expiresAtUtc) = _tokenGenerator.Generate(
            new AuthenticatedUser(user.UserId, user.UserName, usuario?.IdUsuario, usuario?.IdSucursal, roles));

        return new LoginResponse(token, expiresAtUtc, user.UserName, usuario?.Nombre, usuario?.Imagen, usuario?.IdUsuario, usuario?.IdSucursal, roles);
    }

    private void RegisterFailedAttempt(AspnetMembership membership, DateTime nowUtc)
    {
        var windowEnd = membership.FailedPasswordAttemptWindowStart.AddMinutes(_membershipOptions.PasswordAttemptWindowMinutes);

        if (membership.FailedPasswordAttemptCount == 0 || nowUtc > windowEnd)
        {
            membership.FailedPasswordAttemptCount = 1;
            membership.FailedPasswordAttemptWindowStart = nowUtc;
        }
        else
        {
            membership.FailedPasswordAttemptCount++;
        }

        if (membership.FailedPasswordAttemptCount >= _membershipOptions.MaxInvalidPasswordAttempts)
        {
            membership.IsLockedOut = true;
            membership.LastLockoutDate = nowUtc;
        }
    }
}
