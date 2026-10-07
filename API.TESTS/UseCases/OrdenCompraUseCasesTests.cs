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

/// <summary>Orden de compra (OC): paridad con Agregar_Ws / Modificar_Ws / Anular_Ws de FrmOrdenCompraABM.</summary>
public class OrdenCompraUseCasesTests
{
    private const int IdOrden = 410;
    private static readonly DateTime Ahora = new(2026, 10, 7, 20, 0, 0);
    private static readonly DateTime Fecha = new(2026, 10, 7);

    private readonly FakeReciboCobroRepository _recibos = new();
    private readonly FakeVentaRepository _stock;
    private readonly FakeCompraRepository _compras = new();
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    public OrdenCompraUseCasesTests()
    {
        _stock = new FakeVentaRepository(_recibos);
        _user.SetupGet(u => u.IdUsuario).Returns(3);
    }

    [Fact]
    public async Task Create_NumeraLetraXYDejaSaldoPendienteSinMoverStock()
    {
        var r = await CreateSut().ExecuteAsync(Dto(Item(10, 4), Item(11, 2)));

        var doc = _compras.Single<DocumentosProveedor>();
        doc.Should().Match<DocumentosProveedor>(d =>
            d.IdComprobanteTipo == R.Oc && d.Letra == "X" && d.PuntoVenta == "0003" && d.Numero == "00000042" && d.IdPlanillaCaja == 77
            && d.Estado == _ref.Estado("DOCUMENTOSPROVEEDOR", "GENERADO"));
        r.IdDocumentoProveedor.Should().Be(doc.IdDocumentoProveedor);
        _recibos.Operaciones.Should().Contain("Numerar 0003");
        _compras.Operaciones.Should().Contain($"Determinar {doc.IdDocumentoProveedor} remitar=True facturar=True pendiente=True");
        _compras.All<EntidadesCtaCteStockMovimientosDetalle>().Should().HaveCount(2).And.AllSatisfy(s =>
            s.Should().Match<EntidadesCtaCteStockMovimientosDetalle>(x => x.Saldo == x.Total && x.Saldo2 == 0 && x.Concepto == "OC -X-0003-00000042"));
        _stock.Operaciones.Should().NotContain(o => o.Contains("Stock") && !o.StartsWith("Ajustar"));
        _compras.All<EntidadesCtaCte>().Should().BeEmpty();
        _compras.Single<ComprobantesCarga>().IdComprobante.Should().Be(doc.IdDocumentoProveedor);
    }

    [Fact]
    public async Task Create_LineaConRelacion_400()
    {
        var item = Item(10, 1);
        item.Relacion = new ComprobanteRelacionadoDetalleDto { IdDocumentoCliente = 1, IdComprobanteTipo = R.Rc, IdDocumentoClienteDetalle = 1 };

        var act = () => CreateSut().ExecuteAsync(Dto(item));

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task Update_ReemplazaCabeceraYLineas()
    {
        Existente("GENERADO");

        await UpdateSut().ExecuteAsync(IdOrden, Dto(Item(12, 7)));

        _compras.Operaciones.Should().ContainInOrder($"ModificarCabecera {IdOrden} proveedor=5 total=70", $"BorrarDetalles {IdOrden}");
        _stock.Operaciones.Should().Contain($"BorrarMovimientosStock {IdOrden}/{R.Oc}");
        _compras.Single<EntidadesCtaCteStockMovimientosDetalle>().Should().Match<EntidadesCtaCteStockMovimientosDetalle>(s =>
            s.IdComprobante == IdOrden && s.IdItem == 12 && s.Saldo == 7 && s.Concepto == "OC -X-0001-00000005");
    }

    [Fact]
    public async Task Update_ConAlgoRecibido_409()
    {
        Existente("GENERADO");
        _compras.ConRelacionesComoOrigen.Add(IdOrden);

        var act = () => UpdateSut().ExecuteAsync(IdOrden, Dto(Item(12, 1)));

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*recibida o facturada*");
    }

    [Fact]
    public async Task Anular_TrabajaSobreLaOrdenNoSobreUnaVenta()
    {
        Existente("GENERADO");

        await AnularSut().ExecuteAsync(IdOrden);

        _compras.Operaciones.Should().Contain($"AnularDetalles {IdOrden}").And.Contain($"AnularDocumento {IdOrden}={_ref.Estado("DOCUMENTOSPROVEEDOR", "ANULADO")}");
        _stock.Operaciones.Should().Contain($"AnularMovimientosStock {IdOrden}/{R.Oc}").And.NotContain(o => o.StartsWith("AnularDocumento"),
            "el ERP anulaba el comprobante de venta con el mismo ID");
    }

    [Fact]
    public async Task Anular_NoEsOrdenDeCompra_400()
    {
        Existente("GENERADO");
        _compras.Documentos[IdOrden].IdComprobanteTipo = R.Fc;

        var act = () => AnularSut().ExecuteAsync(IdOrden);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no es una orden de compra*");
    }

    [Fact]
    public async Task Anular_Entregada_409()
    {
        Existente("ENTREGADO PARCIAL");

        var act = () => AnularSut().ExecuteAsync(IdOrden);

        await act.Should().ThrowAsync<ConflictException>();
    }

    // ---------- helpers ----------

    private CreateOrdenCompraUseCase CreateSut() => new(_compras, _stock, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private UpdateOrdenCompraUseCase UpdateSut() => new(_compras, _stock, _recibos, _ref, new InlineUnitOfWork(), _user.Object);

    private AnularOrdenCompraUseCase AnularSut() => new(_compras, _stock, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private void Existente(string estado) =>
        _compras.Documentos[IdOrden] = new DocumentosProveedor
        {
            IdDocumentoProveedor = IdOrden, IdComprobanteTipo = R.Oc, IdProveedor = 5, IdSucursal = 1, Letra = "X", PuntoVenta = "0001", Numero = "00000005",
            FechaEmision = Fecha, Estado = _ref.Estado("DOCUMENTOSPROVEEDOR", estado),
        };

    private static CreateOrdenCompraDto Dto(params FacturaCompraItemDto[] items) => new()
    {
        IdProveedor = 5,
        IdSucursal = 1,
        FechaEmision = Fecha,
        Total = items.Sum(i => i.Total),
        Items = [.. items],
    };

    private static FacturaCompraItemDto Item(int idItem, decimal cantidad) => new()
    {
        IdItem = idItem,
        Descripcion = "insumo",
        Cantidad = cantidad,
        PrecioUnitario = 10,
        PrecioNeto = 10 * cantidad,
        Total = 10 * cantidad,
        IdImpuestoIva = 1,
    };
}
