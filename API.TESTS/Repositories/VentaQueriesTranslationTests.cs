using API.DA.DbContexts;
using API.SERVICE.Interfaces.Ventas;
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
    public async Task FacturaElectronica_TraducenASql()
    {
        await _repo.GetSucursalAsync(1);
        await _repo.GetPendientesAfipAsync(3, 99);
        await _repo.ModificarDatosAfipAsync(800, "0002", "00000123", "71234567890123", "30712345678...");

        _interceptor.Commands.Should().HaveCount(3);
        _interceptor.Commands.ElementAt(1).Should().Contain("[d].[CAE] = '0'");
        LastSql.Should().StartWith("UPDATE").And.Contain("[CAE] = @").And.NotContain("[ID_PuntoVenta]", "el SP del ERP no lo modifica");
    }

    [Fact]
    public async Task NotaCredito_TraducenASql()
    {
        await _repo.GetRecibosImputadosAsync(800, 3);
        var total = async () => await _repo.GetTotalNotasCreditoAsync(800, 4, 99, 0);
        await total.Should().ThrowAsync<InvalidOperationException>("el lector vacío no trae la fila del SUM");
        await _repo.GetFacturaDeNotaCreditoAsync(900);
        await _repo.BorrarRelacionAsync(800, 900);

        _interceptor.Commands.Should().HaveCount(4);
        _interceptor.Commands.ElementAt(0).Should().Contain("DISTINCT").And.Contain("FROM [EntidadRecibosDocumentosCliente]");
        _interceptor.Commands.ElementAt(1).Should().Contain("SUM(").And.Contain("INNER JOIN [DocumentosCliente]");
        _interceptor.Commands.ElementAt(2).Should().Contain("TOP(1)").And.Contain("FROM [DocumentosClienteRelacion]");
        LastSql.Should().StartWith("DELETE").And.Contain("[DocumentosClienteRelacion]");
    }

    [Fact]
    public async Task Remitos_TraducenASql()
    {
        await _repo.GetLetraAsync(2, 1);
        await _repo.GetRelacionesComoRemitoAsync(300);
        await _repo.GetComprobantesParaRemitirAsync(5, [3, 11, 1], [43, 122]);
        await _repo.GetLineasPendientesAsync(200);

        _interceptor.Commands.Should().HaveCount(4);
        _interceptor.Commands.ElementAt(0).Should().Contain("FROM [ComprobantesLetras]").And.Contain("= 1");
        _interceptor.Commands.ElementAt(1).Should().Contain("FROM [DocumentosClienteRemitos]").And.Contain("[ID_Remito] = @");
        _interceptor.Commands.ElementAt(2).Should().Contain("[Remitar] = CAST(1 AS bit)").And.Contain("NOT IN").And.Contain("OPENJSON");
        LastSql.Should().Contain("INNER JOIN [DocumentosClienteDetalle]").And.Contain("<> 0.0");
    }

    [Fact]
    public async Task Presupuestos_TraducenASql()
    {
        await _repo.ModificarPresupuestoAsync(381, new PresupuestoCabeceraRow(5, "CLIENTE", 1, "20123456789", 1, 2, "CALLE", 100, 21, 0, 121, "OBS"));
        await _repo.BorrarDetallesAsync(381);
        await _repo.BorrarMovimientosStockAsync(381, 1);

        _interceptor.Commands.Should().HaveCount(3);
        _interceptor.Commands.ElementAt(0).Should().StartWith("UPDATE").And.Contain("[TotalGeneral] = @").And.Contain("[ID_Cliente] = @")
            .And.NotContain("[Numero]", "el SP no toca número ni letra");
        _interceptor.Commands.ElementAt(1).Should().StartWith("DELETE").And.Contain("[DocumentosClienteDetalle]");
        LastSql.Should().StartWith("DELETE").And.Contain("[EntidadesCtaCteStockMovimientosDetalle]").And.Contain("[ID_ComprobanteTipo] = @");
    }

    [Fact]
    public async Task BloquearAutorizacion_UsaAppLockDeSesionYLoLibera()
    {
        await using (await _repo.BloquearAutorizacionAsync(800))
        {
            _interceptor.Commands.Last().Should().Contain("sp_getapplock").And.Contain("@LockOwner = 'Session'");
        }

        LastSql.Should().Contain("sp_releaseapplock");
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
