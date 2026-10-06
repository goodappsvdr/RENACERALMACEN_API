using API.DA.DbContexts;
using API.SERVICE.Repositories.Ventas;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace API.TESTS.Repositories;

/// <summary>Cada operación del repositorio de ventas se traduce a SQL contra el proveedor real (sin base).</summary>
public class VentaQueriesTranslationTests
{
    private static readonly DateTime Ahora = new(2026, 10, 6, 17, 0, 0);

    private readonly OfflineSqlServerInterceptor _interceptor = new();
    private readonly VentaRepository _repo;

    public VentaQueriesTranslationTests()
    {
        var options = new DbContextOptionsBuilder<ElRenacerDbContext>()
            .UseSqlServer("Server=offline;Database=ELRENACER;TrustServerCertificate=True", sql => sql.UseCompatibilityLevel(130))
            .AddInterceptors(_interceptor)
            .Options;
        _repo = new VentaRepository(new ElRenacerDbContext(options));
    }

    private string LastSql => _interceptor.Commands.Last();

    [Fact]
    public async Task Lecturas_TraducenASql()
    {
        await _repo.GetOfertaActivaAsync(1, 10);
        await _repo.TienePendienteRemitarAsync(80);
        await _repo.GetDocumentoAsync(700);
        await _repo.GetDetallesAsync(700);
        await _repo.GetRemitosAsociadosAsync(700);
        await _repo.GetMovimientosStockAsync(700, 11);
        await _repo.GetReciboImputadoAsync(700, 11);

        _interceptor.Commands.Should().HaveCount(7);
    }

    [Fact]
    public async Task Sumas_TraducenASql()
    {
        // SUM sobre un resultado vacío: el interceptor no devuelve filas, alcanza con que la consulta se arme.
        var saldo = async () => await _repo.GetSaldoCtaCteAsync(5);
        var stock = async () => await _repo.GetSaldoStockAsync(80, 2);

        await saldo.Should().ThrowAsync<InvalidOperationException>("el lector vacío no trae la fila del SUM");
        await stock.Should().ThrowAsync<InvalidOperationException>();
        _interceptor.Commands.Should().Contain(sql => sql.Contains("SUM([e].[Total2])"));
    }

    [Fact]
    public async Task OfertaActiva_FiltraEstado198YSucursal()
    {
        await _repo.GetOfertaActivaAsync(1, 10);

        LastSql.Should().Contain("= 198").And.Contain("FROM [OfertasSucursales]");
    }

    [Fact]
    public async Task PendienteRemitar_MismosTiposYSaldoQueElSp()
    {
        await _repo.TienePendienteRemitarAsync(80);

        LastSql.Should().Contain("INNER JOIN [DocumentosClienteDetalle]").And.Contain("<> 0.0");
    }

    [Fact]
    public async Task Stock_SoloItemsQueMuevenStockYConAritmeticaSobreLaColumna()
    {
        await _repo.RestarStockAsync(10, 1, 2, Ahora);

        _interceptor.Commands.Should().HaveCount(2);
        _interceptor.Commands.First().Should().Contain("[StockActual] = [i].[StockActual] - @").And.Contain("[MueveStock] = CAST(1 AS bit)");
        LastSql.Should().Contain("[Stock] = [i].[Stock] - @").And.Contain("[FechaVenta] = @");
    }

    [Fact]
    public async Task Escrituras_TraducenASql()
    {
        await _repo.SumarStockAsync(10, 1, 2, Ahora);
        await _repo.AjustarSaldoStockAsync(80, 81, 2, 10, -3);
        await _repo.AnularMovimientosStockAsync(700, 11);
        await _repo.AjustarOfertaDisponibleAsync(33, -2);
        await _repo.AsignarNroSerieAsync(555, 11, 700, 701, 1);
        await _repo.LiberarNrosSerieAsync(11, 700, 1);
        await _repo.SetEstadoPendienteAsync(80, 1, true);
        await _repo.DeterminarRemitarFacturarAsync(700, true, false, true);
        await _repo.AnularDetalleAsync(701, 1);
        await _repo.BorrarRemitoAsociadoAsync(1);
        await _repo.AnularDocumentoAsync(700, 1, Ahora);

        _interceptor.Commands.Should().HaveCount(12, "sumar stock actualiza Items e ItemsSucursales");
        _interceptor.Commands.Should().Contain(sql => sql.Contains("UPDATE OfertasAgotamiento SET CantidadDisponible = CantidadDisponible + @"));
        _interceptor.Commands.Should().Contain(sql => sql.StartsWith("DELETE") && sql.Contains("[DocumentosClienteRemitos]"));
    }
}
