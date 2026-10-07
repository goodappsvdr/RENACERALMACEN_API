using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Models.Compras;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Compras;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Moq;
using R = API.TESTS.Infrastructure.FakeReferencias;

namespace API.TESTS.UseCases;

/// <summary>Nota de crédito de proveedor (NCP): paridad con Agregar_Ws / Editar_Ws de FrmNotaCreditoProveedor.</summary>
public class NotaCreditoCompraUseCasesTests
{
    private const int IdNota = 460;
    private static readonly DateTime Ahora = new(2026, 10, 7, 18, 0, 0);
    private static readonly DateTime Fecha = new(2026, 10, 7);

    private readonly FakeReciboCobroRepository _recibos = new();
    private readonly FakeVentaRepository _stock;
    private readonly FakeCompraRepository _compras = new();
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    public NotaCreditoCompraUseCasesTests()
    {
        _stock = new FakeVentaRepository(_recibos);
        _user.SetupGet(u => u.IdUsuario).Returns(3);
    }

    [Fact]
    public async Task Create_RestaStockSaldoAFavorYLibroIvaComoNotaDeCredito()
    {
        var dto = Dto(Item(10, 2, neto: 200, iva: 42, nroSerie: "SN-1"));

        var r = await CreateSut().ExecuteAsync(dto);

        var doc = _compras.Single<DocumentosProveedor>();
        doc.Should().Match<DocumentosProveedor>(d => d.IdComprobanteTipo == R.Ncp && d.Letra == "A" && d.PuntoVenta == "0012" && d.Numero == "00000010");
        r.IdDocumentoProveedor.Should().Be(doc.IdDocumentoProveedor);
        _recibos.Bloqueos.Should().Contain(5);

        _stock.Operaciones.Should().Contain("RestarStock 10@1 2");
        _compras.Single<ItemsMovimientosDetalles>().Should().Match<ItemsMovimientosDetalles>(m => m.Haber == 2 && m.Debe == 0 && m.Total2 == -2 && m.Concepto == "NCP -A-0012-00000010");
        _compras.Single<EntidadesCtaCteStockMovimientosDetalle>().Should().Match<EntidadesCtaCteStockMovimientosDetalle>(s => s.IdComprobanteTipo == R.Ncp && s.Total == 2);
        _compras.Single<ItemsNroSeries>().Estado.Should().Be(_ref.Estado("ITEMSNROSERIE", "NO DISPONIBLE"));

        _compras.Single<EntidadesCtaCte>().Should().Match<EntidadesCtaCte>(c => c.Total == 242 && c.Saldo == -242 && c.Total2 == 242 && c.IdComprobanteTipo == R.Ncp);
        _compras.Single<EntidadesCtaCteMovimientos>().Should().Match<EntidadesCtaCteMovimientos>(m => m.AfavorEntidad == 242 && m.EnContraEntidad == 0);
        _compras.Single<LibroIvaCompra>().Should().Match<LibroIvaCompra>(l => l.TipoComprobante == "3" && l.Neto21 == 200 && l.Iva21 == 42);
        _compras.Operaciones.Should().NotContain(o => o.StartsWith("Determinar"), "la NC no queda para remitir ni facturar");
    }

    [Fact]
    public async Task Create_LineaRelacionada_400()
    {
        var item = Item(10, 1);
        item.Relacion = new ComprobanteRelacionadoDetalleDto { IdDocumentoCliente = 400, IdComprobanteTipo = R.Rc, IdDocumentoClienteDetalle = 1 };

        var act = () => CreateSut().ExecuteAsync(Dto(item));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no se relacionan*");
        _compras.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_Duplicada_409()
    {
        _compras.Registradas.Add((5, "0012", "00000010"));

        var act = () => CreateSut().ExecuteAsync(Dto(Item(10, 1)));

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*nota de crédito*ya fue registrada*");
    }

    [Fact]
    public async Task Anular_DevuelveElStockEnLugarDeRestarloDeNuevo()
    {
        Nota("GENERADO");
        _stock.MovimientosStock.Add(new EntidadesCtaCteStockMovimientosDetalle { IdComprobante = IdNota, IdComprobanteTipo = R.Ncp, IdComprobanteDetalle = 950, IdItem = 10, Total = 2, IdSucursal = 1 });

        await AnularSut().ExecuteAsync(IdNota);

        _stock.Operaciones.Should().Contain("SumarStock 10@1 2").And.NotContain(o => o.StartsWith("RestarStock"));
        _compras.Single<ItemsMovimientosDetalles>().Should().Match<ItemsMovimientosDetalles>(m => m.Debe == 2 && m.Item == "INSUMO");
        _compras.Operaciones.Should().Contain($"BorrarLibroIva {IdNota}/{R.Ncp}").And.Contain($"AnularDocumento {IdNota}={_ref.Estado("DOCUMENTOSPROVEEDOR", "ANULADO")}");
        _recibos.Operaciones.Should().Contain("AnularCtaCte 999");
        _stock.Operaciones.Should().Contain($"AnularMovimientosStock {IdNota}/{R.Ncp}");
    }

    [Fact]
    public async Task Anular_YaUsadaEnUnaOrdenDePago_409()
    {
        Nota("GENERADO");
        _compras.CtaCte[(R.Ncp, IdNota)].Saldo = 0;

        var act = () => AnularSut().ExecuteAsync(IdNota);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*orden de pago*");
    }

    [Fact]
    public async Task Anular_NoEsNotaDeCredito_400()
    {
        Nota("GENERADO");
        _compras.Documentos[IdNota].IdComprobanteTipo = R.Fc;

        var act = () => AnularSut().ExecuteAsync(IdNota);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no es una nota de crédito*");
    }

    // ---------- helpers ----------

    private CreateNotaCreditoCompraUseCase CreateSut() => new(_compras, _stock, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private AnularNotaCreditoCompraUseCase AnularSut() => new(_compras, _stock, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private void Nota(string estado)
    {
        _compras.Documentos[IdNota] = new DocumentosProveedor
        {
            IdDocumentoProveedor = IdNota, IdComprobanteTipo = R.Ncp, IdProveedor = 5, IdSucursal = 1, Letra = "A", PuntoVenta = "0012", Numero = "00000010",
            Estado = _ref.Estado("DOCUMENTOSPROVEEDOR", estado), TotalGeneral = 242,
        };
        _compras.Detalles.Add(new DocumentosProveedorDetalle { IdDocumentoProveedorDetalle = 950, IdDocumentoProveedor = IdNota, IdItem = 10, Descripcion = "insumo" });
        _compras.CtaCte[(R.Ncp, IdNota)] = new EntidadesCtaCte { IdEntidadCtaCte = 999, Total = 242, Saldo = -242 };
    }

    private static CreateNotaCreditoCompraDto Dto(params FacturaCompraItemDto[] items) => new()
    {
        IdProveedor = 5,
        IdSucursal = 1,
        Letra = "A",
        PuntoVenta = "12",
        Numero = "10",
        FechaEmision = Fecha,
        Neto = items.Sum(i => i.PrecioNeto),
        Iva = items.Sum(i => i.Iva),
        Total = items.Sum(i => i.Total),
        Items = [.. items],
    };

    private static FacturaCompraItemDto Item(int idItem, decimal cantidad, decimal neto = 100, decimal iva = 21, string? nroSerie = null) => new()
    {
        IdItem = idItem,
        Descripcion = "insumo",
        Cantidad = cantidad,
        PrecioUnitario = neto / cantidad,
        PrecioNeto = neto,
        Iva = iva,
        Total = neto + iva,
        IdImpuestoIva = 1,
        NroSerie = nroSerie,
    };
}
