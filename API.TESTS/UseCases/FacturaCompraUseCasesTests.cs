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

/// <summary>Factura de compra (FC): paridad con Agregar_Ws / Editar_Ws de FrmFacturasCompras.</summary>
public class FacturaCompraUseCasesTests
{
    private const int IdRemito = 400, IdOrden = 410, IdFactura = 450;
    private static readonly DateTime Ahora = new(2026, 10, 7, 11, 0, 0);
    private static readonly DateTime Fecha = new(2026, 10, 7);

    private readonly FakeReciboCobroRepository _recibos = new();
    private readonly FakeVentaRepository _stock;
    private readonly FakeCompraRepository _compras = new();
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    public FacturaCompraUseCasesTests()
    {
        _stock = new FakeVentaRepository(_recibos);
        _user.SetupGet(u => u.IdUsuario).Returns(3);
        _user.SetupGet(u => u.IdSucursal).Returns(1);
        Origen(IdRemito, R.Rc, 801, 10, 5);
        Origen(IdOrden, R.Oc, 811, 11, 4);
    }

    // ---------- Alta ----------

    [Fact]
    public async Task Create_Directa_SumaStockDeudaEnCtaCteYLibroIva()
    {
        var dto = Dto(Item(10, 3, neto: 300, iva: 63, idImpuesto: 1, otros: 2), Item(11, 1, neto: 100, iva: 10.5m, idImpuesto: 2));
        dto.OtrosTributos.Add(new OtroTributoDto { IdTributo = 7, Detalle = "Perc IIBB", BaseImponible = 400, Alicuota = 3, Importe = 12 });
        dto.OtrosTributos.Add(new OtroTributoDto { IdTributo = 1, Importe = 5 });

        var r = await CreateSut().ExecuteAsync(dto);

        var doc = _compras.Single<DocumentosProveedor>();
        doc.Should().Match<DocumentosProveedor>(d =>
            d.IdComprobanteTipo == R.Fc && d.Letra == "A" && d.PuntoVenta == "0012" && d.Numero == "00000345" && d.IdProveedor == 5
            && d.RazonSocial == "ALMACEN DON PEPE" && d.IdPlanillaCaja == 77 && d.Estado == _ref.Estado("DOCUMENTOSPROVEEDOR", "GENERADO") && d.Cae == "0");
        r.IdDocumentoProveedor.Should().Be(doc.IdDocumentoProveedor);
        _recibos.Bloqueos.Should().Contain(5);

        // Stock: suma y queda pendiente de remitir
        _stock.Operaciones.Should().Contain("SumarStock 10@1 3").And.Contain("SumarStock 11@1 1");
        _compras.All<ItemsMovimientosDetalles>().Should().HaveCount(2).And.AllSatisfy(m => m.Debe.Should().Be(m.Total));
        _compras.All<EntidadesCtaCteStockMovimientosDetalle>().Should().AllSatisfy(s => s.Saldo.Should().Be(s.Total));
        _compras.All<DocumentosProveedorDetalle>().Select(d => d.Otros).Should().Equal(2m, 0m);
        _compras.Operaciones.Should().Contain($"Determinar {doc.IdDocumentoProveedor} remitar=True facturar=False pendiente=True");

        // Cta. cte.: deuda con el proveedor
        var cc = _compras.Single<EntidadesCtaCte>();
        cc.Should().Match<EntidadesCtaCte>(c => c.IdEntidad == 5 && c.Total == 475.5m && c.Saldo == 475.5m && c.Total2 == -475.5m && c.Concepto == "FC -A-0012-00000345");
        _compras.Single<EntidadesCtaCteMovimientos>().EnContraEntidad.Should().Be(475.5m);

        // Libro IVA compras (sucursal RI)
        _compras.Single<LibroIvaCompra>().Should().Match<LibroIvaCompra>(l =>
            l.TipoComprobante == "1" && l.Neto21 == 300 && l.Iva21 == 63 && l.Neto10 == 100 && l.Iva10 == 10.5m
            && l.Percepciones == 12 && l.ImpNacional == 5 && l.ImpInterno == 2 && l.Mes == 10 && l.Anio == 2026);
        _compras.All<TxtComprasAlicuotas>().Select(t => t.Alicuota).Should().BeEquivalentTo(["0005", "0004"]);
        _compras.All<DocumentosProveedorOtrosTributos>().Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_DesdeRemito_SoloConsumeSaldo_YDesdeOrden_SumaStock()
    {
        var dto = Dto(Item(10, 2, relacion: (IdRemito, 801)), Item(11, 4, relacion: (IdOrden, 811)));
        dto.Comprobantes.Add(new ComprobanteRelacionadoDto { IdDocumentoCliente = IdRemito, IdComprobanteTipo = R.Rc });
        dto.Comprobantes.Add(new ComprobanteRelacionadoDto { IdDocumentoCliente = IdOrden, IdComprobanteTipo = R.Rc });

        await CreateSut().ExecuteAsync(dto);

        var fc = _compras.Single<DocumentosProveedor>().IdDocumentoProveedor;
        _stock.Operaciones.Should().Contain($"AjustarSaldoStock {IdRemito}/801/{R.Rc} item=10 -2");
        _stock.Operaciones.Should().Contain($"AjustarSaldoStock {IdOrden}/811/{R.Oc} item=11 -4", "el tipo sale del comprobante, no del request");
        _stock.Operaciones.Where(o => o.StartsWith("SumarStock")).Should().Equal("SumarStock 11@1 4");
        _compras.All<DocumentosProveedorRemitos>().Should().HaveCount(2).And.AllSatisfy(x => x.IdRemito.Should().Be(fc));
        _compras.Operaciones.Should().Contain($"Determinar {fc} remitar=True facturar=False pendiente=True", "lo que viene de una orden queda para remitir");
    }

    [Fact]
    public async Task Create_Duplicada_409()
    {
        _compras.Registradas.Add((5, "0012", "00000345"));

        var act = () => CreateSut().ExecuteAsync(Dto(Item(10, 1)));

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*ya fue registrada*");
        _compras.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_LetraQueNoCorresponde_400()
    {
        var dto = Dto(Item(10, 1));
        dto.Letra = "C";

        var act = () => CreateSut().ExecuteAsync(dto);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*letra C*");
    }

    [Fact]
    public async Task Create_SinPlanillaAbierta_400()
    {
        _compras.Planilla = null;

        var act = () => CreateSut().ExecuteAsync(Dto(Item(10, 1)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*planilla*");
    }

    [Fact]
    public async Task Create_MasQueElPendienteDelRemito_400()
    {
        var dto = Dto(Item(10, 6, relacion: (IdRemito, 801)));
        dto.Comprobantes.Add(new ComprobanteRelacionadoDto { IdDocumentoCliente = IdRemito, IdComprobanteTipo = R.Rc });

        var act = () => CreateSut().ExecuteAsync(dto);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*el pendiente es 5*");
    }

    [Fact]
    public async Task Create_RemitoDeOtroProveedor_400()
    {
        _compras.Documentos[IdRemito].IdProveedor = 99;
        var dto = Dto(Item(10, 1, relacion: (IdRemito, 801)));
        dto.Comprobantes.Add(new ComprobanteRelacionadoDto { IdDocumentoCliente = IdRemito, IdComprobanteTipo = R.Rc });

        var act = () => CreateSut().ExecuteAsync(dto);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no es del proveedor*");
    }

    [Fact]
    public async Task Create_SucursalNoInscripta_SinLibroIva()
    {
        _stock.Sucursales[1].IdCategoriaIva = 3;

        await CreateSut().ExecuteAsync(Dto(Item(10, 1)));

        _compras.All<LibroIvaCompra>().Should().BeEmpty();
        _compras.All<TxtComprasAlicuotas>().Should().BeEmpty();
    }

    // ---------- Anulación ----------

    [Fact]
    public async Task Anular_RestaElStockDevuelveSaldosYAnulaTodo()
    {
        Factura(estado: "GENERADO");
        _stock.MovimientosStock.Add(Mov(950, 10, 3, 0, 0));                 // directa: resta
        _stock.MovimientosStock.Add(Mov(951, 10, 2, IdRemito, R.Rc, 801));  // de remito: solo saldo
        _stock.MovimientosStock.Add(Mov(952, 11, 4, IdOrden, R.Oc, 811));   // de orden: saldo + resta
        _compras.Relaciones.Add(new DocumentosProveedorRemitos { IdDocumentoProveedorRemito = 1, IdDocumentoProveedor = IdRemito, IdRemito = IdFactura, IdComprobanteTipo = R.Rc });
        _stock.SaldosStock[IdRemito] = 1;

        await AnularSut().ExecuteAsync(IdFactura);

        _stock.Operaciones.Where(o => o.StartsWith("RestarStock")).Should().Equal("RestarStock 10@1 3", "RestarStock 11@1 4");
        _stock.Operaciones.Should().Contain($"AjustarSaldoStock {IdRemito}/801/{R.Rc} item=10 2").And.Contain($"AjustarSaldoStock {IdOrden}/811/{R.Oc} item=11 4");
        _compras.All<ItemsMovimientosDetalles>().Should().HaveCount(2).And.AllSatisfy(m => m.Haber.Should().Be(m.Total));
        _compras.Operaciones.Should().Contain($"EstadoPendiente {IdRemito}={_ref.Estado("DOCUMENTOSPROVEEDOR", "FACTURADO PARCIAL")} pendiente=True").And.Contain("BorrarRelacion 1");
        _compras.Operaciones.Should().Contain($"BorrarLibroIva {IdFactura}/{R.Fc}").And.Contain($"BorrarOtrosTributos {IdFactura}/{R.Fc}")
            .And.Contain($"AnularDocumento {IdFactura}={_ref.Estado("DOCUMENTOSPROVEEDOR", "ANULADO")}");
        _recibos.Operaciones.Should().Contain("AnularCtaCte 999");
        _stock.Operaciones.Should().Contain($"AnularMovimientosStock {IdFactura}/{R.Fc}").And.Contain($"LiberarNrosSerie {R.Fc}/{IdFactura}");
    }

    [Fact]
    public async Task Anular_ConPagos_409()
    {
        Factura(estado: "GENERADO");
        _compras.CtaCte[(R.Fc, IdFactura)].Saldo = 100;

        var act = () => AnularSut().ExecuteAsync(IdFactura);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*pagos*");
        _stock.Operaciones.Should().BeEmpty();
    }

    [Fact]
    public async Task Anular_Pagada_409()
    {
        Factura(estado: "PAGADO");

        var act = () => AnularSut().ExecuteAsync(IdFactura);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Anular_NoEsFactura_400()
    {
        var act = () => AnularSut().ExecuteAsync(IdRemito);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no es una factura de compra*");
    }

    // ---------- Lecturas ----------

    [Fact]
    public async Task PendientesFacturar_SoloDeLaSucursalSinRolGlobal()
    {
        _compras.Documentos[IdOrden].IdSucursal = 2;

        var r = await new GetComprobantesCompraParaFacturarUseCase(_compras, _ref, _user.Object).ExecuteAsync(5);

        r.Select(x => x.IdDocumentoProveedor).Should().Equal(IdRemito);

        _user.Setup(u => u.IsInRole("CEO")).Returns(true);
        (await new GetComprobantesCompraParaFacturarUseCase(_compras, _ref, _user.Object).ExecuteAsync(5)).Should().HaveCount(2);
    }

    [Fact]
    public async Task LineasPendientes_TraeLaRelacion()
    {
        var r = await new GetLineasPendientesCompraUseCase(_compras, _ref).ExecuteAsync(IdRemito);

        r.Single().Should().Match<LineaPendienteCompraDisplay>(l => l.IdItem == 10 && l.Cantidad == 5 && l.Relacion == new LineaRelacionDisplay(IdRemito, R.Rc, 801));
    }

    // ---------- helpers ----------

    private CreateFacturaCompraUseCase CreateSut() =>
        new(_compras, _stock, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private AnularFacturaCompraUseCase AnularSut() =>
        new(_compras, _stock, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private void Origen(int id, int tipo, long idDetalle, int idItem, decimal saldo)
    {
        _compras.Documentos[id] = new DocumentosProveedor { IdDocumentoProveedor = id, IdComprobanteTipo = tipo, IdProveedor = 5, IdSucursal = 1, Estado = 69, Letra = "R", PuntoVenta = "0001", Numero = id.ToString("00000000") };
        _compras.LineasPendientes[id] = [new LineaPendienteCompraRow(new DocumentosProveedorDetalle { IdDocumentoProveedorDetalle = idDetalle, IdDocumentoProveedor = id, IdItem = idItem, Descripcion = "INSUMO" }, tipo, saldo)];
    }

    private void Factura(string estado)
    {
        _compras.Documentos[IdFactura] = new DocumentosProveedor
        {
            IdDocumentoProveedor = IdFactura, IdComprobanteTipo = R.Fc, IdProveedor = 5, IdSucursal = 1, Letra = "A", PuntoVenta = "0012", Numero = "00000345",
            Estado = _ref.Estado("DOCUMENTOSPROVEEDOR", estado), TotalGeneral = 500,
        };
        _compras.Detalles.Add(new DocumentosProveedorDetalle { IdDocumentoProveedorDetalle = 950, IdDocumentoProveedor = IdFactura, IdItem = 10, Descripcion = "insumo" });
        _compras.CtaCte[(R.Fc, IdFactura)] = new EntidadesCtaCte { IdEntidadCtaCte = 999, Total = 500, Saldo = 500 };
    }

    private static EntidadesCtaCteStockMovimientosDetalle Mov(int idDetalle, int idItem, decimal cantidad, int rel, int relTipo, int relDetalle = 0) => new()
    {
        IdComprobante = IdFactura, IdComprobanteTipo = R.Fc, IdComprobanteDetalle = idDetalle, IdItem = idItem, Total = cantidad, IdSucursal = 1,
        Concepto = "FC -A-0012-00000345", IdComprobanteRelacion = rel, IdComprobanteRelacionTipo = relTipo, IdComprobanteRelacionDetalle = relDetalle,
    };

    private static CreateFacturaCompraDto Dto(params FacturaCompraItemDto[] items) => new()
    {
        IdProveedor = 5,
        IdSucursal = 1,
        Letra = "a",
        PuntoVenta = "12",
        Numero = "345",
        FechaEmision = Fecha,
        Neto = items.Sum(i => i.PrecioNeto),
        Iva = items.Sum(i => i.Iva),
        Otros = items.Sum(i => i.Otros),
        Total = items.Sum(i => i.Total),
        Items = [.. items],
    };

    private static FacturaCompraItemDto Item(int idItem, decimal cantidad, decimal neto = 100, decimal iva = 21, int idImpuesto = 1, decimal otros = 0, (int Doc, int Detalle)? relacion = null) => new()
    {
        IdItem = idItem,
        Descripcion = "insumo",
        Cantidad = cantidad,
        PrecioUnitario = neto / cantidad,
        PrecioNeto = neto,
        Iva = iva,
        Otros = otros,
        Total = neto + iva + otros,
        IdImpuestoIva = idImpuesto,
        Relacion = relacion is { } r ? new ComprobanteRelacionadoDetalleDto { IdDocumentoCliente = r.Doc, IdComprobanteTipo = R.Rc, IdDocumentoClienteDetalle = r.Detalle } : null,
    };
}
