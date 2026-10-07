using API.SERVICE.Interfaces.Sistema;

namespace API.SERVICE.Domain.Cobranzas;

/// <summary>
/// Tipos de comprobante y elementos de cobro que el ERP resuelve por Parametros (COMPROBANTE/*, ELEMENTO/*).
/// Un parámetro que falta no rompe el recibo: simplemente ningún comprobante/elemento coincide con él
/// (el ERP fallaba recién al llegar a ese caso). Solo COMPROBANTE/REC es obligatorio.
/// </summary>
public sealed record CodigosCobranza(
    int Rec,
    int? Ven,
    int? Fv,
    int? Op,
    int? Nc,
    int? Fc,
    int? Com,
    int? Efectivo,
    int? Cheque,
    int? DepositoBancario,
    int? Tarjetas,
    int? Retencion)
{
    public static async Task<CodigosCobranza> LoadAsync(IReferenciasRepository r, CancellationToken ct)
    {
        async Task<int?> Opcional(string categoria, string nombre) =>
            int.TryParse((await r.GetParametroAsync(categoria, nombre, ct))?.Trim(), out var v) ? v : null;

        return new CodigosCobranza(
            Rec: await r.GetParametroEnteroAsync("COMPROBANTE", "REC", ct),
            Ven: await Opcional("COMPROBANTE", "VEN"),
            Fv: await Opcional("COMPROBANTE", "FV"),
            Op: await Opcional("COMPROBANTE", "OP"),
            Nc: await Opcional("COMPROBANTE", "NC"),
            Fc: await Opcional("COMPROBANTE", "FC"),
            Com: await Opcional("COMPROBANTE", "COM"),
            Efectivo: await Opcional("ELEMENTO", "EFECTIVO"),
            Cheque: await Opcional("ELEMENTO", "CHEQUE"),
            DepositoBancario: await Opcional("ELEMENTO", "DEPOSITO BANCARIO"),
            Tarjetas: await Opcional("ELEMENTO", "TARJETAS"),
            Retencion: await Opcional("ELEMENTO", "RETENCION"));
    }

    /// <summary>Clasifica el comprobante imputado en el mismo orden que el Select Case del ERP.</summary>
    public TipoImputacion Clasificar(int idComprobanteTipo)
    {
        if (idComprobanteTipo == Ven) return TipoImputacion.Venta;
        if (idComprobanteTipo == Fv) return TipoImputacion.Venta;
        if (idComprobanteTipo == Rec) return TipoImputacion.Recibo;
        if (idComprobanteTipo == Op) return TipoImputacion.OrdenPago;
        if (idComprobanteTipo == Nc) return TipoImputacion.NotaCredito;
        if (idComprobanteTipo == Fc) return TipoImputacion.Compra;
        if (idComprobanteTipo == Com) return TipoImputacion.Compra;
        return TipoImputacion.NoSoportado;
    }

    public ElementoCobro ClasificarElemento(int idElementoCobro)
    {
        if (idElementoCobro == Efectivo) return ElementoCobro.Efectivo;
        if (idElementoCobro == Cheque) return ElementoCobro.Cheque;
        if (idElementoCobro == DepositoBancario) return ElementoCobro.DepositoBancario;
        if (idElementoCobro == Tarjetas) return ElementoCobro.Tarjeta;
        if (idElementoCobro == Retencion) return ElementoCobro.Retencion;
        return ElementoCobro.Otro;
    }
}

public enum TipoImputacion
{
    /// <summary>VEN / FV: admite cobro parcial; estados COBRADO / COBRADO PARCIAL.</summary>
    Venta,

    /// <summary>FC / COM: admite cobro parcial; estados PAGADO / PAGADO PARCIAL.</summary>
    Compra,

    /// <summary>REC: recibo anterior (saldo a favor); se cancela entero y queda RELACIONADO.</summary>
    Recibo,

    /// <summary>OP: orden de pago; se cancela entera, se imputa con signo negativo y queda RELACIONADA.</summary>
    OrdenPago,

    /// <summary>NC: nota de crédito; se cancela entera y queda COBRADO.</summary>
    NotaCredito,

    NoSoportado,
}

public enum ElementoCobro
{
    Efectivo,
    Cheque,
    DepositoBancario,
    Tarjeta,
    Retencion,

    /// <summary>Elemento sin tratamiento especial: solo va al detalle del recibo y a la cta. cte.</summary>
    Otro,
}

/// <summary>Estados que usa la cobranza (categoría, nombre), resueltos contra la tabla Estados al momento de usarlos.</summary>
public static class EstadosCobranza
{
    public static readonly (string Categoria, string Nombre) ReciboAnulado = ("RECIBOSCOBRO", "ANULADO");
    public static readonly (string Categoria, string Nombre) ReciboRelacionado = ("RECIBOSCOBRO", "RELACIONADO");
    public static readonly (string Categoria, string Nombre) ReciboGenerado = ("RECIBOSCOBRO", "GENERADO");
    public static readonly (string Categoria, string Nombre) DocumentoCobrado = ("DOCUMENTOSCLIENTE", "COBRADO");
    public static readonly (string Categoria, string Nombre) DocumentoCobradoParcial = ("DOCUMENTOSCLIENTE", "COBRADO PARCIAL");
    public static readonly (string Categoria, string Nombre) DocumentoPagado = ("DOCUMENTOSCLIENTE", "PAGADO");
    public static readonly (string Categoria, string Nombre) DocumentoPagadoParcial = ("DOCUMENTOSCLIENTE", "PAGADO PARCIAL");
    public static readonly (string Categoria, string Nombre) DocumentoCancelado = ("DOCUMENTOSCLIENTE", "CANCELADO");
    public static readonly (string Categoria, string Nombre) DocumentoGenerado = ("DOCUMENTOSCLIENTE", "GENERADO");
    public static readonly (string Categoria, string Nombre) DocumentoProveedorGenerado = ("DOCUMENTOSPROVEEDOR", "GENERADO");
    public static readonly (string Categoria, string Nombre) OrdenPagoRelacionada = ("ORDENESPAGO", "RELACIONADA");
    public static readonly (string Categoria, string Nombre) OrdenPagoGenerada = ("ORDENESPAGO", "GENERADO");
    public static readonly (string Categoria, string Nombre) CtaCteGenerado = ("CTACTE", "GENERADO");
    public static readonly (string Categoria, string Nombre) CtaCteAnulado = ("CTACTE", "ANULADO");
    public static readonly (string Categoria, string Nombre) ChequeEnCartera = ("CLIENTECHEQUE", "EN CARTERA");
    public static readonly (string Categoria, string Nombre) ChequeAnulado = ("CLIENTECHEQUE", "ANULADO");
    public static readonly (string Categoria, string Nombre) MovimientoBancoActivo = ("BANCOCUENTAMOVIMIENTO", "ACTIVA");
    public static readonly (string Categoria, string Nombre) MovimientoBancoAnulado = ("BANCOCUENTAMOVIMIENTO", "ANULADO");
    public static readonly (string Categoria, string Nombre) RetencionGenerada = ("RETENCIONES", "GENERADA");
    public static readonly (string Categoria, string Nombre) RetencionAnulada = ("RETENCIONES", "ANULADA");
    public static readonly (string Categoria, string Nombre) PlanillaAbierta = ("CAJASPLANILLA", "ABIERTA");

    public static Task<int> IdAsync(this IReferenciasRepository r, (string Categoria, string Nombre) estado, CancellationToken ct) =>
        r.GetIdEstadoAsync(estado.Categoria, estado.Nombre, ct);
}
