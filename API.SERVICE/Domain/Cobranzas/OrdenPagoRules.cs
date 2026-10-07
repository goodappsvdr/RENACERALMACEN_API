using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces.Sistema;

namespace API.SERVICE.Domain.Cobranzas;

/// <summary>Cómo una orden de pago cancela lo que imputa (Select Case de Agregar_Ws en FrmOrdendePago).</summary>
public enum TipoImputacionPago
{
    /// <summary>FC / COM / NCP: admite pago parcial; estados PAGADO / PAGADO PARCIAL en DocumentosProveedor.</summary>
    Compra,

    /// <summary>VEN / FV / NC del mismo ente como cliente (compensación): se cancela entero y queda COBRADO.</summary>
    DocumentoCliente,

    /// <summary>REC: recibo con saldo a favor; se cancela entero y queda RELACIONADO.</summary>
    Recibo,

    /// <summary>OP anterior con saldo; se cancela entera y queda RELACIONADA.</summary>
    OrdenPago,

    NoSoportado,
}

public enum ElementoPago
{
    Efectivo,
    ChequeTercero,
    ChequePropio,
    DepositoBancario,
    Tarjeta,
    Retencion,
    Otro,
}

/// <summary>Tipos de comprobante y elementos que usa la orden de pago, resueltos por Parametros.</summary>
public sealed record CodigosOrdenPago(
    int Op, int Fc, int Com, int? Ncp, int Rec, int Ven, int Fv, int? Nc,
    int? Efectivo, int? Cheque, int? ChequePropio, int? DepositoBancario, int? Tarjetas, int? Retencion)
{
    public static async Task<CodigosOrdenPago> LoadAsync(IReferenciasRepository r, CancellationToken ct)
    {
        async Task<int?> Opcional(string categoria, string nombre) =>
            int.TryParse((await r.GetParametroAsync(categoria, nombre, ct))?.Trim(), out var v) ? v : null;
        Task<int> Requerido(string nombre) => r.GetParametroEnteroAsync("COMPROBANTE", nombre, ct);

        return new CodigosOrdenPago(
            await Requerido("OP"), await Requerido("FC"), await Requerido("COM"), await Opcional("COMPROBANTE", "NCP"),
            await Requerido("REC"), await Requerido("VEN"), await Requerido("FV"), await Opcional("COMPROBANTE", "NC"),
            await Opcional("ELEMENTO", "EFECTIVO"), await Opcional("ELEMENTO", "CHEQUE"), await Opcional("ELEMENTO", "CHEQUE PROPIO"),
            await Opcional("ELEMENTO", "DEPOSITO BANCARIO"), await Opcional("ELEMENTO", "TARJETAS"), await Opcional("ELEMENTO", "RETENCION"));
    }

    public TipoImputacionPago Clasificar(int tipo)
    {
        if (tipo == Fc || tipo == Com || tipo == Ncp) return TipoImputacionPago.Compra;
        if (tipo == Ven || tipo == Fv || tipo == Nc) return TipoImputacionPago.DocumentoCliente;
        if (tipo == Rec) return TipoImputacionPago.Recibo;
        if (tipo == Op) return TipoImputacionPago.OrdenPago;
        return TipoImputacionPago.NoSoportado;
    }

    public ElementoPago ClasificarElemento(int idElemento)
    {
        if (idElemento == Efectivo) return ElementoPago.Efectivo;
        if (idElemento == Cheque) return ElementoPago.ChequeTercero;
        if (idElemento == ChequePropio) return ElementoPago.ChequePropio;
        if (idElemento == DepositoBancario) return ElementoPago.DepositoBancario;
        if (idElemento == Tarjetas) return ElementoPago.Tarjeta;
        if (idElemento == Retencion) return ElementoPago.Retencion;
        return ElementoPago.Otro;
    }

    /// <summary>
    /// Saldo como lo muestra la pantalla de OP (EntidadesCtaCte_BuscarPorID_Entidad_OrdenPago): REC, FV, VEN y NC con el signo invertido.
    /// </summary>
    public decimal SaldoVisible(int tipo, decimal saldo) =>
        tipo == Rec || tipo == Fv || tipo == Ven || tipo == Nc ? -saldo : saldo;

    public int[] TiposInvertidos => new[] { Rec, Fv, Ven, Nc ?? 0 };
}

public static class OrdenPagoRules
{
    /// <summary>Aplica el saldo disponible de la orden de pago a un comprobante.</summary>
    /// <param name="importeComprobante">Saldo visible del comprobante.</param>
    /// <param name="saldoOrden">Importe de la orden que todavía no se imputó.</param>
    public static ResultadoImputacion Imputar(TipoImputacionPago tipo, decimal importeComprobante, decimal saldoOrden)
    {
        switch (tipo)
        {
            case TipoImputacionPago.Compra:
                if (saldoOrden >= importeComprobante)
                    return new(0, true, importeComprobante, 0, false, ImputacionRules.Redondear(saldoOrden - importeComprobante));

                // El ERP con saldo 0 igual cancelaba la factura y la marcaba PAGADA sin haberle aplicado nada.
                if (saldoOrden <= 0)
                    throw new BusinessException("El importe de la orden de pago no alcanza para pagar todos los comprobantes seleccionados.");

                return new(importeComprobante - saldoOrden, false, saldoOrden, importeComprobante - saldoOrden, true, 0);

            case TipoImputacionPago.DocumentoCliente:
            case TipoImputacionPago.Recibo:
                // Se registra con el importe invertido (EntidadOrdenPagoDocumentosProveedores.ImporteOrdenPago).
                return new(0, true, -importeComprobante, 0, false, ImputacionRules.Redondear(saldoOrden - importeComprobante));

            case TipoImputacionPago.OrdenPago:
                return new(0, true, importeComprobante, 0, false, ImputacionRules.Redondear(saldoOrden - importeComprobante));

            default:
                throw new BusinessException("Tipo de comprobante no admitido en una orden de pago.");
        }
    }
}
