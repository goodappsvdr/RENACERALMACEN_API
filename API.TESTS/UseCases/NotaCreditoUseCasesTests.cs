using API.DA.Entities;
using API.SERVICE.Domain.Afip;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Afip;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Ventas;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using R = API.TESTS.Infrastructure.FakeReferencias;

namespace API.TESTS.UseCases;

/// <summary>Nota de crédito electrónica en dos fases (FrmNotasCreditoAFIP), con AFIP simulado.</summary>
public class NotaCreditoUseCasesTests
{
    private const int IdFactura = 300;
    private static readonly DateTime Ahora = new(2026, 10, 6, 18, 0, 0);
    private static readonly DateTime Fecha = new(2026, 10, 6);

    private readonly FakeReciboCobroRepository _recibos = new();
    private readonly FakeVentaRepository _ventas;
    private readonly FakeReferencias _ref = new();
    private readonly FakeAfipGateway _afip = new();
    private readonly Mock<ICurrentUser> _user = new();

    public NotaCreditoUseCasesTests()
    {
        _ventas = new FakeVentaRepository(_recibos);
        _user.SetupGet(u => u.IdUsuario).Returns(3);

        _ventas.Documentos[IdFactura] = new DocumentosCliente
        {
            IdDocumentoCliente = IdFactura, IdComprobanteTipo = R.Fv, Letra = "A", IdPuntoVenta = 2, PuntoVenta = "0002", Numero = "00000077",
            Cae = "71000000000001", Estado = 42, IdCliente = 5, IdSucursal = 1, RazonSocial = "CLIENTE SA", NroDoc = "20123456789",
            FechaEmision = Fecha.AddDays(-10), TotalNeto = 1200, TotalIva = 231, TotalGeneral = 1431,
        };
        _ventas.Detalles.Add(new DocumentosClienteDetalle
        {
            IdDocumentoClienteDetalle = 901, IdDocumentoCliente = IdFactura, IdItem = 10, Descripcion = "TORNILLO", Cantidad = 5, IdImpuestoIva = 1, ListaPrecio = "L1",
        });
        _ventas.Detalles.Add(new DocumentosClienteDetalle
        {
            IdDocumentoClienteDetalle = 902, IdDocumentoCliente = IdFactura, IdItem = 11, Descripcion = "TUERCA", Cantidad = 2, IdImpuestoIva = 2,
        });
    }

    // ---------- Alta (fase 1 + fase 2) ----------

    [Fact]
    public async Task Create_Aprobada_PideCaeConLaFacturaAsociadaYAplicaLosEfectos()
    {
        var result = await CreateSut().ExecuteAsync(Dto(Item(901, 2, neto: 400, iva: 84)));

        result.Estado.Should().Be(EstadoAutorizacionAfip.Autorizada);

        // Borrador: datos del cliente de la factura, CAE pendiente y relación factura → NC
        var nc = _ventas.Single<DocumentosCliente>();
        nc.Should().Match<DocumentosCliente>(d =>
            d.IdComprobanteTipo == R.Nc && d.Letra == "A" && d.IdCliente == 5 && d.RazonSocial == "CLIENTE SA" && d.PuntoVenta == "0002");
        _ventas.Single<DocumentosClienteRelacion>().Should().Match<DocumentosClienteRelacion>(r =>
            r.IdDocumentoCliente1 == IdFactura && r.IdDocumentoCliente2 == nc.IdDocumentoCliente);
        var linea = _ventas.All<DocumentosClienteDetalle>().Single();
        linea.Should().Match<DocumentosClienteDetalle>(l => l.IdItem == 10 && l.Descripcion == "TORNILLO" && l.Cantidad == 2 && l.IdImpuestoIva == 1);

        // AFIP: NC A (tipo 3) con la factura A (tipo 1) 0002-77 como comprobante asociado
        var s = _afip.Solicitudes.Single();
        s.Should().Match<AfipSolicitudCae>(x => x.CbteTipo == 3 && !x.Monotributo && x.ImpNeto == 400 && x.ImpIva == 84 && x.ImpTotal == 484);
        s.ComprobantesAsociados.Should().Equal(new AfipComprobanteAsociado(1, 2, 77));
        s.Alicuotas.Should().BeEquivalentTo([new AfipAlicuota(5, 400, 84)]);

        // Efectos: stock devuelto, saldo de la línea de la factura, cta. cte. a favor, factura cancelada, nros. de serie
        _ventas.Operaciones.Should().Contain("SumarStock 10@1 2");
        _ventas.Operaciones.Should().Contain($"AjustarSaldoStock {IdFactura}/901/{R.Fv} item=10 -2");
        _ventas.Single<ItemsMovimientosDetalles>().Concepto.Should().Be("NC -A-0002-00000123");
        var ctaCte = _ventas.Single<EntidadesCtaCte>();
        ctaCte.Should().Match<EntidadesCtaCte>(c => c.IdComprobanteTipo == R.Nc && c.Saldo == -484 && c.Total2 == -484 && c.FechaVencimiento == Fecha.AddDays(30));
        _ventas.Single<EntidadesCtaCteMovimientos>().AfavorEntidad.Should().Be(484);
        _recibos.EstadosDocumentoCliente[IdFactura].Should().Be(_ref.Estado("DOCUMENTOSCLIENTE", "CANCELADO"));
        _ventas.Operaciones.Should().Contain($"LiberarNrosSerie {R.Fv}/{IdFactura}");

        // CAE, código de barras con la fecha de emisión, QR y libro IVA como NC
        var barras = AfipRules.CodigoBarras(30712345678, 3, 2, "71234567890123", Fecha.ToString("yyyyMMdd"));
        _ventas.Operaciones.Should().Contain($"DatosAfip {nc.IdDocumentoCliente} 0002-00000123 CAE=71234567890123 BAR={barras}");
        _ventas.Single<DocumentosClienteQr>();
        _ventas.Single<LibroIvaVenta>().TipoComprobante.Should().Be("3");
    }

    [Fact]
    public async Task Create_FacturaCobradaConRecibos_AnulaLosRecibosEnLugarDeCancelarla()
    {
        _ventas.RecibosImputados.AddRange([70, 71]);

        await CreateSut().ExecuteAsync(Dto(Item(901, 1, 200, 42)));

        _recibos.EstadosRecibo.Keys.Should().BeEquivalentTo([70, 71]);
        _recibos.EstadosDocumentoCliente.Should().NotContainKey(IdFactura);
    }

    [Fact]
    public async Task Create_FacturaDeRemitos_DevuelveSaldoALosRemitosYNoTocaElStock()
    {
        _ventas.RemitosAsociados.Add(new DocumentosClienteRemitos { IdDocumentoClienteRemito = 1, IdDocumentoCliente = IdFactura, IdRemito = 40, IdComprobanteTipo = 8 });

        await CreateSut().ExecuteAsync(Dto(Item(901, 1, 200, 42)));

        _ventas.Operaciones.Should().Contain("BorrarRemitoAsociado 1");
        _ventas.Operaciones.Should().NotContain(o => o.StartsWith("SumarStock"));
        _ventas.All<EntidadesCtaCteStockMovimientosDetalle>().Single().Saldo2.Should().Be(1);
    }

    [Fact]
    public async Task Create_AfipRechaza_AnulaElBorradorSinTocarLaFactura()
    {
        _afip.Responder = _ => new AfipRespuestaCae("R", null, null, 0, "10016: comprobante asociado inexistente");

        var act = () => CreateSut().ExecuteAsync(Dto(Item(901, 1, 200, 42)));

        await act.Should().ThrowAsync<UnprocessableException>().WithMessage("*comprobante asociado inexistente*");
        var nc = _ventas.Single<DocumentosCliente>();
        _ventas.Operaciones.Should().Contain($"BorrarRelacion {IdFactura}/{nc.IdDocumentoCliente}");
        _ventas.Operaciones.Should().Contain(o => o.StartsWith($"AnularDocumento {nc.IdDocumentoCliente}="));
        _ventas.All<DocumentosClienteRelacion>().Should().BeEmpty();
        _ventas.All<EntidadesCtaCte>().Should().BeEmpty();
        _ventas.Operaciones.Should().NotContain(o => o.StartsWith("SumarStock"));
        _recibos.EstadosDocumentoCliente.Should().BeEmpty();
        _ventas.Single<DocumentosClienteObservaciones>().Observaciones.Should().StartWith("RECHAZADO POR AFIP");
    }

    [Fact]
    public async Task Create_AfipNoResponde_QuedaPendienteSinEfectos()
    {
        _afip.CaidoAlPedirCae = true;

        var result = await CreateSut().ExecuteAsync(Dto(Item(901, 1, 200, 42)));

        result.Estado.Should().Be(EstadoAutorizacionAfip.PendienteAfip);
        _ventas.All<EntidadesCtaCte>().Should().BeEmpty();
        _ventas.Operaciones.Should().NotContain(o => o.StartsWith("SumarStock") || o.StartsWith("DatosAfip"));
    }

    [Fact]
    public async Task Reintento_DespuesDeCaida_AutorizaYAplicaEfectos()
    {
        _afip.CaidoAlPedirCae = true;
        await CreateSut().ExecuteAsync(Dto(Item(901, 1, 200, 42)));
        var nc = _ventas.Single<DocumentosCliente>();
        _afip.CaidoAlPedirCae = false;

        var result = await DispatcherSut().ExecuteAsync(nc.IdDocumentoCliente);

        result.Estado.Should().Be(EstadoAutorizacionAfip.Autorizada);
        _afip.Solicitudes.Last().ComprobantesAsociados.Should().ContainSingle();
        _ventas.Single<EntidadesCtaCte>().Saldo.Should().Be(-242);
    }

    [Fact]
    public async Task Reintento_YaSeEmitioEnAfip_NoPideOtroCae()
    {
        _afip.CaidoAlPedirCae = true;
        await CreateSut().ExecuteAsync(Dto(Item(901, 1, 200, 42)));
        var nc = _ventas.Single<DocumentosCliente>();
        _afip.CaidoAlPedirCae = false;
        _afip.UltimoAutorizado = 123; // AFIP llegó a emitir el número esperado

        var act = () => DispatcherSut().ExecuteAsync(nc.IdDocumentoCliente);

        await act.Should().ThrowAsync<ConflictException>();
        _afip.Solicitudes.Should().HaveCount(1);
    }

    // ---------- Validaciones ----------

    [Fact]
    public async Task Create_FacturaSinCae_Rechaza()
    {
        _ventas.Documentos[IdFactura].Cae = "0";

        var act = () => CreateSut().ExecuteAsync(Dto(Item(901, 1, 200, 42)));

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*no está autorizada*");
        _ventas.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_NoEsFactura_Rechaza()
    {
        _ventas.Documentos[IdFactura].IdComprobanteTipo = R.Ven;

        var act = () => CreateSut().ExecuteAsync(Dto(Item(901, 1, 200, 42)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no es una factura electrónica*");
    }

    [Fact]
    public async Task Create_LineaDeOtraFactura_Rechaza()
    {
        var act = () => CreateSut().ExecuteAsync(Dto(Item(999, 1, 200, 42)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no pertenece a la factura*");
        _ventas.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_MasCantidadQueLaFacturada_Rechaza()
    {
        var act = () => CreateSut().ExecuteAsync(Dto(Item(902, 2, 100, 10.5m), Item(902, 1, 50, 5.25m)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*TUERCA*");
    }

    [Fact]
    public async Task Create_SuperaLoQueQuedaPorAcreditar_Rechaza()
    {
        _ventas.TotalNotasCreditoPrevias = 1000;

        var act = () => CreateSut().ExecuteAsync(Dto(Item(901, 2, 400, 84)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*supera el saldo acreditable*");
        _afip.Solicitudes.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AfipCaidoAntesDeGrabar_NoGrabaNada()
    {
        _afip.CaidoAlConsultarNumero = true;

        var act = () => CreateSut().ExecuteAsync(Dto(Item(901, 1, 200, 42)));

        await act.Should().ThrowAsync<ServiceUnavailableException>();
        _ventas.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Iniciar_DevuelvePuntoDeVentaYProximoNumeroDeLaNc()
    {
        var sut = new IniciarNotaCreditoUseCase(_ventas, _recibos, _ref, _afip, _user.Object);

        var r = await sut.ExecuteAsync(IdFactura);

        r.Should().Match<NuevaFacturaElectronicaDisplay>(x => x.Letra == "A" && x.CbteTipo == 3 && x.PuntoVenta == "0002" && x.NumeroSugerido == "00000123");
    }

    // ---------- helpers ----------

    private AutorizarNotaCreditoUseCase AutorizarSut() =>
        new(_ventas, _recibos, new VentaAnulador(_ventas, _recibos, _ref), _ref, _afip, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object,
            NullLogger<AutorizarNotaCreditoUseCase>.Instance);

    private CreateNotaCreditoUseCase CreateSut() =>
        new(_ventas, _recibos, _ref, _afip, AutorizarSut(), new InlineUnitOfWork(), _user.Object);

    private AutorizarComprobanteElectronicoUseCase DispatcherSut()
    {
        var factura = new Mock<IAutorizarFacturaElectronicaUseCase>(MockBehavior.Strict);
        return new AutorizarComprobanteElectronicoUseCase(_ventas, _ref, factura.Object, AutorizarSut());
    }

    private static CreateNotaCreditoDto Dto(params NotaCreditoItemDto[] items) => new()
    {
        IdFactura = IdFactura,
        FechaEmision = Fecha,
        Neto = items.Sum(i => i.PrecioNeto),
        Iva = items.Sum(i => i.Iva),
        Total = items.Sum(i => i.Total),
        Items = [.. items],
    };

    private static NotaCreditoItemDto Item(long idLinea, decimal cantidad, decimal neto, decimal iva) => new()
    {
        IdDocumentoClienteDetalle = idLinea,
        Cantidad = cantidad,
        PrecioUnitario = (neto + iva) / cantidad,
        PrecioNeto = neto,
        Iva = iva,
        IvaAlicuota = Math.Round(iva * 100 / neto, 1),
        Total = neto + iva,
    };
}
