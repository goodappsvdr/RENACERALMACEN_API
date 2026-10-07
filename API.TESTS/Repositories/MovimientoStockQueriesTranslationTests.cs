using API.DA.DbContexts;
using API.SERVICE.Repositories.Stock;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace API.TESTS.Repositories;

/// <summary>Cada operación del repositorio de movimientos de stock entre sucursales se traduce a SQL contra el proveedor real (sin base).</summary>
public class MovimientoStockQueriesTranslationTests
{
    private static readonly DateTime Ahora = new(2026, 10, 8, 10, 0, 0);

    private readonly OfflineSqlServerInterceptor _interceptor = new();
    private readonly MovimientoStockRepository _repo;

    public MovimientoStockQueriesTranslationTests()
    {
        var options = new DbContextOptionsBuilder<ElRenacerDbContext>()
            .UseSqlServer("Server=offline;Database=ELRENACER;TrustServerCertificate=True", sql => sql.UseCompatibilityLevel(130))
            .AddInterceptors(_interceptor)
            .Options;
        _repo = new MovimientoStockRepository(new ElRenacerDbContext(options));
    }

    [Fact]
    public async Task Lecturas_TraducenASql()
    {
        await _repo.GetSucursalAsync(1);
        await _repo.OperaSucursalAsync(3, 1);
        await _repo.GetItemsAsync([10, 11], 1);
        await _repo.GetMovimientoAsync(450, 17);
        await _repo.GetDetallesAsync(450);
        await _repo.GetEnTransitoAsync(2, 17, 42);

        _interceptor.Commands.Should().HaveCount(6);
        _interceptor.Commands.ElementAt(2).Should().Contain("[ItemsSucursales]").And.Contain("IN (", "una sola consulta para todos los ítems");
        _interceptor.Commands.ElementAt(3).Should().Contain("[ID_ComprobanteTipo] = @");
        _interceptor.Commands.ElementAt(5).Should().Contain("[ID_Cliente] = @").And.Contain("[Estado] = @");
    }

    [Fact]
    public async Task Escrituras_TraducenASql()
    {
        await _repo.CambiarEstadoAsync(450, 171, 42, null);
        await _repo.CambiarEstadoAsync(450, 43, 42, Ahora);
        await _repo.MoverStockSucursalAsync(10, 1, -4, Ahora);
        await _repo.AnularDetallesAsync(450, 2);

        _interceptor.Commands.Should().HaveCount(4).And.OnlyContain(c => c.StartsWith("UPDATE"));
        _interceptor.Commands.ElementAt(0).Should().Contain("[Estado] = @", "solo si sigue en tránsito");
        _interceptor.Commands.ElementAt(1).Should().Contain("[FechaEmision]");
        _interceptor.Commands.ElementAt(2).Should().Contain("[ItemsSucursales]").And.Contain("[MueveStock]").And.NotContain("[Items] ",
            "una transferencia no cambia el stock total");
        _interceptor.Commands.ElementAt(3).Should().Contain("[DocumentosClienteDetalle]");
    }
}
