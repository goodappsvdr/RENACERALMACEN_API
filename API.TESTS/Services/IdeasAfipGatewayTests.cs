using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using API.SERVICE.Interfaces.Afip;
using API.SERVICE.Services.Afip;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace API.TESTS.Services;

/// <summary>Forma del JSON que se envía al gateway de IDEAS SA (igual que API_GA_AFIP.vb) y lectura de sus respuestas.</summary>
public class IdeasAfipGatewayTests
{
    private static readonly AfipEmisor Emisor = new(30712345678, 2, "certificados", "elrenacer.pfx");

    [Fact]
    public async Task GetUltimoAutorizado_EnviaLoginYClienteYLeeCbteNro()
    {
        var handler = new Handler("""{ "Body": { "FECompUltimoAutorizadoResult": { "CbteNro": 122 } } }""");

        var numero = await Sut(handler).GetUltimoAutorizadoAsync(Emisor, 6);

        numero.Should().Be(122);
        handler.Path.Should().Be("/api/CheckCompNumber");
        var json = handler.Body!;
        json["LoginCmsModel"]!["Service"]!.GetValue<string>().Should().Be("wsfe");
        json["LoginCmsModel"]!["Password"]!.GetValue<string>().Should().Be("secreta");
        json["LoginCmsModel"]!["IsProdEnvironment"]!.GetValue<bool>().Should().BeFalse();
        json["LoginCmsModel"]!["FileName"]!.GetValue<string>().Should().Be("elrenacer.pfx");
        json["WsfeClientModel"]!["Cuit"]!.GetValue<long>().Should().Be(30712345678);
        json["WsfeClientModel"]!["CbteTipo"]!.GetValue<int>().Should().Be(6);
    }

    [Fact]
    public async Task SolicitarCae_ResponsableInscripto_EnviaAlicuotasYLeeLaRespuesta()
    {
        var handler = new Handler("""
            { "FeCabRespModel": { "Resultado": "A" },
              "FeDetRespModel": { "CAE": "71234567890123", "CAEFchVto": "20261016", "CbteDesde": 123, "Resultado": "A", "Observaciones": "" } }
            """);

        var respuesta = await Sut(handler).SolicitarCaeAsync(Solicitud(monotributo: false));

        respuesta.Should().Be(new AfipRespuestaCae("A", "71234567890123", "20261016", 123, ""));
        respuesta.Aprobado.Should().BeTrue();
        handler.Path.Should().Be("/api/GenerateVoucher");
        var json = handler.Body!;
        json["CaeDetRequestModel"]!["ImpIVA"]!.GetValue<decimal>().Should().Be(210);
        json["CaeDetRequestModel"]!["MonId"]!.GetValue<string>().Should().Be("PES");
        json["AlicIvas"]!.AsArray().Should().ContainSingle();
        json["AlicIvas"]![0]!["Id"]!.GetValue<int>().Should().Be(5);
        json["AlicIvas"]![0]!["BaseImp"]!.GetValue<decimal>().Should().Be(1000);
    }

    [Fact]
    public async Task SolicitarCae_Monotributo_VaAlEndpointMonoYSinAlicuotas()
    {
        var handler = new Handler("""{ "FeDetRespModel": { "CAE": "", "Resultado": "R", "Observaciones": "10015" } }""");

        var respuesta = await Sut(handler).SolicitarCaeAsync(Solicitud(monotributo: true));

        respuesta.Aprobado.Should().BeFalse();
        respuesta.Observaciones.Should().Be("10015");
        handler.Path.Should().Be("/api/GenerateVoucherMono");
        handler.Body!["AlicIvas"].Should().BeNull();
    }

    [Theory]
    [InlineData(false, "/api/GenerateVoucherCbteAsoc")]
    [InlineData(true, "/api/GenerateVoucherCbteAsocMono")]
    public async Task SolicitarCae_ConComprobanteAsociado_VaAlEndpointCbteAsocYLoEnvia(bool monotributo, string endpoint)
    {
        var handler = new Handler("""{ "FeDetRespModel": { "CAE": "71234567890123", "CbteDesde": 9, "Resultado": "A" } }""");

        await Sut(handler).SolicitarCaeAsync(Solicitud(monotributo) with { ComprobantesAsociados = [new AfipComprobanteAsociado(1, 2, 77)] });

        handler.Path.Should().Be(endpoint);
        var asociado = handler.Body!["CbteAsocs"]!.AsArray().Single()!;
        asociado["Tipo"]!.GetValue<int>().Should().Be(1);
        asociado["PtoVta"]!.GetValue<int>().Should().Be(2);
        asociado["Nro"]!.GetValue<long>().Should().Be(77);
        (handler.Body!["AlicIvas"] is null).Should().Be(monotributo);
    }

    [Fact]
    public async Task SolicitarCae_SinAsociados_NoEnviaCbteAsocs()
    {
        var handler = new Handler("""{ "FeDetRespModel": { "CAE": "71234567890123", "CbteDesde": 9, "Resultado": "A" } }""");

        await Sut(handler).SolicitarCaeAsync(Solicitud(false));

        handler.Body!["CbteAsocs"].Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "esto no es json")]
    [InlineData(HttpStatusCode.OK, """{ "otra": "cosa" }""")]
    public async Task ErroresDelGateway_SonAfipNoDisponible(HttpStatusCode status, string contenido)
    {
        var act = () => Sut(new Handler(contenido, status)).SolicitarCaeAsync(Solicitud(false));

        await act.Should().ThrowAsync<AfipNoDisponibleException>();
    }

    [Fact]
    public async Task SinRed_EsAfipNoDisponible()
    {
        var act = () => Sut(new Handler(error: new HttpRequestException("sin red"))).GetUltimoAutorizadoAsync(Emisor, 6);

        await act.Should().ThrowAsync<AfipNoDisponibleException>();
    }

    [Fact]
    public async Task SinBaseUrl_EsAfipNoDisponible()
    {
        var gateway = new IdeasAfipGateway(new HttpClient(new Handler("{}")), Options.Create(new AfipGatewayOptions()), NullLogger<IdeasAfipGateway>.Instance);

        var act = () => gateway.GetUltimoAutorizadoAsync(Emisor, 6);

        await act.Should().ThrowAsync<AfipNoDisponibleException>().WithMessage("*no está configurado*");
    }

    private static IdeasAfipGateway Sut(Handler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://gateway.test/api/") },
            Options.Create(new AfipGatewayOptions { Password = "secreta", IsProdEnvironment = false }),
            NullLogger<IdeasAfipGateway>.Instance);

    private static AfipSolicitudCae Solicitud(bool monotributo) => new(
        Emisor, 1, monotributo, new DateTime(2026, 10, 6), 1, 20123456789, 80, new DateTime(2026, 10, 21),
        ImpNeto: 1000, ImpTotConc: 0, ImpOpEx: 0, ImpTrib: 0, ImpIva: 210, ImpTotal: 1210,
        FechaServDesde: new DateTime(2026, 10, 6), FechaServHasta: new DateTime(2026, 10, 6),
        Alicuotas: monotributo ? [] : [new AfipAlicuota(5, 1000, 210)]);

    private sealed class Handler(string respuesta = "{}", HttpStatusCode status = HttpStatusCode.OK, Exception? error = null) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public JsonNode? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath;
            Body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (error is not null) throw error;
            return new HttpResponseMessage(status) { Content = new StringContent(respuesta, Encoding.UTF8, "application/json") };
        }
    }
}
