using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces.Auth;
using API.SERVICE.Models.Auth;
using API.SERVICE.Security;
using API.SERVICE.UseCases.Auth;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace API.TESTS.UseCases;

public class LoginUseCaseTests
{
    private const string Salt = "AAECAwQFBgcICQoLDA0ODw==";
    private const string HashOfSecreto123 = "JQWLd73fvTZXfnT4t8BV9yxk1Rw=";

    private static readonly DateTime Now = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAuthRepository> _repository = new();
    private readonly Mock<IJwtTokenGenerator> _tokenGenerator = new();
    private readonly AspnetUsers _user;

    public LoginUseCaseTests()
    {
        _user = new AspnetUsers
        {
            UserId = Guid.NewGuid(),
            UserName = "cajera1",
            LoweredUserName = "cajera1",
            AspnetMembership = new AspnetMembership
            {
                Password = HashOfSecreto123,
                PasswordSalt = Salt,
                PasswordFormat = 1,
                IsApproved = true,
                FailedPasswordAttemptWindowStart = new DateTime(1754, 1, 1),
            },
            Role = [new AspnetRoles { RoleName = "CAJERA" }, new AspnetRoles { RoleName = "ADMINISTRADOR" }],
        };

        _repository.Setup(r => r.GetMembershipUserAsync("cajera1", It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _repository.Setup(r => r.GetUsuarioAsync("cajera1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Usuarios { IdUsuario = 7, IdSucursal = 2, Nombre = "Cajera Uno", Usuario = "cajera1" });
        _tokenGenerator.Setup(t => t.Generate(It.IsAny<AuthenticatedUser>())).Returns(("jwt", Now.AddHours(8)));
    }

    [Fact]
    public async Task ExecuteAsync_ValidCredentials_ReturnsTokenWithProfileAndRoles()
    {
        var result = await CreateSut().ExecuteAsync(Login("Secreto123"));

        result.Token.Should().Be("jwt");
        result.IdUsuario.Should().Be(7);
        result.IdSucursal.Should().Be(2);
        result.Roles.Should().Equal("ADMINISTRADOR", "CAJERA");
        _user.AspnetMembership!.LastLoginDate.Should().Be(Now);
        _tokenGenerator.Verify(t => t.Generate(It.Is<AuthenticatedUser>(u => u.UserId == _user.UserId && u.IdUsuario == 7)));
    }

    [Fact]
    public async Task ExecuteAsync_ValidCredentialsAfterFailures_ResetsFailedAttempts()
    {
        _user.AspnetMembership!.FailedPasswordAttemptCount = 3;

        await CreateSut().ExecuteAsync(Login("Secreto123"));

        _user.AspnetMembership.FailedPasswordAttemptCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WrongPassword_ThrowsUnauthorizedAndCountsAttempt()
    {
        var act = () => CreateSut().ExecuteAsync(Login("otra"));

        await act.Should().ThrowAsync<UnauthorizedException>();
        _user.AspnetMembership!.FailedPasswordAttemptCount.Should().Be(1);
        _user.AspnetMembership.FailedPasswordAttemptWindowStart.Should().Be(Now);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_FifthFailureWithinWindow_LocksUser()
    {
        _user.AspnetMembership!.FailedPasswordAttemptCount = 4;
        _user.AspnetMembership.FailedPasswordAttemptWindowStart = Now.AddMinutes(-5);

        var act = () => CreateSut().ExecuteAsync(Login("otra"));

        await act.Should().ThrowAsync<UnauthorizedException>();
        _user.AspnetMembership.IsLockedOut.Should().BeTrue();
        _user.AspnetMembership.LastLockoutDate.Should().Be(Now);
    }

    [Fact]
    public async Task ExecuteAsync_FailureAfterWindowExpired_RestartsCount()
    {
        _user.AspnetMembership!.FailedPasswordAttemptCount = 4;
        _user.AspnetMembership.FailedPasswordAttemptWindowStart = Now.AddMinutes(-11);

        var act = () => CreateSut().ExecuteAsync(Login("otra"));

        await act.Should().ThrowAsync<UnauthorizedException>();
        _user.AspnetMembership.FailedPasswordAttemptCount.Should().Be(1);
        _user.AspnetMembership.IsLockedOut.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_LockedUserWithCorrectPassword_ThrowsUnauthorized()
    {
        _user.AspnetMembership!.IsLockedOut = true;

        var act = () => CreateSut().ExecuteAsync(Login("Secreto123"));

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Usuario o contraseña inválidos.");
        _tokenGenerator.Verify(t => t.Generate(It.IsAny<AuthenticatedUser>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NotApprovedUser_ThrowsUnauthorized()
    {
        _user.AspnetMembership!.IsApproved = false;

        var act = () => CreateSut().ExecuteAsync(Login("Secreto123"));

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ExecuteAsync_UnknownUser_ThrowsUnauthorizedWithSameMessage()
    {
        var act = () => CreateSut().ExecuteAsync(new LoginDto { Usuario = "nadie", Password = "x" });

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Usuario o contraseña inválidos.");
    }

    private LoginUseCase CreateSut() => new(
        _repository.Object,
        _tokenGenerator.Object,
        Options.Create(new MembershipOptions()),
        NullLogger<LoginUseCase>.Instance,
        new FixedTimeProvider(Now));

    private static LoginDto Login(string password) => new() { Usuario = "cajera1", Password = password };

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
