using API.DA.DbContexts;
using API.SERVICE.Interfaces.Caja;
using API.SERVICE.Repositories.Caja;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace API.TESTS.Repositories;

/// <summary>Cada operación del repositorio de planillas de caja se traduce a SQL contra el proveedor real (sin base).</summary>
public class PlanillaCajaQueriesTranslationTests
{
    private readonly OfflineSqlServerInterceptor _interceptor = new();
    private readonly PlanillaCajaRepository _repo;

    public PlanillaCajaQueriesTranslationTests()
    {
        var options = new DbContextOptionsBuilder<ElRenacerDbContext>()
            .UseSqlServer("Server=offline;Database=ELRENACER;TrustServerCertificate=True", sql => sql.UseCompatibilityLevel(130))
            .AddInterceptors(_interceptor)
            .Options;
        _repo = new PlanillaCajaRepository(new ElRenacerDbContext(options));
    }

    private string LastSql => _interceptor.Commands.Last();

    [Fact]
    public async Task Lecturas_TraducenASql()
    {
        await _repo.GetAsync(10);
        await _repo.GetDeSucursalesDelUsuarioAsync(38);
        await _repo.TienePlanillaEnEstadoAsync(38, 45);
        await _repo.GetSaldoAnteriorAsync(38);
        await _repo.GetTotalesDetalleAsync(10);
        await _repo.GetPuntosVentaAsync();
        await _repo.GetPuntoVentaSucursalAsync(2);
        await _repo.GetUsuarioAsync(38);
        await _repo.OperaSucursalAsync(38, 2);

        _interceptor.Commands.Should().HaveCount(9);
        _interceptor.Commands.ElementAt(1).Should().Contain("FROM [UsuariosSucursales]").And.Contain("ORDER BY [c].[ID_PlanillaCaja] DESC");
        _interceptor.Commands.ElementAt(3).Should().Contain("TOP(1)").And.Contain("ORDER BY [c].[ID_PlanillaCaja] DESC");
        _interceptor.Commands.ElementAt(4).Should().Contain("SUM([t].[Debe])").And.Contain("SUM([t].[Haber])").And.Contain("FROM [CajasPlanillasDetalle]");
        _interceptor.Commands.ElementAt(5).Should().Contain("DISTINCT");
    }

    [Fact]
    public async Task Modificar_UnUpdatePorClave()
    {
        await _repo.ModificarAsync(10, new PlanillaCajaCierre(new DateTime(2026, 10, 6, 21, 0, 0), 0, 1000, 0, 1000, 0, 44));

        LastSql.Should().StartWith("UPDATE").And.Contain("[Diferencia] = @").And.Contain("[Estado] = @").And.Contain("[ID_PlanillaCaja] = @");
    }

    [Fact]
    public async Task Bloqueo_UsaAppLockDeTransaccion()
    {
        await _repo.BloquearUsuarioAsync(38);

        LastSql.Should().Contain("sp_getapplock").And.Contain("'Transaction'");
    }
}
