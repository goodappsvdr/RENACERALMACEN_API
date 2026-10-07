using System.Text;
using System.Text.Json;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces.Afip;
using QRCoder;

namespace API.SERVICE.Domain.Afip;

/// <summary>Reglas de la factura electrónica, tal como las aplica FrmFacturasAFIP (Agregar_Ws y ModuloPrincipal).</summary>
public static class AfipRules
{
    /// <summary>Letras de comprobante electrónico admitidas.</summary>
    public static readonly IReadOnlySet<string> Letras = new HashSet<string> { "A", "B", "C" };

    /// <summary>Clase de comprobante: arma el nombre del parámetro AFIP/* con su código ("FACTURA A", "NC B", ...).</summary>
    public const string Factura = "FACTURA";
    public const string NotaCredito = "NC";

    /// <summary>
    /// Tipo AFIP de una factura según su letra, como lo calcula DocumentosCliente_BuscarPorID (columna TipoAfip):
    /// A → 1, C → 11, cualquier otra → 6. Se usa para el comprobante asociado de la nota de crédito.
    /// </summary>
    public static int TipoAfipFactura(string? letra) => letra switch
    {
        "A" => 1,
        "C" => 11,
        _ => 6,
    };

    /// <summary>Marca de "sin CAE" con la que el ERP crea el comprobante; hasta que AFIP lo autoriza queda así.</summary>
    public const string CaePendiente = "0";

    /// <summary>
    /// Netos e IVA por alícuota a partir del detalle (ID_ImpuestoIVA 1 = 21 %, 2 = 10,5 %, 3 = 27 %, 4 = exento),
    /// con el descuento global del comprobante (Porcentaje) aplicado a cada grupo y redondeado a 2 decimales.
    /// </summary>
    public static IvaPorAlicuota AgruparIva(IEnumerable<(int IdImpuestoIva, decimal Neto, decimal Iva)> lineas, decimal porcentajeDescuento)
    {
        decimal n21 = 0, i21 = 0, n105 = 0, i105 = 0, n27 = 0, i27 = 0, nEx = 0, iEx = 0;
        foreach (var (idImpuesto, neto, iva) in lineas)
        {
            switch (idImpuesto)
            {
                case 1: n21 += neto; i21 += iva; break;
                case 2: n105 += neto; i105 += iva; break;
                case 3: n27 += neto; i27 += iva; break;
                case 4: nEx += neto; iEx += iva; break;
            }
        }

        decimal Desc(decimal v) => Math.Round(v - v * porcentajeDescuento / 100m, 2, MidpointRounding.AwayFromZero);

        if (n21 > 0) { n21 = Desc(n21); i21 = Desc(i21); }
        if (n105 > 0) { n105 = Desc(n105); i105 = Desc(i105); }
        if (n27 > 0) { n27 = Desc(n27); i27 = Desc(i27); }
        // El ERP ponía en 0 el IVA del 27 % cuando había exento (bug); lo que corresponde es que el exento no lleve IVA.
        if (nEx > 0) { nEx = Desc(nEx); iEx = 0; }

        return new IvaPorAlicuota(n21, i21, n105, i105, n27, i27, nEx, iEx);
    }

    /// <summary>Tipo y número de documento del cliente: vacío → sin identificar; 11 dígitos → CUIT; si no → DNI.</summary>
    public static (string ParametroDocTipo, long DocNro) Documento(string? cuit)
    {
        var limpio = (cuit ?? string.Empty).Trim();
        if (limpio.Length == 0)
            return ("SIN IDENTIFICAR", 0);

        if (!long.TryParse(limpio.Replace("-", string.Empty), out var numero))
            throw new BusinessException($"El documento del cliente ({limpio}) no es numérico; AFIP no lo acepta.");

        return (limpio.Length == 11 ? "CUIT" : "DNI", numero);
    }

    /// <summary>Código de barras del comprobante: CUIT + tipo + punto de venta + CAE + vencimiento + dígito verificador.</summary>
    public static string CodigoBarras(long cuitEmpresa, int tipoComprobante, int puntoVenta, string cae, string vencimientoYyyyMmDd)
    {
        var baseCodigo = $"{cuitEmpresa}{tipoComprobante}{puntoVenta}{cae}{vencimientoYyyyMmDd}";
        return baseCodigo + DigitoVerificador(baseCodigo);
    }

    /// <summary>Dígito verificador (verificarDigito del ERP): impares × 3 + pares, hasta la decena siguiente.</summary>
    public static int DigitoVerificador(string numero)
    {
        int impares = 0, pares = 0;
        for (var i = 0; i < numero.Length; i++)
        {
            var d = numero[i] - '0';
            if (i % 2 == 0) impares += d; else pares += d; // posiciones 1, 3, 5... son impares (base 1)
        }

        var total = impares * 3 + pares;
        return (10 - total % 10) % 10;
    }

    /// <summary>URL del QR de AFIP: url base + JSON del comprobante en base64 (CodigoQR del ERP).</summary>
    public static string UrlQr(string urlBase, DateTime fecha, long cuit, int puntoVenta, int tipoComprobante, long numero,
        decimal importe, int tipoDocReceptor, long nroDocReceptor, string cae)
    {
        var datos = new
        {
            ver = 1,
            fecha = fecha.ToString("yyyy-MM-dd"),
            cuit,
            ptoVta = puntoVenta,
            tipoCmp = tipoComprobante,
            nroCmp = numero,
            importe,
            moneda = "PES",
            ctz = 1,
            tipoDocRec = tipoDocReceptor,
            nroDocRec = nroDocReceptor,
            tipoCodAut = "E",
            codAut = long.TryParse(cae, out var c) ? c : 0,
        };
        return urlBase + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(datos)));
    }

    /// <summary>Imagen PNG del QR en base64, que es lo que el ERP guarda en DocumentosClienteQR.CodigoQR.</summary>
    public static string ImagenQrBase64(string url)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        return Convert.ToBase64String(new PngByteQRCode(datos).GetGraphic(3));
    }
}

public sealed record IvaPorAlicuota(
    decimal Neto21, decimal Iva21,
    decimal Neto105, decimal Iva105,
    decimal Neto27, decimal Iva27,
    decimal NetoExento, decimal IvaExento)
{
    /// <summary>Alícuotas a informar a AFIP (el exento va en ImpOpEx, no como alícuota).</summary>
    public IReadOnlyList<AfipAlicuota> ParaAfip()
    {
        var lista = new List<AfipAlicuota>();
        if (Neto21 > 0) lista.Add(new AfipAlicuota(5, Neto21, Iva21));
        if (Neto105 > 0) lista.Add(new AfipAlicuota(4, Neto105, Iva105));
        if (Neto27 > 0) lista.Add(new AfipAlicuota(6, Neto27, Iva27));
        return lista;
    }
}
