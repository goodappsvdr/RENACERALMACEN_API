using API.DA.DbContexts;
using API.SERVICE.Repositories.Compras;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace API.TESTS.Repositories;

/// <summary>Cada operación del repositorio de compras se traduce a SQL contra el proveedor real (sin base).</summary>
public class CompraQueriesTranslationTests
{
    private static readonly DateTime Ahora = new(2026, 10, 7, 11, 0, 0);

    private readonly OfflineSqlServerInterceptor _interceptor = new();
    private readonly CompraRepository _repo;

    public CompraQueriesTranslationTests()
    {
        var options = new DbContextOptionsBuilder<ElRenacerDbContext>()
            .UseSqlServer("Server=offline;Database=ELRENACER;TrustServerCertificate=True", sql => sql.UseCompatibilityLevel(130))
            .AddInterceptors(_interceptor)
            .Options;
        _repo = new CompraRepository(new ElRenacerDbContext(options));
    }

    private string LastSql => _interceptor.Commands.Last();

    [Fact]
    public async Task Lecturas_TraducenASql()
    {
        await _repo.GetDocumentoAsync(450);
        await _repo.GetDetallesAsync(450);
        await _repo.ExisteDuplicadoAsync(5, 4, "0012", "00000345", 70);
        await _repo.GetLetrasAsync(4, 2, 2);
        await _repo.GetPlanillaAbiertaAsync(3, 45);
        await _repo.GetComprobantesConPendienteAsync(5, [6, 22], [70, 96], 1);
        await _repo.GetLineasPendientesAsync(400, [4, 12, 6, 22]);
        await _repo.GetRelacionesComoDestinoAsync(450);
        await _repo.GetCtaCteAsync(4, 450);
        await _repo.TieneRelacionesComoOrigenAsync(450);

        _interceptor.Commands.Should().HaveCount(10);
        LastSql.Should().Contain("FROM [DocumentosProveedorRemitos]").And.Contain("[ID_DocumentoProveedor] = @");
        _interceptor.Commands.ElementAt(2).Should().Contain("[PuntoVenta] = @").And.Contain("[Numero] = @").And.Contain("<> @");
        _interceptor.Commands.ElementAt(3).Should().Contain("[ID_CategoriaIVAProveedor] = @").And.Contain("[ID_CategoriaIVACliente] = @");
        _interceptor.Commands.ElementAt(5).Should().Contain("EXISTS").And.Contain("[EntidadesCtaCteStockMovimientosDetalle]").And.Contain("[ID_Sucursal] = @");
        _interceptor.Commands.ElementAt(6).Should().Contain("INNER JOIN [DocumentosProveedorDetalle]").And.Contain("<> 0.0");
        _interceptor.Commands.ElementAt(7).Should().Contain("[ID_Remito] = @");
    }

    [Fact]
    public async Task Escrituras_TraducenASql()
    {
        await _repo.BorrarRelacionAsync(1);
        await _repo.SetEstadoPendienteAsync(400, 195, true);
        await _repo.DeterminarRemitarFacturarAsync(450, true, false, true);
        await _repo.AnularDocumentoAsync(450, 70, Ahora);
        await _repo.BorrarLibroIvaAsync(450, 4);
        await _repo.BorrarOtrosTributosAsync(450, 4);
        await _repo.SetPendienteAsync(450, true);
        await _repo.ModificarCabeceraAsync(410, new API.SERVICE.Interfaces.Compras.CabeceraProveedorRow(5, "PROV", 2, "30", 1, 2, "CALLE", 100, 21, 0, 121));
        await _repo.BorrarDetallesAsync(410);
        await _repo.AnularDetallesAsync(410, 99);

        _interceptor.Commands.Should().HaveCount(11);
        _interceptor.Commands.ElementAt(8).Should().StartWith("UPDATE").And.Contain("[TotalGeneral] = @").And.NotContain("[Numero]");
        _interceptor.Commands.ElementAt(9).Should().StartWith("DELETE").And.Contain("[DocumentosProveedorDetalle]");
        _interceptor.Commands.ElementAt(10).Should().StartWith("UPDATE").And.Contain("[DocumentosProveedorDetalle]").And.Contain("[EstadoLibroIVA] = @");
        _interceptor.Commands.ElementAt(0).Should().StartWith("DELETE").And.Contain("[ID_DocumentoProveedorRemito] = @", "el SP del ERP borraba por otra columna");
        _interceptor.Commands.ElementAt(5).Should().StartWith("DELETE").And.Contain("[TxtComprasAlicuotas]").And.Contain("[ID_ComprobanteTipo] = @",
            "el SP del ERP comparaba la columna consigo misma");
        _interceptor.Commands.ElementAt(6).Should().StartWith("DELETE").And.Contain("[DocumentosProveedorOtrosTributos]");
        _interceptor.Commands.ElementAt(7).Should().StartWith("UPDATE").And.Contain("[Pendiente] = @").And.NotContain("[Estado]");
    }
}
