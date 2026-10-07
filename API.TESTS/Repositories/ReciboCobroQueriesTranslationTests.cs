using API.DA.DbContexts;
using API.SERVICE.Repositories.Clientes;
using API.SERVICE.Repositories.Sistema;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace API.TESTS.Repositories;

/// <summary>
/// Cada operación del repositorio de recibos se traduce a SQL contra el proveedor real (sin base).
/// Verifica además que las actualizaciones reproduzcan la aritmética de los SPs del ERP.
/// </summary>
public class ReciboCobroQueriesTranslationTests
{
    private static readonly DateTime Ahora = new(2026, 10, 6, 11, 0, 0);

    private readonly OfflineSqlServerInterceptor _interceptor = new();
    private readonly ElRenacerDbContext _context;
    private readonly ReciboCobroRepository _repo;

    public ReciboCobroQueriesTranslationTests()
    {
        var options = new DbContextOptionsBuilder<ElRenacerDbContext>()
            .UseSqlServer("Server=offline;Database=ELRENACER;TrustServerCertificate=True", sql => sql.UseCompatibilityLevel(130))
            .AddInterceptors(_interceptor)
            .Options;
        _context = new ElRenacerDbContext(options);
        _repo = new ReciboCobroRepository(_context);
    }

    private string LastSql => _interceptor.Commands.Last();

    [Fact]
    public async Task Lecturas_TraducenASql()
    {
        await _repo.GetPlanillaAbiertaAsync(3, 7, "X", 45);
        await _repo.GetProximoNumeroAsync("0003", "X", 7);
        await _repo.GetEntidadAsync(5);
        await _repo.GetCtaCtePendienteAsync(5, 50, 11);
        await _repo.GetComprobantesPendientesAsync(5);
        await _repo.GetReciboAsync(300);
        await _repo.GetImputacionesAsync(300);
        await _repo.TieneRelacionAsync(51);
        await _repo.TieneImputacionesAsync(52);
        await _repo.GetIdCtaCteAsync(7, 300);

        _interceptor.Commands.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetPlanillaAbierta_JoinConPuntosVentaComoElSp()
    {
        await _repo.GetPlanillaAbiertaAsync(3, 7, "X", 45);

        LastSql.Should().Contain("INNER JOIN [PuntosVenta]").And.Contain("[c].[PuntoVenta] = [p].[Descripcion]");
    }

    [Fact]
    public async Task ComprobantesPendientes_FiltraEstado48YNoCancelados()
    {
        await _repo.GetComprobantesPendientesAsync(5);

        LastSql.Should().Contain("[e].[Cancelado] = CAST(0 AS bit)").And.Contain("= 48").And.Contain("OPENJSON");
    }

    [Fact]
    public async Task SaldosRecibosAutomaticos_AgrupaYFiltraSaldosPositivosComoElSp()
    {
        await _repo.GetSaldosRecibosAutomaticosAsync();

        LastSql.Should().Contain("[e].[EsHijo] = CAST(0 AS bit)").And.Contain("GROUP BY").And.Contain("HAVING").And.Contain("SUM([e0].[Total2])");
    }

    [Fact]
    public async Task SucursalLocal_MismosJoinsYEstadoQueElSp()
    {
        await _repo.GetIdSucursalLocalAsync(3);

        LastSql.Should().Contain("INNER JOIN [UsuariosSucursales]").And.Contain("INNER JOIN [Localidades]").And.Contain("= 161").And.Contain("UPPER(LTRIM(RTRIM([s].[Descripcion]))) = 'LOCAL'");
    }

    [Fact]
    public async Task BloquearEntidad_UsaAppLockDeTransaccion()
    {
        await _repo.BloquearEntidadAsync(5);

        LastSql.Should().Contain("sp_getapplock").And.Contain("@LockOwner = 'Transaction'");
    }

    [Fact]
    public async Task ReservarNumero_IncrementaYDevuelveAtomico()
    {
        var numero = await _repo.ReservarNumeroAsync("0003", "X", 7);

        numero.Should().BeNull("el interceptor no devuelve filas");
        LastSql.Should().Contain("UPDATE PuntosVenta SET Nro = Nro + 1").And.Contain("OUTPUT inserted.Nro");
    }

    [Fact]
    public async Task ImputarCtaCte_AcumulaInteresSobreLaColumnaComoElSp()
    {
        await _repo.ImputarCtaCteAsync(50, 11, 0, Ahora, true, 50);

        LastSql.Should().StartWith("UPDATE").And.Contain("[InteresAplicado] = [e].[InteresAplicado] + @").And.Contain("[Total2] = [e].[Total2] + @");
    }

    [Fact]
    public async Task RevertirImputacion_DevuelveSaldoYRestaInteres()
    {
        await _repo.RevertirImputacionCtaCteAsync(50, 11, 950, 50, Ahora);

        LastSql.Should().Contain("[Saldo] = [e].[Saldo] + @").And.Contain("[InteresAplicado] = [e].[InteresAplicado] - @").And.Contain("[Cancelado] = CAST(0 AS bit)");
    }

    [Fact]
    public async Task Actualizaciones_TraducenASql()
    {
        await _repo.SetEstadoDocumentoClienteAsync(50, 1);
        await _repo.SetEstadoDocumentoProveedorAsync(50, 1);
        await _repo.SetEstadoReciboAsync(300, 1);
        await _repo.SetEstadoOrdenPagoAsync(10, 1);
        await _repo.AnularDetalleAsync(300, Ahora);
        await _repo.AnularCajaAsync(7, 300);
        await _repo.AnularChequesAsync(300, 7, 1);
        await _repo.AnularMovimientosBancoAsync(7, 300, 1, Ahora);
        await _repo.AnularRetencionesAsync(7, 300, 1);
        await _repo.BorrarImputacionesAsync(300);
        await _repo.AnularCtaCteAsync(9000, 1, Ahora);

        _interceptor.Commands.Should().HaveCount(12, "AnularCtaCte actualiza cabecera y movimientos");
        _interceptor.Commands.Should().Contain(sql => sql.StartsWith("DELETE"));
    }

    [Fact]
    public async Task Referencias_TraducenASqlYMemorizan()
    {
        var referencias = new ReferenciasRepository(_context);

        await referencias.GetParametroAsync("COMPROBANTE ", "REC");
        await referencias.GetParametroAsync("COMPROBANTE ", "REC");
        var estado = () => referencias.GetIdEstadoAsync("RECIBOSCOBRO", "ANULADO");
        var categoria = () => referencias.GetIdCategoriaAsync("CLIENTECHEQUE", "AUTOMATICO");

        await estado.Should().ThrowAsync<InvalidOperationException>().WithMessage("*RECIBOSCOBRO/ANULADO*");
        await categoria.Should().ThrowAsync<InvalidOperationException>();
        _interceptor.Commands.Should().HaveCount(3, "el segundo parámetro sale de la memoria del request");
    }
}
