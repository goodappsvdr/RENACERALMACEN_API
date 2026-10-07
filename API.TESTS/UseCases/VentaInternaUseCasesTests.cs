using API.DA.Entities;
using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Ventas;
using API.SERVICE.Models.Clientes;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Clientes;
using API.SERVICE.UseCases.Ventas;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Moq;
using R = API.TESTS.Infrastructure.FakeReferencias;

namespace API.TESTS.UseCases;

/// <summary>Comprobante interno de venta (VEN): paridad con Agregar_Ws / Editar_Ws de FrmFacturas.</summary>
public class VentaInternaUseCasesTests
{
    private static readonly DateTime Ahora = new(2026, 10, 6, 17, 45, 30);
    private static readonly DateTime Fecha = new(2026, 10, 6);

    private readonly FakeReciboCobroRepository _recibos = new();
    private readonly FakeVentaRepository _ventas;
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    public VentaInternaUseCasesTests()
    {
        _ventas = new FakeVentaRepository(_recibos);
        _user.SetupGet(u => u.IdUsuario).Returns(3);
        // Por defecto: cliente con cta. cte. habilitada y límite holgado.
        _recibos.Entidades[5].CtaCte = true;
        _recibos.Entidades[5].LimiteCtaCte = 1_000_000;
        _recibos.Entidades[5].DiasInteres = 30;
    }

    // ---------- Alta ----------

    [Fact]
    public async Task Create_VentaDirectaACtaCte_GrabaCabeceraDetalleStockYCtaCte()
    {
        var result = await CreateSut().ExecuteAsync(Dto(Item(10, cantidad: 2, total: 2420)));

        var doc = _ventas.Single<DocumentosCliente>();
        result.Comprobante.IdDocumentoCliente.Should().Be(doc.IdDocumentoCliente);
        result.IdRecibo.Should().BeNull();
        doc.Should().Match<DocumentosCliente>(d =>
            d.IdComprobanteTipo == R.Ven && d.Letra == "X" && d.PuntoVenta == "0003" && d.Numero == "00000042" &&
            d.Estado == 42 && d.IdCondicion == 14 && d.Cae == "0" && d.IdPlanillaCaja == 77 &&
            d.FechaEmision == Fecha.Add(Ahora.TimeOfDay) && d.RazonSocial == "ALMACEN DON PEPE" && d.TotalGeneral == 2420);

        _ventas.Single<DocumentosClienteDetalle>().Should().Match<DocumentosClienteDetalle>(d => d.Descripcion == "YERBA" && d.Cantidad == 2);
        _ventas.Single<DocumentosClienteVencimientos>().FechaVencimiento.Should().Be(doc.FechaEmision!.Value.AddDays(30));

        // Venta directa: descuenta stock y deja el movimiento
        _ventas.Operaciones.Should().Contain("RestarStock 10@1 2");
        _ventas.Single<ItemsMovimientosDetalles>().Should().Match<ItemsMovimientosDetalles>(m =>
            m.Haber == 2 && m.Total2 == -2 && m.Concepto == "VEN -X-0003-00000042" && m.FechaAlta == Ahora);
        _ventas.Single<EntidadesCtaCteStockMovimientosDetalle>().Should().Match<EntidadesCtaCteStockMovimientosDetalle>(s =>
            s.Total == 2 && s.Saldo == 2 && s.Saldo2 == 2 && s.IdComprobanteRelacion == 0);

        // Cta. cte. por el total, y el comprobante queda habilitado para remitarse
        _ventas.Single<EntidadesCtaCte>().Should().Match<EntidadesCtaCte>(c =>
            c.Total == 2420 && c.Saldo == 2420 && c.Total2 == 2420 && c.Cancelado == false && c.Concepto == "VEN -X-0003-00000042");
        _ventas.Single<EntidadesCtaCteMovimientos>().Should().Match<EntidadesCtaCteMovimientos>(m =>
            m.EnContraEntidad == 2420 && m.IdElementoCobroPago == R.ElementoCtaCte);
        _ventas.Operaciones.Should().Contain($"DeterminarRemitar {doc.IdDocumentoCliente}");
        _recibos.Bloqueos.Should().Equal(5);
    }

    [Fact]
    public async Task Create_ConFormaDePago_GeneraElReciboQueLaCancelaEnLaMismaTransaccion()
    {
        _recibos.Entidades[5].CtaCte = false; // contado
        var dto = Dto(Item(10, cantidad: 1, total: 1000));
        dto.Elementos = [new ElementoCobroDto { IdElementoCobro = R.Efectivo, Importe = 1000, Descripcion = "EFECTIVO" }];

        var result = await CreateSut().ExecuteAsync(dto);

        var doc = _ventas.Single<DocumentosCliente>();
        result.IdRecibo.Should().Be(_recibos.Single<EntidadesRecibos>().IdEntidadRecibo);
        _recibos.Single<EntidadRecibosDocumentosCliente>().Should().Match<EntidadRecibosDocumentosCliente>(i =>
            i.IdDocumentoCliente == doc.IdDocumentoCliente && i.IdComprobanteTipo == R.Ven && i.ImporteRecibo == 1000 && i.Saldo == 0);
        _recibos.EstadosDocumentoCliente[doc.IdDocumentoCliente].Should().Be(_ref.Estado(EstadosCobranza.DocumentoCobrado));
        _recibos.Single<CajasPlanillasDetalle>().Debe.Should().Be(1000);
    }

    [Fact]
    public async Task Create_ClienteSinCtaCteYSinFormaDePago_Rechaza()
    {
        _recibos.Entidades[5].CtaCte = false;

        var act = () => CreateSut().ExecuteAsync(Dto(Item(10, 1, 100)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no tiene cuenta corriente habilitada*");
        _ventas.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_SuperaLimiteSinConfirmar_Conflict()
    {
        _recibos.Entidades[5].LimiteCtaCte = 1000;
        _ventas.SaldoCtaCte = 900;

        var act = () => CreateSut().ExecuteAsync(Dto(Item(10, 1, 200)));

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*superó el límite*confirmarExcesoLimite*");
    }

    [Fact]
    public async Task Create_SuperaLimiteConfirmado_Graba()
    {
        _recibos.Entidades[5].LimiteCtaCte = 1000;
        _ventas.SaldoCtaCte = 900;
        var dto = Dto(Item(10, 1, 200));
        dto.ConfirmarExcesoLimite = true;

        await CreateSut().ExecuteAsync(dto);

        _ventas.All<DocumentosCliente>().Should().ContainSingle();
    }

    [Fact]
    public async Task Create_LineaDesdeRemito_NoMueveStockYConsumeElSaldoDelRemito()
    {
        var item = Item(10, 3, 300);
        item.Relacion = new ComprobanteRelacionadoDetalleDto { IdDocumentoCliente = 80, IdComprobanteTipo = 2, IdDocumentoClienteDetalle = 81 };
        var dto = Dto(item);
        dto.Remitos = [new ComprobanteRelacionadoDto { IdDocumentoCliente = 80, IdComprobanteTipo = 2 }];

        await CreateSut().ExecuteAsync(dto);

        _ventas.Operaciones.Should().NotContain(o => o.StartsWith("RestarStock"));
        _ventas.Operaciones.Should().Contain("AjustarSaldoStock 80/81/2 item=10 -3");
        _ventas.Single<EntidadesCtaCteStockMovimientosDetalle>().Should().Match<EntidadesCtaCteStockMovimientosDetalle>(s =>
            s.Total == 3 && s.Saldo == 0 && s.Saldo2 == -3 && s.IdComprobanteRelacion == 80);
        _ventas.Single<DocumentosClienteRemitos>().IdRemito.Should().Be(80);
        _ventas.Operaciones.Should().Contain($"EstadoPendiente 80={_ref.Estado("DOCUMENTOSCLIENTE", "FACTURADO")} pendiente=False");
        _ventas.Operaciones.Should().NotContain(o => o.StartsWith("DeterminarRemitar"), "un remito ya se remitó");
    }

    [Fact]
    public async Task Create_LineaDesdePresupuestoConSaldoPendiente_DescuentaStockYQuedaFacturadoParcial()
    {
        var item = Item(10, 1, 100);
        item.Relacion = new ComprobanteRelacionadoDetalleDto { IdDocumentoCliente = 90, IdComprobanteTipo = R.Pv, IdDocumentoClienteDetalle = 91 };
        var dto = Dto(item);
        dto.Remitos = [new ComprobanteRelacionadoDto { IdDocumentoCliente = 90, IdComprobanteTipo = R.Pv }];
        _ventas.ConPendienteRemitar.Add(90);

        await CreateSut().ExecuteAsync(dto);

        _ventas.Operaciones.Should().Contain("AjustarSaldoStock 90/91/1 item=10 -1").And.Contain("RestarStock 10@1 1");
        _ventas.Operaciones.Should().Contain($"EstadoPendiente 90={_ref.Estado("DOCUMENTOSCLIENTE", "FACTURADO PARCIAL")} pendiente=True");
        _ventas.Operaciones.Should().Contain(o => o.StartsWith("DeterminarRemitar"), "lo facturado de un presupuesto queda para remitar");
    }

    [Fact]
    public async Task Create_OfertaPorAgotamientoYNroSerie()
    {
        _ventas.Oferta = new OfertaActivaRow(IdOferta: 33, TipoOferta: 72);
        var item = Item(10, 2, 200);
        item.IdNroSerie = 555;

        await CreateSut().ExecuteAsync(Dto(item));

        var detalle = _ventas.Single<DocumentosClienteDetalle>();
        _ventas.Operaciones.Should().Contain("AjustarOferta 33 -2")
            .And.Contain($"AsignarNroSerie 555 -> {detalle.IdDocumentoCliente}/{detalle.IdDocumentoClienteDetalle}");
    }

    [Fact]
    public async Task Create_ObservacionLarga_CabeceraTruncadaYCompletaEnSuTabla()
    {
        var dto = Dto(Item(10, 1, 100));
        dto.Observaciones = new string('x', 80);

        await CreateSut().ExecuteAsync(dto);

        _ventas.Single<DocumentosCliente>().Observaciones.Should().HaveLength(50);
        _ventas.Single<DocumentosClienteObservaciones>().Observaciones.Should().HaveLength(80);
    }

    [Fact]
    public async Task Create_DatosDelClienteInformados_PisanLaFicha()
    {
        var dto = Dto(Item(10, 1, 100));
        dto.RazonSocial = "JUAN PEREZ";

        await CreateSut().ExecuteAsync(dto);

        _ventas.Single<DocumentosCliente>().Should().Match<DocumentosCliente>(d => d.RazonSocial == "JUAN PEREZ" && d.NroDoc == "20123456789");
    }

    [Fact]
    public async Task Create_SinPlanillaAbierta_Rechaza()
    {
        _recibos.Planilla = null;

        var act = () => CreateSut().ExecuteAsync(Dto(Item(10, 1, 100)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*planilla de caja abierta*");
    }

    // ---------- Anulación ----------

    [Fact]
    public async Task Anular_VentaDirecta_DevuelveStockYAnulaTodo()
    {
        Documento(700, estado: 42);
        _ventas.Detalles.Add(new DocumentosClienteDetalle { IdDocumentoClienteDetalle = 701, IdDocumentoCliente = 700, IdItem = 10, Cantidad = 2, Descripcion = "yerba" });
        _ventas.Oferta = new OfertaActivaRow(33, 72);
        _ventas.ReciboImputado = 300;

        await AnularSut().ExecuteAsync(700);

        _ventas.Operaciones.Should().ContainInOrder(
            "AjustarOferta 33 2", "SumarStock 10@1 2", "AnularDetalle 701",
            $"AnularMovimientosStock 700/{R.Ven}",
            $"LiberarNrosSerie {R.Ven}/700",
            $"AnularDocumento 700={_ref.Estado("DOCUMENTOSCLIENTE", "ANULADO")}");
        _ventas.Single<ItemsMovimientosDetalles>().Should().Match<ItemsMovimientosDetalles>(m =>
            m.Debe == 2 && m.Total2 == 2 && m.Concepto == "VEN -X-0003-00000042" && m.Item == "YERBA");

        // caja/cheques/bancos/retenciones/cta. cte. del comprobante y del recibo cobrado en el momento
        _recibos.Operaciones.Should().Contain($"AnularCaja {R.Ven}/700").And.Contain($"AnularCaja {R.Rec}/300").And.Contain("AnularDetalle 300");
        _recibos.EstadosRecibo[300].Should().Be(_ref.Estado(EstadosCobranza.ReciboAnulado));
        _recibos.Operaciones.Count(o => o.StartsWith("AnularCtaCte")).Should().Be(2);
    }

    [Fact]
    public async Task Anular_ConRemitos_DevuelveSaldosYLiberaLosRemitos()
    {
        Documento(700, estado: 104);
        _ventas.RemitosAsociados.Add(new DocumentosClienteRemitos { IdDocumentoClienteRemito = 1, IdDocumentoCliente = 700, IdRemito = 80, IdComprobanteTipo = 2 });
        _ventas.RemitosAsociados.Add(new DocumentosClienteRemitos { IdDocumentoClienteRemito = 2, IdDocumentoCliente = 700, IdRemito = 90, IdComprobanteTipo = R.Pv });
        _ventas.MovimientosStock.Add(new EntidadesCtaCteStockMovimientosDetalle
            { IdComprobante = 700, IdComprobanteTipo = R.Ven, IdItem = 10, Total = 3, IdSucursal = 1, IdComprobanteRelacion = 80, IdComprobanteRelacionTipo = 2, IdComprobanteRelacionDetalle = 81 });
        _ventas.MovimientosStock.Add(new EntidadesCtaCteStockMovimientosDetalle
            { IdComprobante = 700, IdComprobanteTipo = R.Ven, IdItem = 11, Total = 1, IdSucursal = 1, IdComprobanteRelacion = 90, IdComprobanteRelacionTipo = R.Pv, IdComprobanteRelacionDetalle = 91, Concepto = "VEN -X-0003-00000042" });
        _ventas.SaldosStock[90] = 5; // al presupuesto le queda saldo

        await AnularSut().ExecuteAsync(700);

        _ventas.Operaciones.Should().Contain("AjustarSaldoStock 80/81/2 item=10 3").And.Contain("AjustarSaldoStock 90/91/1 item=11 1");
        _ventas.Operaciones.Should().Contain("SumarStock 11@1 1").And.NotContain("SumarStock 10@1 3");
        _ventas.Operaciones.Should().Contain($"EstadoPendiente 80={_ref.Estado("DOCUMENTOSCLIENTE", "GENERADO")} pendiente=True")
            .And.Contain($"EstadoPendiente 90={_ref.Estado("DOCUMENTOSCLIENTE", "FACTURADO PARCIAL")} pendiente=True")
            .And.Contain("BorrarRemitoAsociado 1").And.Contain("BorrarRemitoAsociado 2");
        _ventas.Single<ItemsMovimientosDetalles>().Item.Should().Be("DEVOLUCION POR ANULACION");
    }

    [Fact]
    public async Task Anular_EstadoNoAnulable_Conflict()
    {
        Documento(700, estado: 43);

        var act = () => AnularSut().ExecuteAsync(700);

        await act.Should().ThrowAsync<ConflictException>();
        _ventas.Operaciones.Should().BeEmpty();
    }

    [Fact]
    public async Task Anular_FacturaElectronica_NoSePuedePorAca()
    {
        Documento(700, estado: 42, tipo: R.Fv);

        var act = () => AnularSut().ExecuteAsync(700);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*nota de crédito*");
    }

    [Fact]
    public async Task Iniciar_DevuelvePlanillaYNumero()
    {
        var result = await new IniciarVentaInternaUseCase(_recibos, _ref, _user.Object).ExecuteAsync();

        result.Should().Be(new NuevaVentaInternaDisplay(77, "0003", "X", false, "00000042"));
    }

    // ---------- helpers ----------

    private CreateVentaInternaUseCase CreateSut()
    {
        var clock = new FixedServerClock(Ahora);
        var writer = new VentaWriter(_ventas, new ReciboCobroWriter(_recibos, _ref, clock), _ref);
        return new CreateVentaInternaUseCase(writer, _recibos, _ref, new InlineUnitOfWork(), clock, _user.Object);
    }

    private AnularVentaInternaUseCase AnularSut() =>
        new(new VentaAnulador(_ventas, _recibos, _ref), _ventas, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private void Documento(int id, int estado, int tipo = R.Ven) =>
        _ventas.Documentos[id] = new DocumentosCliente
        {
            IdDocumentoCliente = id, IdComprobanteTipo = tipo, Estado = estado, IdCliente = 5, IdSucursal = 1,
            Letra = "X", PuntoVenta = "0003", Numero = "00000042",
        };

    private static CreateVentaInternaDto Dto(params VentaItemDto[] items) => new()
    {
        IdEntidad = 5,
        FechaEmision = Fecha,
        IdSucursal = 1,
        Total = items.Sum(i => i.Total),
        Neto = items.Sum(i => i.PrecioNeto),
        Iva = items.Sum(i => i.Iva),
        Items = [.. items],
    };

    private static VentaItemDto Item(int idItem, decimal cantidad, decimal total) => new()
    {
        IdItem = idItem,
        Descripcion = "yerba",
        Cantidad = cantidad,
        PrecioUnitario = total / cantidad,
        PrecioNeto = Math.Round(total / 1.21m, 2),
        IvaAlicuota = 21,
        Iva = total - Math.Round(total / 1.21m, 2),
        Total = total,
        IdImpuestoIva = 1,
    };
}
