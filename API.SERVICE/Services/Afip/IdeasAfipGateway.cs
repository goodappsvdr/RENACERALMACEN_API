using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using API.SERVICE.Interfaces.Afip;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.SERVICE.Services.Afip;

/// <summary>Sección "AfipGateway" de configuración. La contraseña del certificado va en user-secrets / variables de entorno.</summary>
public sealed class AfipGatewayOptions
{
    public const string SectionName = "AfipGateway";

    /// <summary>URL base del gateway (el ERP usa http://ideassa.com.ar/AFIP_GA_API_48/api).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Contraseña del certificado. En el ERP está escrita en el código; acá solo por configuración secreta.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>false = homologación de AFIP.</summary>
    public bool IsProdEnvironment { get; set; }

    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Cliente del gateway AFIP de IDEAS SA. Mismos endpoints y forma de JSON que API_GA_AFIP.vb
/// (CheckCompNumber, GenerateVoucher, GenerateVoucherMono). Nunca loguea el request: lleva la contraseña.
/// </summary>
public sealed class IdeasAfipGateway : IAfipGateway
{
    // PascalCase como el JavaScriptSerializer del ERP; el gateway es .NET y no distingue mayúsculas.
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = null };

    private readonly HttpClient _http;
    private readonly AfipGatewayOptions _options;
    private readonly ILogger<IdeasAfipGateway> _logger;

    public IdeasAfipGateway(HttpClient http, IOptions<AfipGatewayOptions> options, ILogger<IdeasAfipGateway> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<long> GetUltimoAutorizadoAsync(AfipEmisor emisor, int cbteTipo, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            LoginCmsModel = Login(emisor),
            WsfeClientModel = new { emisor.Cuit, PtoVta = emisor.PuntoVenta, CbteTipo = cbteTipo, CantReg = 1 },
        };

        var json = await PostAsync("CheckCompNumber", body, cancellationToken);
        var nro = json?["Body"]?["FECompUltimoAutorizadoResult"]?["CbteNro"];
        return nro is not null && long.TryParse(nro.ToString(), out var numero)
            ? numero
            : throw new AfipNoDisponibleException("AFIP no devolvió el último número autorizado.");
    }

    public async Task<AfipRespuestaCae> SolicitarCaeAsync(AfipSolicitudCae s, CancellationToken cancellationToken = default)
    {
        var caeDet = new
        {
            CbteFch = s.CbteFecha,
            s.Concepto,
            s.DocNro,
            s.DocTipo,
            FchVtoPago = s.FechaVtoPago,
            s.ImpNeto,
            s.ImpTotConc,
            s.ImpOpEx,
            s.ImpTrib,
            ImpIVA = s.ImpIva,
            s.ImpTotal,
            FchServDesde = s.FechaServDesde,
            FchServHasta = s.FechaServHasta,
            MonCotiz = 1,
            MonId = "PES",
        };
        var cliente = new { s.Emisor.Cuit, PtoVta = s.Emisor.PuntoVenta, s.CbteTipo, CantReg = 1 };

        object body = s.Monotributo
            ? new { LoginCmsModel = Login(s.Emisor), WsfeClientModel = cliente, CaeDetRequestModel = caeDet }
            : new
            {
                LoginCmsModel = Login(s.Emisor),
                WsfeClientModel = cliente,
                CaeDetRequestModel = caeDet,
                AlicIvas = s.Alicuotas.Select(a => new { a.Id, BaseImp = a.BaseImponible, a.Importe }).ToList(),
            };

        var json = await PostAsync(s.Monotributo ? "GenerateVoucherMono" : "GenerateVoucher", body, cancellationToken);
        var det = json?["FeDetRespModel"]
            ?? throw new AfipNoDisponibleException("La respuesta de AFIP no tiene el detalle del comprobante.");

        return new AfipRespuestaCae(
            Resultado: det["Resultado"]?.ToString() ?? string.Empty,
            Cae: det["CAE"]?.ToString(),
            CaeVencimiento: det["CAEFchVto"]?.ToString(),
            Numero: long.TryParse(det["CbteDesde"]?.ToString(), out var n) ? n : 0,
            Observaciones: det["Observaciones"]?.ToString());
    }

    private object Login(AfipEmisor emisor) => new
    {
        _options.IsProdEnvironment,
        Service = "wsfe",
        _options.Password,
        Verbose = true,
        FolderFile = emisor.CarpetaCertificado,
        FileName = emisor.ArchivoCertificado,
    };

    private async Task<JsonNode?> PostAsync(string endpoint, object body, CancellationToken cancellationToken)
    {
        if (_http.BaseAddress is null)
            throw new AfipNoDisponibleException("El gateway de AFIP no está configurado (AfipGateway:BaseUrl).");

        try
        {
            using var response = await _http.PostAsJsonAsync(endpoint, body, Json, cancellationToken);
            var contenido = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gateway AFIP {Endpoint} respondió {Status}", endpoint, (int)response.StatusCode);
                throw new AfipNoDisponibleException($"El servicio de AFIP respondió con error ({(int)response.StatusCode}).");
            }

            return JsonNode.Parse(contenido);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Gateway AFIP {Endpoint} no disponible", endpoint);
            throw new AfipNoDisponibleException("No se pudo comunicar con AFIP.", ex);
        }
    }
}
