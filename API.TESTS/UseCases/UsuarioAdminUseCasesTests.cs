using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Models.Sistema;
using API.SERVICE.Security;
using API.SERVICE.UseCases.Sistema;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Moq;

namespace API.TESTS.UseCases;

/// <summary>Administración de usuarios (FrmUsuariosABM / FrmCambiarPass) sincronizando Membership.</summary>
public class UsuarioAdminUseCasesTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 6, 23, 15, 42, 123, TimeSpan.Zero);
    private static readonly Guid App = Guid.NewGuid();
    private static readonly Guid RolCajera = Guid.NewGuid();
    private static readonly Guid RolAdmin = Guid.NewGuid();

    private readonly FakeUsuarioAdminRepository _repo = new();
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();
    private readonly FixedTimeProvider _time = new(Ahora);

    public UsuarioAdminUseCasesTests()
    {
        _user.SetupGet(u => u.IdUsuario).Returns(1);
        _user.Setup(u => u.IsInRole("ADMINISTRADOR")).Returns(true);
        _repo.Roles.Add(new AspnetRoles { ApplicationId = App, RoleId = RolCajera, RoleName = "CAJERA", LoweredRoleName = "cajera" });
        _repo.Roles.Add(new AspnetRoles { ApplicationId = App, RoleId = RolAdmin, RoleName = "ADMINISTRADOR", LoweredRoleName = "administrador" });
        _repo.Sucursales.AddRange([new Sucursales { IdSucursal = 1, Estado = 161 }, new Sucursales { IdSucursal = 2, Estado = 161 }]);
        _ref.Parametros[("USER", "EMPRESA")] = "https://ejemplo/favicon.png";
    }

    // ---------- Alta ----------

    [Fact]
    public async Task Create_MembershipCompatibleRolUsuarioYSucursales_SinPasswordEnClaro()
    {
        var r = await CreateSut().ExecuteAsync(Dto("rosana", "clave-segura-1", 2, 1));

        var aspnet = _repo.Added.OfType<AspnetUsers>().Single();
        aspnet.Should().Match<AspnetUsers>(a => a.UserName == "rosana" && a.LoweredUserName == "rosana" && a.ApplicationId == App && !a.IsAnonymous);
        aspnet.Role.Should().ContainSingle(x => x.RoleId == RolCajera);
        var m = aspnet.AspnetMembership!;
        m.Should().Match<AspnetMembership>(x =>
            x.PasswordFormat == 1 && x.IsApproved && !x.IsLockedOut && x.Email == "rosana@ejemplo.com" && x.LoweredEmail == "rosana@ejemplo.com"
            && x.CreateDate == new DateTime(2026, 10, 6, 23, 15, 42) && x.LastLockoutDate == new DateTime(1754, 1, 1));
        MembershipPasswordHasher.Verify("clave-segura-1", m.Password, m.PasswordSalt, m.PasswordFormat).Should().BeTrue("el login (API y WebForms) usa este hash");
        Convert.FromBase64String(m.PasswordSalt).Should().HaveCount(16);

        var u = _repo.Added.OfType<Usuarios>().Single();
        u.Should().Match<Usuarios>(x =>
            x.Usuario == "rosana" && x.UserId == aspnet.UserId && x.Pass == "" && x.IdSucursal == 2 && x.IdEstado == 15 && x.Imagen == "https://ejemplo/favicon.png");
        _repo.SucursalesPorUsuario[u.IdUsuario].Should().Equal(2, 1);
        r.Should().Match<UsuarioAdminDisplay>(x => x.IdRol == RolCajera && x.Rol == "CAJERA" && x.Activo);
    }

    [Fact]
    public async Task Create_Inactivo_NoQuedaAprobadoEnMembership()
    {
        var dto = Dto("baja", "clave-segura-1", 1);
        dto.Activo = false;

        await CreateSut().ExecuteAsync(dto);

        _repo.Added.OfType<AspnetUsers>().Single().AspnetMembership!.IsApproved.Should().BeFalse("el ERP lo dejaba aprobado aunque se diera de alta inactivo");
        _repo.Added.OfType<Usuarios>().Single().IdEstado.Should().Be(16);
    }

    [Fact]
    public async Task Create_UsuarioEnUso_409()
    {
        _repo.UserNames.Add("rosana");

        var act = () => CreateSut().ExecuteAsync(Dto("Rosana", "clave-segura-1", 1));

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*en uso*");
        _repo.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_SucursalInactiva_400()
    {
        var act = () => CreateSut().ExecuteAsync(Dto("nuevo", "clave-segura-1", 9));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*9*");
    }

    [Fact]
    public async Task Create_RolInexistente_400()
    {
        var dto = Dto("nuevo", "clave-segura-1", 1);
        dto.IdRol = Guid.NewGuid();

        var act = () => CreateSut().ExecuteAsync(dto);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*rol*");
    }

    [Fact]
    public async Task SinRolAdministrador_403()
    {
        _user.Setup(u => u.IsInRole("ADMINISTRADOR")).Returns(false);

        await FluentActions.Invoking(() => CreateSut().ExecuteAsync(Dto("x", "clave-segura-1", 1))).Should().ThrowAsync<ForbiddenException>();
        await FluentActions.Invoking(() => new GetUsuariosAdminUseCase(_repo, _user.Object).ExecuteAsync()).Should().ThrowAsync<ForbiddenException>();
        await FluentActions.Invoking(() => UpdateSut().ExecuteAsync(38, Update(RolCajera))).Should().ThrowAsync<ForbiddenException>();
    }

    // ---------- Modificación ----------

    [Fact]
    public async Task Update_SincronizaMembershipRolSucursalesYNoTocaLaPasswordSiNoViene()
    {
        var (usuario, aspnet) = Existente();
        var hashPrevio = aspnet.AspnetMembership!.Password;

        await UpdateSut().ExecuteAsync(38, Update(RolAdmin, activo: false, sucursales: [1]));

        usuario.Should().Match<Usuarios>(u => u.Nombre == "Rosana E." && u.Email == "nueva@ejemplo.com" && u.IdEstado == 16 && u.IdSucursal == 1);
        usuario.Pass.Should().Be("vieja-en-claro", "sin contraseña nueva no se toca");
        aspnet.AspnetMembership!.Should().Match<AspnetMembership>(m => !m.IsApproved && m.Email == "nueva@ejemplo.com" && m.Password == hashPrevio);
        aspnet.Role.Should().ContainSingle(r => r.RoleId == RolAdmin);
        _repo.SucursalesPorUsuario[38].Should().Equal(1);
    }

    [Fact]
    public async Task Update_ConPassword_NuevoHashYBorraLaCopiaEnClaro()
    {
        var (usuario, aspnet) = Existente();
        var dto = Update(RolCajera);
        dto.Password = "otra-clave-99";

        await UpdateSut().ExecuteAsync(38, dto);

        var m = aspnet.AspnetMembership!;
        MembershipPasswordHasher.Verify("otra-clave-99", m.Password, m.PasswordSalt, m.PasswordFormat).Should().BeTrue();
        m.LastPasswordChangedDate.Should().Be(new DateTime(2026, 10, 6, 23, 15, 42));
        usuario.Pass.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_DarseDeBajaASiMismo_400()
    {
        Existente(idUsuario: 1);

        var act = () => UpdateSut().ExecuteAsync(1, Update(RolAdmin, activo: false));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*sí mismo*");
    }

    [Fact]
    public async Task Get_NuncaDevuelveLaPassword()
    {
        Existente();

        var r = await new GetUsuarioAdminUseCase(_repo, _user.Object).ExecuteAsync(38);

        r.Should().Match<UsuarioAdminDisplay>(x => x.Usuario == "rosana" && x.IdRol == RolCajera && x.Sucursales.SequenceEqual(new[] { 2 }));
        typeof(UsuarioAdminDisplay).GetProperties().Select(p => p.Name).Should().NotContain(n => n.Contains("Pass"));
    }

    // ---------- Cambio de contraseña propia ----------

    [Fact]
    public async Task CambiarPassword_ValidaContraElHashDeMembership()
    {
        var (usuario, aspnet) = Existente(password: "actual-123");
        _user.SetupGet(u => u.UserId).Returns(aspnet.UserId);
        _user.SetupGet(u => u.IdUsuario).Returns(38);

        await CambiarSut().ExecuteAsync(new CambiarPasswordDto { PasswordActual = "actual-123", PasswordNueva = "nueva-clave-1" });

        var m = aspnet.AspnetMembership!;
        MembershipPasswordHasher.Verify("nueva-clave-1", m.Password, m.PasswordSalt, m.PasswordFormat).Should().BeTrue();
        usuario.Pass.Should().BeEmpty();
    }

    [Fact]
    public async Task CambiarPassword_ActualIncorrecta_400()
    {
        var (_, aspnet) = Existente(password: "actual-123");
        _user.SetupGet(u => u.UserId).Returns(aspnet.UserId);

        var act = () => CambiarSut().ExecuteAsync(new CambiarPasswordDto { PasswordActual = "vieja-en-claro", PasswordNueva = "nueva-clave-1" });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no es correcta*");
    }

    // ---------- Roles ----------

    [Fact]
    public async Task CreateRol_MayusculasEspaciosNormalizadosYMismaAplicacion()
    {
        var r = await new CreateRolUseCase(_repo, new InlineUnitOfWork(), _user.Object).ExecuteAsync(new RolDto { Nombre = "  deposito   central " });

        var rol = _repo.Added.OfType<AspnetRoles>().Single();
        rol.Should().Match<AspnetRoles>(x => x.RoleName == "DEPOSITO CENTRAL" && x.LoweredRoleName == "deposito central" && x.ApplicationId == App);
        r.Nombre.Should().Be("DEPOSITO CENTRAL");
    }

    [Fact]
    public async Task CreateRol_Existente_409()
    {
        var act = () => new CreateRolUseCase(_repo, new InlineUnitOfWork(), _user.Object).ExecuteAsync(new RolDto { Nombre = "Cajera" });

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*ya existe*");
    }

    [Fact]
    public async Task RenombrarRol_ActualizaNombreYDescripcion()
    {
        var r = await new RenombrarRolUseCase(_repo, new InlineUnitOfWork(), _user.Object).ExecuteAsync(RolCajera, new RolDto { Nombre = "caja" });

        _repo.Roles.Single(x => x.RoleId == RolCajera).Should().Match<AspnetRoles>(x => x.RoleName == "CAJA" && x.LoweredRoleName == "caja" && x.Description == "CAJA");
        r.Nombre.Should().Be("CAJA");
    }

    [Fact]
    public async Task RenombrarRol_DelSistema_409()
    {
        var act = () => new RenombrarRolUseCase(_repo, new InlineUnitOfWork(), _user.Object).ExecuteAsync(RolAdmin, new RolDto { Nombre = "ADMIN" });

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*permisos*");
    }

    [Fact]
    public async Task RenombrarRol_ANombreDeOtro_409()
    {
        var act = () => new RenombrarRolUseCase(_repo, new InlineUnitOfWork(), _user.Object).ExecuteAsync(RolCajera, new RolDto { Nombre = "administrador" });

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*ya existe*");
    }

    [Fact]
    public async Task Roles_SinAdministrador_403()
    {
        _user.Setup(u => u.IsInRole("ADMINISTRADOR")).Returns(false);

        await FluentActions.Invoking(() => new CreateRolUseCase(_repo, new InlineUnitOfWork(), _user.Object).ExecuteAsync(new RolDto { Nombre = "X" }))
            .Should().ThrowAsync<ForbiddenException>();
    }

    // ---------- helpers ----------

    private CreateUsuarioAdminUseCase CreateSut() => new(_repo, _ref, new InlineUnitOfWork(), _time, _user.Object);

    private UpdateUsuarioAdminUseCase UpdateSut() => new(_repo, new InlineUnitOfWork(), _time, _user.Object);

    private CambiarPasswordUseCase CambiarSut() => new(_repo, new InlineUnitOfWork(), _time, _user.Object);

    private static CreateUsuarioAdminDto Dto(string usuario, string password, params int[] sucursales) => new()
    {
        Usuario = usuario,
        Password = password,
        Nombre = "Rosana",
        Email = $"{usuario.ToLowerInvariant()}@ejemplo.com",
        IdRol = RolCajera,
        Sucursales = [.. sucursales],
    };

    private static UpdateUsuarioAdminDto Update(Guid rol, bool activo = true, int[]? sucursales = null) => new()
    {
        Nombre = "Rosana E.",
        Email = "nueva@ejemplo.com",
        IdRol = rol,
        Activo = activo,
        Sucursales = [.. sucursales ?? [2]],
    };

    private (Usuarios Usuario, AspnetUsers Aspnet) Existente(int idUsuario = 38, string password = "actual-123")
    {
        var (hash, salt) = MembershipPasswordHasher.Create(password);
        var aspnet = new AspnetUsers
        {
            ApplicationId = App, UserId = Guid.NewGuid(), UserName = "rosana", LoweredUserName = "rosana",
            AspnetMembership = new AspnetMembership { ApplicationId = App, Password = hash, PasswordSalt = salt, PasswordFormat = 1, IsApproved = true, Email = "r@ejemplo.com" },
        };
        aspnet.Role.Add(_repo.Roles.First(r => r.RoleId == RolCajera));
        var usuario = new Usuarios
        {
            IdUsuario = idUsuario, Nombre = "Rosana", Usuario = "rosana", Email = "r@ejemplo.com", Pass = "vieja-en-claro", UserId = aspnet.UserId, IdSucursal = 2, IdEstado = 15,
        };
        _repo.Usuarios[idUsuario] = usuario;
        _repo.Memberships[aspnet.UserId] = aspnet;
        _repo.SucursalesPorUsuario[idUsuario] = [2];
        return (usuario, aspnet);
    }
}

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public sealed class FakeUsuarioAdminRepository : IUsuarioAdminRepository
{
    private int _nextId = 100;

    public Dictionary<int, Usuarios> Usuarios { get; } = [];
    public Dictionary<Guid, AspnetUsers> Memberships { get; } = [];
    public HashSet<string> UserNames { get; } = [];
    public List<AspnetRoles> Roles { get; } = [];
    public List<Sucursales> Sucursales { get; } = [];
    public Dictionary<int, List<int>> SucursalesPorUsuario { get; } = [];
    public List<object> Added { get; } = [];

    public Task<List<UsuarioListaRow>> GetUsuariosAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Usuarios.Values.Select(u => new UsuarioListaRow(u.IdUsuario, u.Nombre, u.Usuario, u.Email, u.IdEstado, null, null)).ToList());

    public Task<Usuarios?> GetUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default) =>
        Task.FromResult(Usuarios.GetValueOrDefault(idUsuario));

    public Task<AspnetUsers?> GetMembershipAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Memberships.GetValueOrDefault(userId));

    public Task<bool> ExisteUserNameAsync(string userName, CancellationToken cancellationToken = default) =>
        Task.FromResult(UserNames.Contains(userName.ToLowerInvariant()));

    public Task<List<AspnetRoles>> GetRolesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Roles.ToList());

    public Task<AspnetRoles?> GetRolAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Roles.FirstOrDefault(r => r.RoleId == roleId));

    public Task<bool> ExisteRolAsync(string nombre, Guid? excluir, CancellationToken cancellationToken = default) =>
        Task.FromResult(Roles.Concat(Added.OfType<AspnetRoles>()).Any(r => r.LoweredRoleName == nombre.ToLowerInvariant() && r.RoleId != excluir));

    public Task<Guid?> GetApplicationIdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Roles.Select(r => (Guid?)r.ApplicationId).FirstOrDefault());

    public Task<List<Sucursales>> GetSucursalesActivasAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Sucursales.Where(s => s.Estado == 161).ToList());

    public Task<List<int>> GetSucursalesDeUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default) =>
        Task.FromResult(SucursalesPorUsuario.GetValueOrDefault(idUsuario) ?? []);

    public Task ReemplazarSucursalesAsync(int idUsuario, IReadOnlyCollection<int> sucursales, CancellationToken cancellationToken = default)
    {
        SucursalesPorUsuario[idUsuario] = [.. sucursales];
        return Task.CompletedTask;
    }

    public void Add<TEntity>(TEntity entity) where TEntity : class => Added.Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var u in Added.OfType<Usuarios>().Where(u => u.IdUsuario == 0))
            u.IdUsuario = _nextId++;
        return Task.CompletedTask;
    }
}
