using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Compras;
using API.SERVICE.Models.Compras;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Compras;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Moq;
using R = API.TESTS.Infrastructure.FakeReferencias;

namespace API.TESTS.UseCases;

/// <summary>Remito de compra (RC): paridad con Agregar_Ws / Editar_Ws de FrmRemitosCompra.</summary>
public class RemitoCompraUseCasesTests
{
    private const int IdFactura = 450, IdOrden = 410, IdRemito = 480;
    private static readonly DateTime Ahora = new(2026, 10, 7, 19, 0, 0);
    private static readonly DateTime Fecha = new(2026, 10, 7);

    private readonly FakeReciboCobroRepository _recibos = new();
    private readonly FakeVentaRepository _stock;
    private readonly FakeCompraRepository _compras = new();
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    public RemitoCompraUseCasesTests()
    {
        _stock = new FakeVentaRepository(_recibos);
        _user.SetupGet(u => u.IdUsuario).Returns(3);
        _user.SetupGet(u => u.IdSucursal).Returns(1);
        _compras.Letras.Clear(); // el tipo RC no tiene filas en ComprobantesLetras
        Origen(IdFactura, R.Fc, 851, 10, 5, estado: "PAGADO");
        Origen(IdOrden, R.Oc, 811, 11, 4, estado: "GENERADO");
    }

    [Fact]
    public async Task Create_Directo_LetraRSumaStockYQuedaParaFacturar()
    {
        var dto = Dto(Item(10, 3));
        dto.Cae = "CAI-1";

        var r = await CreateSut().ExecuteAsync(dto);

        var doc = _compras.Single<DocumentosProveedor>();
        doc.Should().Match<DocumentosProveedor>(d => d.IdComprobanteTipo == R.Rc && d.Letra == "R" && d.PuntoVenta == "0007" && d.Numero == "00000088" && d.Cae == "CAI-1");
        r.IdDocumentoProveedor.Should().Be(doc.IdDocumentoProveedor);
        _stock.Operaciones.Should().Contain("SumarStock 10@1 3");
        _compras.Single<EntidadesCtaCteStockMovimientosDetalle>().Should().Match<EntidadesCtaCteStockMovimientosDetalle>(s => s.Saldo == 3 && s.Concepto == "RC -R-0007-00000088");
        _compras.Operaciones.Should().Contain($"Determinar {doc.IdDocumentoProveedor} remitar=False facturar=True pendiente=True");
        _compras.Single<ComprobantesCarga>().IdComprobante.Should().Be(doc.IdDocumentoProveedor);
        _compras.All<EntidadesCtaCte>().Should().BeEmpty("un remito no mueve la cta. cte.");
        _compras.All<LibroIvaCompra>().Should().BeEmpty();
    }

    [Fact]
    public async Task Create_DesdeFactura_NoSumaStockNiPisaElEstadoDePago()
    {
        var dto = Dto(Item(10, 5, relacion: (IdFactura, 851)));
        dto.Comprobantes.Add(new ComprobanteRelacionadoDto { IdDocumentoCliente = IdFactura, IdComprobanteTipo = R.Fc });

        await CreateSut().ExecuteAsync(dto);

        _stock.Operaciones.Should().NotContain(o => o.StartsWith("SumarStock"));
        _stock.Operaciones.Should().Contain($"AjustarSaldoStock {IdFactura}/851/{R.Fc} item=10 -5");
        _compras.Single<EntidadesCtaCteStockMovimientosDetalle>().Should().Match<EntidadesCtaCteStockMovimientosDetalle>(s => s.Saldo == 0 && s.Saldo2 == -5);
        _compras.Operaciones.Should().Contain($"Pendiente {IdFactura}=True").And.NotContain(o => o.StartsWith($"EstadoPendiente {IdFactura}"),
            "el ERP le ponía ENTREGADO a una factura PAGADA");
        _compras.Operaciones.Should().NotContain(o => o.StartsWith("Determinar"));
    }

    [Fact]
    public async Task Create_DesdeOrden_SumaStockYOrdenEntregadaParcial()
    {
        var dto = Dto(Item(11, 2, relacion: (IdOrden, 811)));
        dto.Comprobantes.Add(new ComprobanteRelacionadoDto { IdDocumentoCliente = IdOrden, IdComprobanteTipo = R.Oc });

        await CreateSut().ExecuteAsync(dto);

        var rc = _compras.Single<DocumentosProveedor>().IdDocumentoProveedor;
        _stock.Operaciones.Should().Contain("SumarStock 11@1 2");
        _compras.Operaciones.Should().Contain($"EstadoPendiente {IdOrden}={_ref.Estado("DOCUMENTOSPROVEEDOR", "ENTREGADO PARCIAL")} pendiente=True")
            .And.Contain($"Determinar {rc} remitar=False facturar=True pendiente=True");
        _compras.Single<DocumentosProveedorRemitos>().Should().Match<DocumentosProveedorRemitos>(x => x.IdDocumentoProveedor == IdOrden && x.IdRemito == rc);
    }

    [Fact]
    public async Task Create_MasQueElPendiente_400()
    {
        var dto = Dto(Item(11, 5, relacion: (IdOrden, 811)));
        dto.Comprobantes.Add(new ComprobanteRelacionadoDto { IdDocumentoCliente = IdOrden, IdComprobanteTipo = R.Oc });

        var act = () => CreateSut().ExecuteAsync(dto);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*el pendiente es 4*");
    }

    [Fact]
    public async Task Create_Duplicado_409()
    {
        _compras.Registradas.Add((5, "0007", "00000088"));

        var act = () => CreateSut().ExecuteAsync(Dto(Item(10, 1)));

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*remito*ya fue registrad*");
    }

    [Fact]
    public async Task Anular_RestaStockYDevuelveSaldos()
    {
        Remito("GENERADO");
        _stock.MovimientosStock.Add(Mov(950, 10, 3, 0, 0));
        _stock.MovimientosStock.Add(Mov(951, 10, 5, IdFactura, R.Fc, 851));
        _stock.MovimientosStock.Add(Mov(952, 11, 2, IdOrden, R.Oc, 811));
        _compras.Relaciones.Add(new DocumentosProveedorRemitos { IdDocumentoProveedorRemito = 1, IdDocumentoProveedor = IdFactura, IdRemito = IdRemito, IdComprobanteTipo = R.Fc });
        _compras.Relaciones.Add(new DocumentosProveedorRemitos { IdDocumentoProveedorRemito = 2, IdDocumentoProveedor = IdOrden, IdRemito = IdRemito, IdComprobanteTipo = R.Oc });

        await AnularSut().ExecuteAsync(IdRemito);

        _stock.Operaciones.Where(o => o.StartsWith("RestarStock")).Should().Equal("RestarStock 10@1 3", "RestarStock 11@1 2");
        _stock.Operaciones.Should().Contain($"AjustarSaldoStock {IdFactura}/851/{R.Fc} item=10 5").And.Contain($"AjustarSaldoStock {IdOrden}/811/{R.Oc} item=11 2");
        _compras.Operaciones.Should().Contain($"Pendiente {IdFactura}=True")
            .And.Contain($"EstadoPendiente {IdOrden}={_ref.Estado("DOCUMENTOSPROVEEDOR", "GENERADO")} pendiente=True")
            .And.Contain("BorrarRelacion 1").And.Contain("BorrarRelacion 2")
            .And.Contain($"AnularDocumento {IdRemito}={_ref.Estado("DOCUMENTOSPROVEEDOR", "ANULADO")}");
    }

    [Fact]
    public async Task Anular_Facturado_409()
    {
        Remito("GENERADO");
        _compras.ConRelacionesComoOrigen.Add(IdRemito);

        var act = () => AnularSut().ExecuteAsync(IdRemito);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*facturado*");
    }

    [Fact]
    public async Task AnularFactura_ConMercaderiaRemitida_409()
    {
        _compras.Documentos[IdFactura].Estado = _ref.Estado("DOCUMENTOSPROVEEDOR", "GENERADO");
        _compras.ConRelacionesComoOrigen.Add(IdFactura);

        var act = () => new AnularFacturaCompraUseCase(_compras, _stock, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object)
            .ExecuteAsync(IdFactura);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*remitida*");
    }

    [Fact]
    public async Task PendientesRemitir_FacturasYOrdenes()
    {
        var r = await new GetComprobantesCompraParaRemitirUseCase(_compras, _ref, _user.Object).ExecuteAsync(5);

        r.Select(x => x.IdDocumentoProveedor).Should().BeEquivalentTo([IdFactura, IdOrden]);
    }

    // ---------- helpers ----------

    private CreateRemitoCompraUseCase CreateSut() => new(_compras, _stock, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private AnularRemitoCompraUseCase AnularSut() => new(_compras, _stock, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private void Origen(int id, int tipo, long idDetalle, int idItem, decimal saldo, string estado)
    {
        _compras.Documentos[id] = new DocumentosProveedor
        {
            IdDocumentoProveedor = id, IdComprobanteTipo = tipo, IdProveedor = 5, IdSucursal = 1, Estado = _ref.Estado("DOCUMENTOSPROVEEDOR", estado),
            Letra = "A", PuntoVenta = "0001", Numero = id.ToString("00000000"),
        };
        _compras.LineasPendientes[id] = [new LineaPendienteCompraRow(new DocumentosProveedorDetalle { IdDocumentoProveedorDetalle = idDetalle, IdDocumentoProveedor = id, IdItem = idItem, Descripcion = "INSUMO" }, tipo, saldo)];
    }

    private void Remito(string estado)
    {
        _compras.Documentos[IdRemito] = new DocumentosProveedor
        {
            IdDocumentoProveedor = IdRemito, IdComprobanteTipo = R.Rc, IdProveedor = 5, IdSucursal = 1, Letra = "R", PuntoVenta = "0007", Numero = "00000088",
            Estado = _ref.Estado("DOCUMENTOSPROVEEDOR", estado),
        };
        _compras.Detalles.Add(new DocumentosProveedorDetalle { IdDocumentoProveedorDetalle = 950, IdDocumentoProveedor = IdRemito, IdItem = 10, Descripcion = "insumo" });
    }

    private static EntidadesCtaCteStockMovimientosDetalle Mov(int idDetalle, int idItem, decimal cantidad, int rel, int relTipo, int relDetalle = 0) => new()
    {
        IdComprobante = IdRemito, IdComprobanteTipo = R.Rc, IdComprobanteDetalle = idDetalle, IdItem = idItem, Total = cantidad, IdSucursal = 1,
        Concepto = "RC -R-0007-00000088", IdComprobanteRelacion = rel, IdComprobanteRelacionTipo = relTipo, IdComprobanteRelacionDetalle = relDetalle,
    };

    private static CreateRemitoCompraDto Dto(params FacturaCompraItemDto[] items) => new()
    {
        IdProveedor = 5,
        IdSucursal = 1,
        PuntoVenta = "7",
        Numero = "88",
        FechaEmision = Fecha,
        Total = items.Sum(i => i.Total),
        Items = [.. items],
    };

    private static FacturaCompraItemDto Item(int idItem, decimal cantidad, (int Doc, int Detalle)? relacion = null) => new()
    {
        IdItem = idItem,
        Descripcion = "insumo",
        Cantidad = cantidad,
        PrecioUnitario = 10,
        PrecioNeto = 10 * cantidad,
        Total = 10 * cantidad,
        IdImpuestoIva = 1,
        Relacion = relacion is { } r ? new ComprobanteRelacionadoDetalleDto { IdDocumentoCliente = r.Doc, IdComprobanteTipo = R.Rc, IdDocumentoClienteDetalle = r.Detalle } : null,
    };
}
