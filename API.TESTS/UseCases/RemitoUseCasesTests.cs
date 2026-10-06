using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Ventas;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Ventas;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Moq;
using R = API.TESTS.Infrastructure.FakeReferencias;

namespace API.TESTS.UseCases;

/// <summary>Remito de venta (RV): paridad con Agregar_Ws / Editar_Ws de FrmRemitos.</summary>
public class RemitoUseCasesTests
{
    private const int IdPresupuesto = 200, IdVenta = 210;
    private static readonly DateTime Ahora = new(2026, 10, 6, 17, 45, 30);
    private static readonly DateTime Fecha = new(2026, 10, 6);

    private readonly FakeReciboCobroRepository _recibos = new();
    private readonly FakeVentaRepository _ventas;
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    public RemitoUseCasesTests()
    {
        _ventas = new FakeVentaRepository(_recibos);
        _user.SetupGet(u => u.IdUsuario).Returns(3);

        Origen(IdPresupuesto, R.Pv, (901, 10, 5m));
        Origen(IdVenta, R.Ven, (911, 11, 2m));
    }

    // ---------- Alta ----------

    [Fact]
    public async Task Create_Directo_DescuentaStockYQuedaParaFacturar()
    {
        var remito = await CreateSut().ExecuteAsync(Dto(Item(10, 3, otros: 7)));

        var doc = _ventas.Single<DocumentosCliente>();
        doc.Should().Match<DocumentosCliente>(d =>
            d.IdComprobanteTipo == R.Rv && d.Letra == "R" && d.PuntoVenta == "0003" && d.Numero == "00000042" && d.IdCliente == 5
            && d.RazonSocial == "ALMACEN DON PEPE" && d.FechaEmision == Fecha && d.Estado == 42);
        remito.IdDocumentoCliente.Should().Be(doc.IdDocumentoCliente);
        _recibos.Bloqueos.Should().Contain(5);
        _recibos.Operaciones.Should().Contain("Numerar 0003");

        _ventas.All<DocumentosClienteDetalle>().Single().Otros.Should().Be(7, "cada línea graba su Otros (el ERP grababa el de la cabecera)");
        _ventas.Operaciones.Should().Contain("RestarStock 10@1 3");
        _ventas.Single<ItemsMovimientosDetalles>().Should().Match<ItemsMovimientosDetalles>(m =>
            m.Concepto == "RV -R-0003-00000042" && m.Haber == 3 && m.Total2 == -3 && m.FechaAlta == Ahora);
        _ventas.Single<EntidadesCtaCteStockMovimientosDetalle>().Should().Match<EntidadesCtaCteStockMovimientosDetalle>(s =>
            s.IdComprobanteTipo == R.Rv && s.Total == 3 && s.Saldo == 3 && s.Saldo2 == 3 && s.IdComprobanteRelacion == 0);
        _ventas.Operaciones.Should().Contain($"DeterminarRemitar {doc.IdDocumentoCliente}");
        _ventas.Single<ComprobantesCarga>().IdComprobante.Should().Be(doc.IdDocumentoCliente, "el SP del ERP grababa el ID_Usuario en ID_Comprobante");
        _ventas.All<DocumentosClienteRemitos>().Should().BeEmpty();
    }

    [Fact]
    public async Task Create_DesdePresupuesto_ConsumeSaldoDescuentaStockYQuedaEntregadoParcial()
    {
        _ventas.ConPendienteRemitar.Add(IdPresupuesto); // quedan 3 de 5

        await CreateSut().ExecuteAsync(Dto([IdPresupuesto], Item(10, 2, relacion: (IdPresupuesto, R.Ven, 901))));

        var doc = _ventas.Single<DocumentosCliente>();
        _ventas.Operaciones.Should().Contain($"AjustarSaldoStock {IdPresupuesto}/901/{R.Pv} item=10 -2", "el tipo sale del comprobante, no del request");
        _ventas.Operaciones.Should().Contain("RestarStock 10@1 2");
        _ventas.Single<EntidadesCtaCteStockMovimientosDetalle>().Should().Match<EntidadesCtaCteStockMovimientosDetalle>(s =>
            s.Saldo == 2 && s.Saldo2 == 2 && s.IdComprobanteRelacion == IdPresupuesto && s.IdComprobanteRelacionTipo == R.Pv && s.IdComprobanteRelacionDetalle == 901);
        _ventas.Single<DocumentosClienteRemitos>().Should().Match<DocumentosClienteRemitos>(r =>
            r.IdDocumentoCliente == IdPresupuesto && r.IdRemito == doc.IdDocumentoCliente && r.IdComprobanteTipo == R.Pv);
        _ventas.Operaciones.Should().Contain($"EstadoPendiente {IdPresupuesto}={_ref.Estado("DOCUMENTOSCLIENTE", "ENTREGADO PARCIAL")} pendiente=True");
        _ventas.Operaciones.Should().Contain($"DeterminarRemitar {doc.IdDocumentoCliente}", "lo presupuestado queda para facturar");
    }

    [Fact]
    public async Task Create_DesdeVenta_SoloConsumeSaldoYQuedaEntregado()
    {
        await CreateSut().ExecuteAsync(Dto([IdVenta], Item(11, 2, relacion: (IdVenta, R.Ven, 911))));

        var doc = _ventas.Single<DocumentosCliente>();
        _ventas.Operaciones.Should().NotContain(o => o.StartsWith("RestarStock"), "el stock lo descontó la venta");
        _ventas.Operaciones.Should().Contain($"AjustarSaldoStock {IdVenta}/911/{R.Ven} item=11 -2");
        _ventas.Single<EntidadesCtaCteStockMovimientosDetalle>().Should().Match<EntidadesCtaCteStockMovimientosDetalle>(s => s.Saldo == 0 && s.Saldo2 == -2);
        _ventas.Operaciones.Should().Contain($"EstadoPendiente {IdVenta}={_ref.Estado("DOCUMENTOSCLIENTE", "ENTREGADO")} pendiente=False");
        _ventas.Operaciones.Should().NotContain($"DeterminarRemitar {doc.IdDocumentoCliente}");
    }

    [Fact]
    public async Task Create_ComprobanteDeOtroCliente_Rechaza()
    {
        _ventas.Documentos[IdVenta].IdCliente = 99;

        var act = () => CreateSut().ExecuteAsync(Dto([IdVenta], Item(11, 1, relacion: (IdVenta, R.Ven, 911))));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no es del cliente*");
        _ventas.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ComprobanteSinPendiente_409()
    {
        _ventas.Documentos[IdVenta].Pendiente = false;

        var act = () => CreateSut().ExecuteAsync(Dto([IdVenta], Item(11, 1, relacion: (IdVenta, R.Ven, 911))));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Create_MasQueElPendiente_Rechaza()
    {
        var act = () => CreateSut().ExecuteAsync(Dto([IdVenta], Item(11, 2, relacion: (IdVenta, R.Ven, 911)), Item(11, 1, relacion: (IdVenta, R.Ven, 911))));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*el pendiente es 2*");
    }

    [Fact]
    public async Task Create_LineaDeOtroItem_Rechaza()
    {
        var act = () => CreateSut().ExecuteAsync(Dto([IdVenta], Item(99, 1, relacion: (IdVenta, R.Ven, 911))));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*otro ítem*");
    }

    [Fact]
    public async Task Create_LineaRelacionadaSinElComprobante_Rechaza()
    {
        var act = () => CreateSut().ExecuteAsync(Dto(Item(11, 1, relacion: (IdVenta, R.Ven, 911))));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*incluirlo en Comprobantes*");
    }

    [Fact]
    public async Task Create_ComprobanteSinLineas_Rechaza()
    {
        var act = () => CreateSut().ExecuteAsync(Dto([IdVenta], Item(10, 1)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no entrega ninguna línea*");
    }

    [Fact]
    public async Task Create_SinLetraConfigurada_Rechaza()
    {
        _ventas.Letra = null;

        var act = () => CreateSut().ExecuteAsync(Dto(Item(10, 1)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*ComprobantesLetras*");
    }

    // ---------- Anulación ----------

    [Fact]
    public async Task Anular_Directo_DevuelveStockYAnula()
    {
        var id = Remito((0, 0, 0, 10, 3m));

        await AnularSut().ExecuteAsync(id);

        _ventas.Operaciones.Should().Contain("SumarStock 10@1 3");
        _ventas.Single<ItemsMovimientosDetalles>().Should().Match<ItemsMovimientosDetalles>(m => m.Debe == 3 && m.Item == "TORNILLO");
        _ventas.Operaciones.Should().ContainInOrder(
            $"AnularMovimientosStock {id}/{R.Rv}",
            $"LiberarNrosSerie {R.Rv}/{id}",
            $"AnularDocumento {id}={_ref.Estado("DOCUMENTOSCLIENTE", "ANULADO")}");
        _recibos.Bloqueos.Should().Contain(5);
    }

    [Fact]
    public async Task Anular_DePresupuestoYVenta_DevuelveSaldosYSoloElStockDelPresupuesto()
    {
        var id = Remito((IdPresupuesto, R.Pv, 901, 10, 2m), (IdVenta, R.Ven, 911, 11, 1m));
        _ventas.RemitosAsociados.Add(new DocumentosClienteRemitos { IdDocumentoClienteRemito = 1, IdDocumentoCliente = IdPresupuesto, IdRemito = id, IdComprobanteTipo = R.Pv });
        _ventas.RemitosAsociados.Add(new DocumentosClienteRemitos { IdDocumentoClienteRemito = 2, IdDocumentoCliente = IdVenta, IdRemito = id, IdComprobanteTipo = R.Ven });
        _ventas.SaldosStock[IdVenta] = 1; // a la venta le queda algo entregado por otro remito

        await AnularSut().ExecuteAsync(id);

        _ventas.Operaciones.Should().Contain($"AjustarSaldoStock {IdPresupuesto}/901/{R.Pv} item=10 2");
        _ventas.Operaciones.Should().Contain($"AjustarSaldoStock {IdVenta}/911/{R.Ven} item=11 1");
        _ventas.Operaciones.Should().ContainSingle(o => o.StartsWith("SumarStock")).Which.Should().Be("SumarStock 10@1 2");
        _ventas.Operaciones.Should().Contain($"EstadoPendiente {IdPresupuesto}={_ref.Estado("DOCUMENTOSCLIENTE", "GENERADO")} pendiente=True");
        _ventas.Operaciones.Should().Contain($"EstadoPendiente {IdVenta}={_ref.Estado("DOCUMENTOSCLIENTE", "ENTREGADO PARCIAL")} pendiente=True");
        _ventas.Operaciones.Should().Contain("BorrarRemitoAsociado 1").And.Contain("BorrarRemitoAsociado 2");
    }

    [Fact]
    public async Task Anular_Facturado_409()
    {
        var id = Remito((0, 0, 0, 10, 3m));
        _ventas.RemitosAsociados.Add(new DocumentosClienteRemitos { IdDocumentoClienteRemito = 3, IdDocumentoCliente = 777, IdRemito = id, IdComprobanteTipo = R.Rv });

        var act = () => AnularSut().ExecuteAsync(id);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*facturado (comprobante 777)*");
        _ventas.Operaciones.Should().BeEmpty();
    }

    [Fact]
    public async Task Anular_YaAnulado_409()
    {
        var id = Remito((0, 0, 0, 10, 3m));
        _ventas.Documentos[id].Estado = _ref.Estado("DOCUMENTOSCLIENTE", "ANULADO");

        var act = () => AnularSut().ExecuteAsync(id);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Anular_NoEsRemito_400()
    {
        var act = () => AnularSut().ExecuteAsync(IdVenta);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no es un remito*");
    }

    // ---------- Lecturas ----------

    [Fact]
    public async Task ComprobantesParaRemitir_SoloPendientesDelCliente()
    {
        _ventas.Documentos[IdVenta].Estado = _ref.Estado("DOCUMENTOSCLIENTE", "ANULADO");

        var r = await new GetComprobantesParaRemitirUseCase(_ventas, _ref).ExecuteAsync(5);

        r.Should().ContainSingle().Which.Should().Match<ComprobanteParaRemitirDisplay>(c =>
            c.IdDocumentoCliente == IdPresupuesto && c.IdComprobanteTipo == R.Pv && c.Comprobante == "X-0001-00000200");
    }

    [Fact]
    public async Task LineasPendientes_CantidadEsElSaldoYTraeLaRelacion()
    {
        var r = await new GetLineasPendientesUseCase(_ventas).ExecuteAsync(IdPresupuesto);

        r.Should().ContainSingle().Which.Should().Match<LineaPendienteDisplay>(l =>
            l.IdItem == 10 && l.Cantidad == 5 && l.Relacion == new LineaRelacionDisplay(IdPresupuesto, R.Pv, 901));
    }

    [Fact]
    public async Task Iniciar_LetraSegunClienteYNumeroSugerido()
    {
        var r = await new IniciarRemitoUseCase(_ventas, _recibos, _ref, _user.Object).ExecuteAsync(5);

        r.Should().Be(new NuevaVentaInternaDisplay(77, "0003", "R", false, "00000042"));
    }

    // ---------- helpers ----------

    private CreateRemitoUseCase CreateSut() =>
        new(_ventas, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private AnularRemitoUseCase AnularSut() =>
        new(_ventas, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private void Origen(int id, int tipo, params (long Detalle, int Item, decimal Saldo)[] lineas)
    {
        _ventas.Documentos[id] = new DocumentosCliente
        {
            IdDocumentoCliente = id, IdComprobanteTipo = tipo, IdCliente = 5, Letra = "X", PuntoVenta = "0001", Numero = id.ToString("00000000"),
            Remitar = true, Pendiente = true, Estado = 42, TotalGeneral = 1000,
        };
        _ventas.LineasPendientes[id] = lineas
            .Select(l => new LineaPendienteRow(new DocumentosClienteDetalle { IdDocumentoClienteDetalle = l.Detalle, IdDocumentoCliente = id, IdItem = l.Item, Descripcion = "ARTICULO" }, tipo, l.Saldo))
            .ToList();
    }

    /// <summary>Remito ya grabado con un movimiento de stock por línea.</summary>
    private int Remito(params (int Rel, int RelTipo, int RelDetalle, int Item, decimal Cantidad)[] lineas)
    {
        const int id = 300;
        _ventas.Documentos[id] = new DocumentosCliente
        {
            IdDocumentoCliente = id, IdComprobanteTipo = R.Rv, IdCliente = 5, IdSucursal = 1, Letra = "R", PuntoVenta = "0003", Numero = "00000042",
            Estado = _ref.Estado("DOCUMENTOSCLIENTE", "GENERADO"),
        };
        var detalle = 950;
        foreach (var l in lineas)
        {
            _ventas.Detalles.Add(new DocumentosClienteDetalle { IdDocumentoClienteDetalle = detalle, IdDocumentoCliente = id, IdItem = l.Item, Descripcion = "tornillo", Cantidad = l.Cantidad });
            _ventas.MovimientosStock.Add(new EntidadesCtaCteStockMovimientosDetalle
            {
                IdComprobante = id, IdComprobanteTipo = R.Rv, IdComprobanteDetalle = detalle, IdItem = l.Item, Total = l.Cantidad, IdSucursal = 1,
                Concepto = "RV -R-0003-00000042", IdComprobanteRelacion = l.Rel, IdComprobanteRelacionTipo = l.RelTipo, IdComprobanteRelacionDetalle = l.RelDetalle,
            });
            detalle++;
        }
        return id;
    }

    private static CreateRemitoDto Dto(params VentaItemDto[] items) => Dto([], items);

    private static CreateRemitoDto Dto(int[] comprobantes, params VentaItemDto[] items) => new()
    {
        IdEntidad = 5,
        IdSucursal = 1,
        FechaEmision = Fecha,
        Total = items.Sum(i => i.Total),
        Items = [.. items],
        Comprobantes = comprobantes.Select(c => new ComprobanteRelacionadoDto { IdDocumentoCliente = c, IdComprobanteTipo = R.Ven }).ToList(),
    };

    private static VentaItemDto Item(int idItem, decimal cantidad, decimal otros = 0, (int Doc, int Tipo, int Detalle)? relacion = null) => new()
    {
        IdItem = idItem,
        Descripcion = "tornillo",
        Cantidad = cantidad,
        PrecioUnitario = 100,
        PrecioNeto = 100 * cantidad,
        Otros = otros,
        Total = 100 * cantidad + otros,
        Relacion = relacion is { } r
            ? new ComprobanteRelacionadoDetalleDto { IdDocumentoCliente = r.Doc, IdComprobanteTipo = r.Tipo, IdDocumentoClienteDetalle = r.Detalle }
            : null,
    };
}
