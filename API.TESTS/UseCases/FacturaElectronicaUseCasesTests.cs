using System.Text;
using System.Text.Json;
using API.DA.Entities;
using API.SERVICE.Domain.Afip;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Afip;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Clientes;
using API.SERVICE.UseCases.Ventas;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using R = API.TESTS.Infrastructure.FakeReferencias;

namespace API.TESTS.UseCases;

/// <summary>Factura electrónica en dos fases (FrmFacturasAFIP), con AFIP simulado.</summary>
public class FacturaElectronicaUseCasesTests
{
    private static readonly DateTime Ahora = new(2026, 10, 6, 18, 0, 0);
    private static readonly DateTime Fecha = new(2026, 10, 6);

    private readonly FakeReciboCobroRepository _recibos = new();
    private readonly FakeVentaRepository _ventas;
    private readonly FakeReferencias _ref = new();
    private readonly FakeAfipGateway _afip = new();
    private readonly Mock<ICurrentUser> _user = new();

    public FacturaElectronicaUseCasesTests()
    {
        _ventas = new FakeVentaRepository(_recibos);
        _user.SetupGet(u => u.IdUsuario).Returns(3);
        _recibos.Entidades[5].CtaCte = true;
        _recibos.Entidades[5].LimiteCtaCte = 1_000_000;
        _recibos.Entidades[5].DiasInteres = 15;
        _recibos.Entidades[5].Cuit = "20123456789";
    }

    // ---------- Alta (fase 1 + fase 2) ----------

    [Fact]
    public async Task Create_Aprobada_GrabaSinCaeYDespuesRegistraCaeNumeroQrYLibroIva()
    {
        var result = await CreateSut().ExecuteAsync(Dto("A",
            Item(10, neto: 1000, iva: 210, idImpuesto: 1),
            Item(11, neto: 200, iva: 21, idImpuesto: 2)));

        result.Estado.Should().Be(EstadoAutorizacionAfip.Autorizada);

        // Fase 1: comprobante FV letra A, punto de venta AFIP de la sucursal y número esperado (último + 1)
        var doc = _ventas.Single<DocumentosCliente>();
        doc.Should().Match<DocumentosCliente>(d => d.IdComprobanteTipo == R.Fv && d.Letra == "A" && d.Estado == 42);
        _ventas.Single<EntidadesCtaCte>().Concepto.Should().Be("FV -A-0002-00000123");

        // Solicitud a AFIP (Responsable Inscripto): IVA por alícuota, documento CUIT, vencimiento por días de interés
        var s = _afip.Solicitudes.Single();
        s.Should().Match<AfipSolicitudCae>(x =>
            !x.Monotributo && x.CbteTipo == 1 && x.Emisor.Cuit == 30712345678 && x.Emisor.PuntoVenta == 2 &&
            x.DocTipo == 80 && x.DocNro == 20123456789 && x.ImpNeto == 1200 && x.ImpIva == 231 && x.ImpTotal == 1431 &&
            x.FechaVtoPago == Fecha.AddDays(15) && x.Emisor.ArchivoCertificado == "elrenacer.pfx");
        s.Alicuotas.Should().BeEquivalentTo([new AfipAlicuota(5, 1000, 210), new AfipAlicuota(4, 200, 21)]);

        // Fase 2: datos AFIP, QR y libro de IVA
        var barras = AfipRules.CodigoBarras(30712345678, 1, 2, "71234567890123", Fecha.AddDays(30).ToString("yyyyMMdd"));
        _ventas.Operaciones.Should().Contain($"DatosAfip {doc.IdDocumentoCliente} 0002-00000123 CAE=71234567890123 BAR={barras}");
        _ventas.Single<DocumentosClienteQr>().CodigoQr.Should().StartWith("iVBOR", "es un PNG en base64");
        _ventas.Single<LibroIvaVenta>().Should().Match<LibroIvaVenta>(l =>
            l.Numero == "00000123" && l.Neto21 == 1000 && l.Iva21 == 210 && l.Neto10 == 200 && l.Iva10 == 21 && l.TipoComprobante == "1");
        _ventas.All<TxtVentasAlicuotas>().Select(t => t.Alicuota).Should().Equal("0005", "0004");
        _ventas.Operaciones.Should().ContainInOrder($"BloquearAutorizacion {doc.IdDocumentoCliente}", $"LiberarAutorizacion {doc.IdDocumentoCliente}");
    }

    [Fact]
    public async Task Create_AfipAsignaOtroNumero_GanaElDeAfip()
    {
        _afip.Responder = _ => new AfipRespuestaCae("A", "71234567890123", "20261016", 125, null);

        await CreateSut().ExecuteAsync(Dto("B", Item(10, 1000, 210, 1)));

        _ventas.Operaciones.Should().Contain(o => o.Contains("0002-00000125 CAE=71234567890123"));
    }

    [Fact]
    public async Task Create_SucursalMonotributo_SinIvaNiLibro()
    {
        _ventas.Sucursales[1].IdCategoriaIva = 99;

        await CreateSut().ExecuteAsync(Dto("C", Item(10, 1000, 0, 4)));

        _afip.Solicitudes.Single().Should().Match<AfipSolicitudCae>(x =>
            x.Monotributo && x.CbteTipo == 11 && x.ImpNeto == 1000 && x.ImpIva == 0 && x.ImpTotal == 1000 && x.Alicuotas.Count == 0);
        _ventas.All<LibroIvaVenta>().Should().BeEmpty();
        _ventas.All<DocumentosClienteQr>().Should().ContainSingle();
    }

    [Fact]
    public async Task Create_AfipCaidoAntesDeGrabar_NoGrabaNada()
    {
        _afip.CaidoAlConsultarNumero = true;

        var act = () => CreateSut().ExecuteAsync(Dto("A", Item(10, 1000, 210, 1)));

        await act.Should().ThrowAsync<ServiceUnavailableException>();
        _ventas.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AfipNoRespondeAlPedirCae_QuedaPendienteSinCae()
    {
        _afip.CaidoAlPedirCae = true;

        var result = await CreateSut().ExecuteAsync(Dto("A", Item(10, 1000, 210, 1)));

        result.Estado.Should().Be(EstadoAutorizacionAfip.PendienteAfip);
        result.Mensaje.Should().Contain("/autorizar");
        _ventas.Single<DocumentosCliente>().Cae.Should().Be("0");
        _ventas.Operaciones.Should().NotContain(o => o.StartsWith("DatosAfip"));
        _ventas.All<LibroIvaVenta>().Should().BeEmpty();
    }

    [Fact]
    public async Task Create_AfipRechaza_SeRevierteLaFacturaYSeInformaElMotivo()
    {
        _afip.Responder = _ => new AfipRespuestaCae("R", null, null, 0, "10015: el CUIT del receptor es inválido");

        var act = () => CreateSut().ExecuteAsync(Dto("A", Item(10, 1000, 210, 1)));

        await act.Should().ThrowAsync<UnprocessableException>().WithMessage("*10015*se revirtieron*");
        var doc = _ventas.Single<DocumentosCliente>();
        _ventas.Operaciones.Should().Contain($"AnularDocumento {doc.IdDocumentoCliente}={_ref.Estado("DOCUMENTOSCLIENTE", "ANULADO")}")
            .And.Contain("SumarStock 10@1 1");
        _ventas.All<DocumentosClienteObservaciones>().Should().Contain(o => o.Observaciones!.StartsWith("RECHAZADO POR AFIP: 10015"));
        _ventas.All<LibroIvaVenta>().Should().BeEmpty();
    }

    [Fact]
    public async Task Create_LetraInvalida_Rechaza()
    {
        var act = () => CreateSut().ExecuteAsync(Dto("X", Item(10, 1000, 210, 1)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*letra X*");
    }

    // ---------- Reintento ----------

    [Fact]
    public async Task Reintento_SiAfipYaAutorizoElNumeroEsperado_NoPideOtroCae()
    {
        Pendiente(800, numero: "00000123");
        _afip.UltimoAutorizado = 123; // el intento anterior pudo haberse emitido

        var act = () => AutorizarSut().ExecuteAsync(800, reintento: true);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*Verificar en AFIP*");
        _afip.Solicitudes.Should().BeEmpty();
    }

    [Fact]
    public async Task Reintento_SinRiesgoDeDuplicado_Autoriza()
    {
        Pendiente(800, numero: "00000123");
        _afip.UltimoAutorizado = 122;

        var result = await AutorizarSut().ExecuteAsync(800, reintento: true);

        result.Should().Match<AutorizacionAfipResultado>(r => r.Estado == EstadoAutorizacionAfip.Autorizada && r.Numero == "00000123");
    }

    [Theory]
    [InlineData("71234567890123", 42, "ya está autorizada")]
    [InlineData("0", -1, "está anulada")]
    public async Task Autorizar_YaAutorizadaOAnulada_Conflict(string cae, int estado, string mensaje)
    {
        Pendiente(800, numero: "00000123");
        _ventas.Documentos[800].Cae = cae;
        _ventas.Documentos[800].Estado = estado == -1 ? _ref.Estado("DOCUMENTOSCLIENTE", "ANULADO") : estado;

        var act = () => AutorizarSut().ExecuteAsync(800, reintento: true);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{mensaje}*");
    }

    [Fact]
    public async Task Pendientes_ListaFacturasSinCae()
    {
        Pendiente(800, "00000123");
        Pendiente(801, "00000124");
        _ventas.Documentos[801].Cae = "71234567890123";

        var result = await new GetFacturasPendientesAfipUseCase(_ventas, _ref).ExecuteAsync();

        result.Should().ContainSingle().Which.IdDocumentoCliente.Should().Be(800);
    }

    // ---------- Reglas ----------

    [Fact]
    public void AgruparIva_AplicaDescuentoGlobalYElExentoNoLlevaIva()
    {
        var iva = AfipRules.AgruparIva([(1, 1000m, 210m), (3, 100m, 27m), (4, 50m, 5m)], porcentajeDescuento: 10);

        iva.Should().Be(new IvaPorAlicuota(900, 189, 0, 0, 90, 24.3m, 45, 0));
        iva.ParaAfip().Should().BeEquivalentTo([new AfipAlicuota(5, 900, 189), new AfipAlicuota(6, 90, 24.3m)]);
    }

    [Theory]
    [InlineData("01234567890", 5)] // impares (0+2+4+6+8+0)·3 = 60, pares 25 → 85 → 5
    [InlineData("1", 7)]           // 3 → 7
    [InlineData("0", 0)]
    public void DigitoVerificador_ComoElErp(string numero, int digito)
    {
        AfipRules.DigitoVerificador(numero).Should().Be(digito);
    }

    [Theory]
    [InlineData(null, "SIN IDENTIFICAR", 0L)]
    [InlineData("20123456789", "CUIT", 20123456789L)]
    [InlineData("30111222", "DNI", 30111222L)]
    public void Documento_PorLargo(string? cuit, string tipo, long numero)
    {
        AfipRules.Documento(cuit).Should().Be((tipo, numero));
    }

    [Fact]
    public void UrlQr_JsonEnBase64ConLosCamposDeAfip()
    {
        var url = AfipRules.UrlQr("https://www.afip.gob.ar/fe/qr/?p=", Fecha, 30712345678, 2, 1, 123, 1431.5m, 80, 20123456789, "71234567890123");

        var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(url["https://www.afip.gob.ar/fe/qr/?p=".Length..]))).RootElement;
        json.GetProperty("fecha").GetString().Should().Be("2026-10-06");
        json.GetProperty("nroCmp").GetInt64().Should().Be(123);
        json.GetProperty("importe").GetDecimal().Should().Be(1431.5m);
        json.GetProperty("codAut").GetInt64().Should().Be(71234567890123);
        json.GetProperty("tipoCodAut").GetString().Should().Be("E");
    }

    // ---------- helpers ----------

    private AutorizarFacturaElectronicaUseCase AutorizarSut()
    {
        var clock = new FixedServerClock(Ahora);
        return new AutorizarFacturaElectronicaUseCase(
            _ventas, _recibos, new VentaAnulador(_ventas, _recibos, _ref), _ref, _afip, new InlineUnitOfWork(), clock, _user.Object,
            NullLogger<AutorizarFacturaElectronicaUseCase>.Instance);
    }

    private CreateFacturaElectronicaUseCase CreateSut()
    {
        var clock = new FixedServerClock(Ahora);
        var writer = new VentaWriter(_ventas, new ReciboCobroWriter(_recibos, _ref, clock), _ref);
        return new CreateFacturaElectronicaUseCase(writer, _ventas, _recibos, _ref, _afip, AutorizarSut(), new InlineUnitOfWork(), clock, _user.Object);
    }

    private void Pendiente(int id, string numero) =>
        _ventas.Documentos[id] = new DocumentosCliente
        {
            IdDocumentoCliente = id, IdComprobanteTipo = R.Fv, Letra = "A", PuntoVenta = "0002", Numero = numero, Cae = "0", Estado = 42,
            IdCliente = 5, IdSucursal = 1, NroDoc = "20123456789", FechaEmision = Fecha, TotalNeto = 1000, TotalIva = 210, TotalGeneral = 1210,
        };

    private static CreateFacturaElectronicaDto Dto(string letra, params VentaItemDto[] items) => new()
    {
        Letra = letra,
        IdEntidad = 5,
        FechaEmision = Fecha,
        IdSucursal = 1,
        Neto = items.Sum(i => i.PrecioNeto),
        Iva = items.Sum(i => i.Iva),
        Total = items.Sum(i => i.Total),
        Items = [.. items],
    };

    private static VentaItemDto Item(int idItem, decimal neto, decimal iva, int idImpuesto) => new()
    {
        IdItem = idItem,
        Descripcion = "articulo",
        Cantidad = 1,
        PrecioUnitario = neto + iva,
        PrecioNeto = neto,
        Iva = iva,
        IvaAlicuota = neto == 0 ? 0 : Math.Round(iva * 100 / neto, 1),
        Total = neto + iva,
        IdImpuestoIva = idImpuesto,
    };
}
