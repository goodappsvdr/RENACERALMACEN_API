using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Models.Compras;
using API.SERVICE.UseCases.Compras;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Moq;
using R = API.TESTS.Infrastructure.FakeReferencias;

namespace API.TESTS.UseCases;

/// <summary>Compra con pago en el momento (COM + OP): paridad con Agregar_Ws / generarOrdenDePago / Editar_Ws de FrmCompras.</summary>
public class CompraContadoUseCasesTests
{
    private const int IdCompra = 470;
    private static readonly DateTime Ahora = new(2026, 10, 7, 21, 0, 0);
    private static readonly DateTime Fecha = new(2026, 10, 7);

    private readonly FakeReciboCobroRepository _recibos = new();
    private readonly FakeVentaRepository _stock;
    private readonly FakeCompraRepository _compras;
    private readonly FakeOrdenPagoRepository _ordenes = new();
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    public CompraContadoUseCasesTests()
    {
        _stock = new FakeVentaRepository(_recibos);
        _compras = new FakeCompraRepository(_recibos);
        _user.SetupGet(u => u.IdUsuario).Returns(3);
    }

    [Fact]
    public async Task Create_ConEfectivo_SumaStockYGeneraLaOrdenQueLaCancela()
    {
        var r = await CreateSut().ExecuteAsync(Dto([Item(10, 2, precioUnitario: 121, iva: 42)], Efectivo(242)));

        var com = _compras.Single<DocumentosProveedor>();
        com.Should().Match<DocumentosProveedor>(d => d.IdComprobanteTipo == R.Com && d.Letra == "X" && d.PuntoVenta == "0003" && d.Numero == "00000042");
        _stock.Operaciones.Should().Contain("SumarStock 10@1 2");
        _compras.All<EntidadesCtaCteStockMovimientosDetalle>().Should().BeEmpty("la COM no deja saldo para remitir ni facturar");
        _compras.Single<EntidadesCtaCte>().Should().Match<EntidadesCtaCte>(c => c.IdComprobanteTipo == R.Com && c.Total == 242 && c.Saldo == 242 && c.Total2 == -242);
        _compras.Single<EntidadesCtaCteMovimientos>().AfavorEntidad.Should().Be(242, "el ERP graba el movimiento de la COM a favor");
        _compras.All<LibroIvaCompra>().Should().BeEmpty();

        // Orden de pago generada en la misma transacción
        r.OrdenPago.Should().NotBeNull();
        var op = _ordenes.Single<ProveedoresRecibos>();
        op.Total.Should().Be(242);
        _recibos.Operaciones.Should().Contain($"ImputarCtaCte {com.IdDocumentoProveedor}/{R.Com} saldo=0.00 cancelado=True interes=0.00 fecha=2026-10-07");
        _recibos.Operaciones.Should().Contain($"EstadoDocumentoProveedor {com.IdDocumentoProveedor}={_ref.Estado("DOCUMENTOSPROVEEDOR", "PAGADO")}");
        _ordenes.Single<CajasPlanillasDetalle>().Haber.Should().Be(242);
        _compras.Operaciones.Should().NotContain(o => o.StartsWith("ActualizarCosto"), "CAMBIAPRECIO = 0");
    }

    [Fact]
    public async Task Create_SinFormasDePago_QuedaEnCtaCte()
    {
        var r = await CreateSut().ExecuteAsync(Dto([Item(10, 1)]));

        r.OrdenPago.Should().BeNull();
        _ordenes.Added.Should().BeEmpty();
        _compras.Single<EntidadesCtaCte>().Saldo.Should().Be(121);
    }

    [Fact]
    public async Task Create_PagoParcial_QuedaPagadoParcial()
    {
        await CreateSut().ExecuteAsync(Dto([Item(10, 1)], Efectivo(100)));

        var com = _compras.Single<DocumentosProveedor>();
        _recibos.Operaciones.Should().Contain($"EstadoDocumentoProveedor {com.IdDocumentoProveedor}={_ref.Estado("DOCUMENTOSPROVEEDOR", "PAGADO PARCIAL")}");
    }

    [Fact]
    public async Task Create_ConCambiaPrecio_ActualizaElCostoSinIva()
    {
        _ref.Parametros[("CAMBIAPRECIO", "CAMBIAPRECIO")] = "1";

        await CreateSut().ExecuteAsync(Dto([Item(10, 1, precioUnitario: 121, iva: 21, alicuota: 21)]));

        _compras.Operaciones.Should().Contain("ActualizarCosto 10=100.00");
    }

    [Fact]
    public async Task Create_ChequeNoDisponible_NoGrabaNada_409()
    {
        _ordenes.ChequesTerceros[50] = new EntidadesCheques { IdEntidadCheque = 50, Importe = 121, Estado = _ref.Estado("CLIENTECHEQUE", "ENTREGADO") };

        var act = () => CreateSut().ExecuteAsync(Dto([Item(10, 1)], new ElementoPagoDto { IdElementoCobro = R.Cheque, Importe = 121, IdCheque = 50 }));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Anular_RestaStockYAnula()
    {
        Compra("GENERADO");

        await AnularSut().ExecuteAsync(IdCompra);

        _stock.Operaciones.Should().Contain("RestarStock 10@1 2");
        _compras.Single<ItemsMovimientosDetalles>().Should().Match<ItemsMovimientosDetalles>(m => m.Haber == 2 && m.Concepto == "COM -X-0003-00000042");
        _compras.Operaciones.Should().Contain($"AnularDetalles {IdCompra}").And.Contain($"AnularDocumento {IdCompra}={_ref.Estado("DOCUMENTOSPROVEEDOR", "ANULADO")}");
        _recibos.Operaciones.Should().Contain("AnularCtaCte 999");
    }

    [Fact]
    public async Task Anular_Pagada_409()
    {
        Compra("PAGADO");

        var act = () => AnularSut().ExecuteAsync(IdCompra);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*orden de pago*");
        _stock.Operaciones.Should().BeEmpty();
    }

    // ---------- helpers ----------

    private CreateCompraContadoUseCase CreateSut()
    {
        var clock = new FixedServerClock(Ahora);
        return new(_compras, _stock, _recibos, _ref, new OrdenPagoWriter(_ordenes, _recibos, _ref, clock), new InlineUnitOfWork(), clock, _user.Object);
    }

    private AnularCompraContadoUseCase AnularSut() => new(_compras, _stock, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private void Compra(string estado)
    {
        _compras.Documentos[IdCompra] = new DocumentosProveedor
        {
            IdDocumentoProveedor = IdCompra, IdComprobanteTipo = R.Com, IdProveedor = 5, IdSucursal = 1, Letra = "X", PuntoVenta = "0003", Numero = "00000042",
            Estado = _ref.Estado("DOCUMENTOSPROVEEDOR", estado), TotalGeneral = 242,
        };
        _compras.Detalles.Add(new DocumentosProveedorDetalle { IdDocumentoProveedorDetalle = 950, IdDocumentoProveedor = IdCompra, IdItem = 10, Cantidad = 2, Descripcion = "insumo" });
        _compras.CtaCte[(R.Com, IdCompra)] = new EntidadesCtaCte { IdEntidadCtaCte = 999, Total = 242, Saldo = 242 };
    }

    private static CreateCompraContadoDto Dto(FacturaCompraItemDto[] items, params ElementoPagoDto[] elementos) => new()
    {
        IdProveedor = 5,
        IdSucursal = 1,
        FechaEmision = Fecha,
        Neto = items.Sum(i => i.PrecioNeto),
        Iva = items.Sum(i => i.Iva),
        Total = items.Sum(i => i.Total),
        Items = [.. items],
        Elementos = [.. elementos],
    };

    private static ElementoPagoDto Efectivo(decimal importe) => new() { IdElementoCobro = R.Efectivo, Importe = importe, Descripcion = "EFECTIVO" };

    private static FacturaCompraItemDto Item(int idItem, decimal cantidad, decimal precioUnitario = 121, decimal iva = 21, decimal alicuota = 21) => new()
    {
        IdItem = idItem,
        Descripcion = "insumo",
        Cantidad = cantidad,
        PrecioUnitario = precioUnitario,
        PrecioNeto = precioUnitario * cantidad - iva,
        IvaAlicuota = alicuota,
        Iva = iva,
        Total = precioUnitario * cantidad,
        IdImpuestoIva = 1,
    };
}
