using API.SERVICE.Domain.Afip;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces.Afip;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Ventas;

/// <summary>Pasos comunes a la autorización de facturas y notas de crédito electrónicas.</summary>
internal static class ComprobanteElectronicoAfip
{
    /// <summary>
    /// Reintento: si el último número autorizado en AFIP ya alcanzó el que se esperaba para el comprobante, el intento
    /// anterior pudo haberse emitido aunque no llegó la respuesta. Pedir otro CAE generaría un duplicado en AFIP.
    /// </summary>
    public static async Task VerificarQueNoSeEmitioAsync(IAfipGateway afip, Db.DocumentosCliente doc, EmisorFactura emisor, CancellationToken ct)
    {
        long ultimo;
        try
        {
            ultimo = await afip.GetUltimoAutorizadoAsync(emisor.Afip, emisor.CbteTipo, ct);
        }
        catch (AfipNoDisponibleException)
        {
            throw new ServiceUnavailableException("AFIP no responde; no se puede verificar el comprobante para reintentar. Probar más tarde.");
        }

        if (long.TryParse(doc.Numero, out var esperado) && ultimo >= esperado)
        {
            throw new ConflictException(
                $"AFIP ya tiene autorizado hasta el número {ultimo} y este comprobante esperaba el {esperado}: el intento anterior puede " +
                "haberse emitido. Verificar en AFIP antes de reintentar para no duplicarlo.");
        }
    }

    /// <summary>QR de AFIP (imagen PNG en base64), si está configurado AFIP/URL.</summary>
    public static async Task AgregarQrAsync(
        IVentaRepository ventas, IReferenciasRepository referencias, Db.DocumentosCliente doc, EmisorFactura emisor,
        long numero, string cae, int docTipo, long docNro, CancellationToken ct)
    {
        var urlQr = await referencias.GetParametroAsync("AFIP", "URL", ct);
        if (string.IsNullOrWhiteSpace(urlQr))
            return;

        var url = AfipRules.UrlQr(urlQr, doc.FechaEmision!.Value, emisor.Afip.Cuit, emisor.Afip.PuntoVenta, emisor.CbteTipo, numero,
            doc.TotalGeneral ?? 0m, docTipo, docNro, cae);
        ventas.Add(new Db.DocumentosClienteQr { IdDocumentoCliente = doc.IdDocumentoCliente, CodigoQr = AfipRules.ImagenQrBase64(url) });
    }

    /// <summary>LibroIvaVenta_Agregar + TxtVentasAlicuotas_Agregar por cada alícuota con neto (solo Responsable Inscripto).</summary>
    public static void RegistrarLibroIva(
        IVentaRepository ventas, Db.DocumentosCliente doc, int cbteTipo, string puntoVenta, string numero, IvaPorAlicuota iva, int idComprobanteTipo)
    {
        var fecha = doc.FechaEmision!.Value;
        ventas.Add(new Db.LibroIvaVenta
        {
            FechaEmision = DateOnly.FromDateTime(fecha),
            TipoComprobante = cbteTipo.ToString(),
            Letra = doc.Letra,
            PuntoVenta = puntoVenta,
            Numero = numero,
            RazonSocial = VentaRules.Truncar(doc.RazonSocial, 50),
            NroDocumento = doc.NroDoc,
            TotalGeneral = doc.TotalGeneral,
            TotalNeto = doc.TotalNeto,
            TotalIva = doc.TotalIva,
            Neto21 = iva.Neto21,
            Neto10 = iva.Neto105,
            Neto27 = iva.Neto27,
            NetoExento = iva.NetoExento,
            Iva21 = iva.Iva21,
            Iva10 = iva.Iva105,
            Iva27 = iva.Iva27,
            IvaExcento = iva.IvaExento,
            IngBruto = 0,
            Percepciones = 0,
            ImpNacional = 0,
            ImpMunicipal = 0,
            ImpInterno = 0,
            OtrosTributo = 0,
            Mes = fecha.Month,
            Anio = fecha.Year,
            IdComprobante = doc.IdDocumentoCliente,
            IdComprobanteTipo = idComprobanteTipo,
        });

        void Txt(decimal neto, string alicuota, decimal importe)
        {
            if (neto <= 0) return;
            ventas.Add(new Db.TxtVentasAlicuotas
            {
                TipoComprobante = cbteTipo.ToString().PadLeft(3, '0'),
                PuntoVenta = puntoVenta.PadLeft(5, '0'),
                NroComprobante = numero.PadLeft(20, '0'),
                ImporteNeto = neto,
                Alicuota = alicuota,
                ImporteLiquidado = importe,
                Mes = fecha.Month,
                Anio = fecha.Year,
                FechaAlta = DateOnly.FromDateTime(fecha),
                IdComprobante = doc.IdDocumentoCliente,
                IdComprobanteTipo = idComprobanteTipo,
            });
        }

        Txt(iva.Neto21, "0005", iva.Iva21);
        Txt(iva.Neto105, "0004", iva.Iva105);
        Txt(iva.Neto27, "0006", iva.Iva27);
        Txt(iva.NetoExento, "0003", iva.IvaExento);
    }
}
