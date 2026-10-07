using API.DA.DbContexts;
using API.SERVICE.Repositories.Sistema;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace API.TESTS.Repositories;

/// <summary>Cada operación del repositorio de administración de usuarios se traduce a SQL contra el proveedor real (sin base).</summary>
public class UsuarioAdminQueriesTranslationTests
{
    private readonly OfflineSqlServerInterceptor _interceptor = new();
    private readonly UsuarioAdminRepository _repo;

    public UsuarioAdminQueriesTranslationTests()
    {
        var options = new DbContextOptionsBuilder<ElRenacerDbContext>()
            .UseSqlServer("Server=offline;Database=ELRENACER;TrustServerCertificate=True", sql => sql.UseCompatibilityLevel(130))
            .AddInterceptors(_interceptor)
            .Options;
        _repo = new UsuarioAdminRepository(new ElRenacerDbContext(options));
    }

    [Fact]
    public async Task Lecturas_TraducenASql()
    {
        await _repo.GetUsuariosAsync();
        await _repo.GetUsuarioAsync(38);
        await _repo.GetMembershipAsync(Guid.NewGuid());
        await _repo.ExisteUserNameAsync("Rosana");
        await _repo.GetRolesAsync();
        await _repo.GetRolAsync(Guid.NewGuid());
        await _repo.GetSucursalesActivasAsync();
        await _repo.GetSucursalesDeUsuarioAsync(38);
        await _repo.ExisteRolAsync("Cajera", Guid.NewGuid());
        await _repo.GetApplicationIdAsync();

        _interceptor.Commands.Should().HaveCount(10);
        _interceptor.Commands.ElementAt(8).Should().Contain("[LoweredRoleName] = @").And.Contain("[RoleId] <> @");
        _interceptor.Commands.ElementAt(0).Should().Contain("[aspnet_UsersInRoles]").And.Contain("[RoleName]").And.NotContain("[Pass]");
        _interceptor.Commands.ElementAt(2).Should().Contain("[aspnet_Membership]").And.Contain("[aspnet_UsersInRoles]");
        _interceptor.Commands.ElementAt(3).Should().Contain("[LoweredUserName] = @");
        _interceptor.Commands.ElementAt(6).Should().Contain("[Estado] = 161");
    }

    [Fact]
    public async Task ReemplazarSucursales_BorraYAgrega()
    {
        await _repo.ReemplazarSucursalesAsync(38, [1, 2]);

        _interceptor.Commands.Single().Should().StartWith("DELETE").And.Contain("[UsuariosSucursales]").And.Contain("[ID_Usuario] = @");
    }
}
