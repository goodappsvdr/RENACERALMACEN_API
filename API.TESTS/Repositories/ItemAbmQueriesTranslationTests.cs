using API.DA.DbContexts;
using API.SERVICE.Repositories.Items;
using API.SERVICE.Repositories.Sistema;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace API.TESTS.Repositories;

/// <summary>Las consultas escritas a mano del ABM de ítems traducen a SQL (sin base).</summary>
public class ItemAbmQueriesTranslationTests
{
    private readonly OfflineSqlServerInterceptor _interceptor = new();
    private readonly ElRenacerDbContext _context;

    public ItemAbmQueriesTranslationTests()
    {
        var options = new DbContextOptionsBuilder<ElRenacerDbContext>()
            .UseSqlServer("Server=offline;Database=ELRENACER;TrustServerCertificate=True")
            .AddInterceptors(_interceptor)
            .Options;
        _context = new ElRenacerDbContext(options);
    }

    [Fact]
    public async Task GetSucursalesAsync_FiltraPorItemYOrdenaPorAlta()
    {
        await new ItemRepository(_context).GetSucursalesAsync(10);

        _interceptor.Commands.Should().ContainSingle().Which.Should()
            .Contain("[ID_Item] = @__idItem_0").And.Contain("ORDER BY [i].[ID_ItemSucursal]");
    }

    [Fact]
    public async Task GetUltimaActualizacionPrecioAsync_TraeLaMasReciente()
    {
        var result = await new ItemRepository(_context).GetUltimaActualizacionPrecioAsync(10);

        result.Should().BeNull();
        _interceptor.Commands.Should().ContainSingle().Which.Should()
            .Contain("TOP(1)").And.Contain("ORDER BY [i].[ID_ItemPrecioActulizacion] DESC");
    }

    [Fact]
    public async Task ReplaceImpuestoAsync_LeeLosImpuestosActualesDelItem()
    {
        await new ItemRepository(_context).ReplaceImpuestoAsync(10, 1);

        _interceptor.Commands.Should().ContainSingle().Which.Should().Contain("FROM [ItemsImpuestos]");
        _context.ChangeTracker.Entries().Should().ContainSingle(e => e.State == EntityState.Added);
    }

    [Fact]
    public async Task Parametro_GetValorAsync_IgnoraEspaciosFinalesComoElSp()
    {
        var valor = await new ParametroRepository(_context).GetValorAsync("CAMBIASTOCK ", "CAMBIASTOCK");

        valor.Should().BeNull();
        _interceptor.Commands.Should().ContainSingle().Which.Should().Contain("RTRIM([p].[Categoria])").And.Contain("RTRIM([p].[Nombre])");
    }
}
