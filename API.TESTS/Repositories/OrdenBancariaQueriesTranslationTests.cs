using API.DA.DbContexts;
using API.SERVICE.Repositories.Bancos;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace API.TESTS.Repositories;

/// <summary>Cada operación del repositorio de órdenes de depósito / extracción se traduce a SQL contra el proveedor real (sin base).</summary>
public class OrdenBancariaQueriesTranslationTests
{
    private static readonly DateTime Ahora = new(2026, 10, 7, 22, 0, 0);

    private readonly OfflineSqlServerInterceptor _interceptor = new();
    private readonly OrdenBancariaRepository _repo;

    public OrdenBancariaQueriesTranslationTests()
    {
        var options = new DbContextOptionsBuilder<ElRenacerDbContext>()
            .UseSqlServer("Server=offline;Database=ELRENACER;TrustServerCertificate=True", sql => sql.UseCompatibilityLevel(130))
            .AddInterceptors(_interceptor)
            .Options;
        _repo = new OrdenBancariaRepository(new ElRenacerDbContext(options));
    }

    [Fact]
    public async Task Lecturas_TraducenASql()
    {
        await _repo.GetCuentaAsync(15);
        await _repo.GetDepositoAsync(610);
        await _repo.GetExtraccionAsync(610);

        _interceptor.Commands.Should().HaveCount(3);
        _interceptor.Commands.ElementAt(0).Should().Contain("[BancosCuentas]");
        _interceptor.Commands.ElementAt(1).Should().Contain("[OrdenesDepositos]");
        _interceptor.Commands.ElementAt(2).Should().Contain("[OrdenesExrtacciones]");
    }

    [Fact]
    public async Task Escrituras_TraducenASql()
    {
        await _repo.AnularDepositoAsync(610, 2, 1, Ahora);
        await _repo.AnularExtraccionAsync(610, 2, 1, Ahora);
        await _repo.AnularDetalleDepositoAsync(610, Ahora);
        await _repo.AnularDetalleExtraccionAsync(610, Ahora);

        _interceptor.Commands.Should().HaveCount(4).And.OnlyContain(c => c.StartsWith("UPDATE"));
        _interceptor.Commands.ElementAt(0).Should().Contain("[OrdenesDepositos]").And.Contain("[Estado] = @", "solo si sigue GENERADA");
        _interceptor.Commands.ElementAt(1).Should().Contain("[OrdenesExrtacciones]").And.Contain("[Estado] = @");
        _interceptor.Commands.ElementAt(2).Should().Contain("[OrdenesDepositosDetalle]");
        _interceptor.Commands.ElementAt(3).Should().Contain("[OrdenesExtraccionesDetalle]");
    }
}
