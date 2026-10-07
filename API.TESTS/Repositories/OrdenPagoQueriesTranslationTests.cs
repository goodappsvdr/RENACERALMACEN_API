using API.DA.DbContexts;
using API.SERVICE.Repositories.Compras;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace API.TESTS.Repositories;

/// <summary>Cada operación del repositorio de órdenes de pago se traduce a SQL contra el proveedor real (sin base).</summary>
public class OrdenPagoQueriesTranslationTests
{
    private static readonly DateTime Ahora = new(2026, 10, 7, 16, 0, 0);

    private readonly OfflineSqlServerInterceptor _interceptor = new();
    private readonly OrdenPagoRepository _repo;

    public OrdenPagoQueriesTranslationTests()
    {
        var options = new DbContextOptionsBuilder<ElRenacerDbContext>()
            .UseSqlServer("Server=offline;Database=ELRENACER;TrustServerCertificate=True", sql => sql.UseCompatibilityLevel(130))
            .AddInterceptors(_interceptor)
            .Options;
        _repo = new OrdenPagoRepository(new ElRenacerDbContext(options));
    }

    [Fact]
    public async Task Lecturas_TraducenASql()
    {
        await _repo.GetOrdenAsync(800);
        await _repo.GetComprobantesPendientesAsync(5, 48);
        await _repo.GetImputacionesAsync(800);
        await _repo.TieneImputacionesAsync(450, 4);
        await _repo.GetChequeTerceroAsync(50);
        await _repo.GetChequePropioAsync(60);

        _interceptor.Commands.Should().HaveCount(6);
        _interceptor.Commands.ElementAt(1).Should().Contain("[Cancelado] = CAST(0 AS bit)").And.Contain("[Estado] = @");
        _interceptor.Commands.ElementAt(3).Should().Contain("[ID_DocumentoProveedor] = @").And.Contain("[ID_ComprobanteTipo] = @",
            "el SP del ERP comparaba la columna consigo misma");
        _interceptor.Commands.ElementAt(5).Should().Contain("INNER JOIN [BancosCuentas]");
    }

    [Fact]
    public async Task Escrituras_TraducenASql()
    {
        await _repo.BorrarImputacionesAsync(800);
        await _repo.AsignarChequeTerceroAsync(50, 800, 9, 60, 58);
        await _repo.DevolverChequesTercerosAsync(800, 9, 58);
        await _repo.EntregarChequePropioAsync(60, "0001", 800, 9, Ahora, Ahora, 1000, 125, 126);
        await _repo.AnularChequesPropiosAsync(800, 9, 127);
        await _repo.AnularChequesProveedorAsync(800, 9, 79);
        await _repo.AnularDetalleAsync(800, Ahora);

        _interceptor.Commands.Should().HaveCount(7);
        _interceptor.Commands.ElementAt(0).Should().StartWith("DELETE").And.Contain("[EntidadOrdenPagoDocumentosProveedores]");
        _interceptor.Commands.ElementAt(1).Should().StartWith("UPDATE").And.Contain("[Estado] = @", "solo si el cheque sigue en cartera");
        _interceptor.Commands.ElementAt(3).Should().StartWith("UPDATE").And.Contain("[BancosCheques]").And.Contain("[Estado] = @");
        _interceptor.Commands.Last().Should().StartWith("UPDATE").And.Contain("[ProveedoresRecibosDetalle]");
    }
}
