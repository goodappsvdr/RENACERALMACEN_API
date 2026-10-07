using API.SERVICE.Domain.Exceptions;

namespace API.SERVICE.Domain.Cobranzas;

/// <summary>
/// Cómo un recibo cancela los comprobantes que imputa. Replica el Select Case de Agregar_Ws (FrmRecibos):
/// el importe del recibo se va consumiendo en el orden en que llegan los comprobantes.
/// </summary>
public static class ImputacionRules
{
    /// <summary>Comprobantes cuyo saldo en cta. cte. se muestra con el signo invertido (EntidadesCtaCte_BuscarPorID_Entidad_Recibos).</summary>
    private static readonly int[] TiposConSaldoInvertido = [4, 12, 9];

    /// <summary>Comprobantes que generan días de mora (EntidadesCtaCte_BuscarPorID_Entidad_Recibos).</summary>
    public static readonly int[] TiposConVencimiento = [11, 3];

    /// <summary>Estado de cta. cte. que lista el SP de comprobantes pendientes (hardcodeado en el ERP).</summary>
    public const int EstadoCtaCtePendiente = 48;

    /// <summary>Estado con el que el ERP graba todo recibo nuevo (hardcodeado: "ESTADO GENERADO").</summary>
    public const int EstadoReciboNuevo = 56;

    /// <summary>Saldo pendiente tal como lo muestra (y lo manda) el formulario de recibos.</summary>
    public static decimal SaldoVisible(int idComprobanteTipo, decimal saldo) =>
        TiposConSaldoInvertido.Contains(idComprobanteTipo) ? -saldo : saldo;

    public static decimal Redondear(decimal importe) => Math.Round(importe, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Aplica el saldo disponible del recibo a un comprobante.
    /// </summary>
    /// <param name="tipo">Clasificación del comprobante.</param>
    /// <param name="importeComprobante">Saldo del comprobante + interés.</param>
    /// <param name="saldoRecibo">Importe del recibo que todavía no se imputó.</param>
    public static ResultadoImputacion Imputar(TipoImputacion tipo, decimal importeComprobante, decimal saldoRecibo)
    {
        switch (tipo)
        {
            case TipoImputacion.Venta:
            case TipoImputacion.Compra:
                if (saldoRecibo >= importeComprobante)
                {
                    return new ResultadoImputacion(
                        SaldoCtaCte: 0,
                        Cancelado: true,
                        ImporteImputado: importeComprobante,
                        SaldoComprobante: 0,
                        Parcial: false,
                        SaldoReciboRestante: Redondear(saldoRecibo - importeComprobante));
                }

                if (saldoRecibo <= 0)
                {
                    // El ERP igual lo marcaba "cobrado parcial" con 0 imputado; acá se rechaza.
                    throw new BusinessException("El importe del recibo no alcanza para imputar todos los comprobantes seleccionados.");
                }

                return new ResultadoImputacion(
                    SaldoCtaCte: importeComprobante - saldoRecibo,
                    Cancelado: false,
                    ImporteImputado: saldoRecibo,
                    SaldoComprobante: importeComprobante - saldoRecibo,
                    Parcial: true,
                    SaldoReciboRestante: 0);

            case TipoImputacion.Recibo:
            case TipoImputacion.NotaCredito:
                return Total(importeComprobante, importeComprobante, saldoRecibo);

            case TipoImputacion.OrdenPago:
                // La OP se registra con el importe en negativo en EntidadRecibosDocumentosCliente.
                return Total(importeComprobante, -importeComprobante, saldoRecibo);

            default:
                throw new BusinessException("Tipo de comprobante no admitido en un recibo.");
        }
    }

    /// <summary>
    /// Saldo que queda en la cta. cte. del recibo: lo que pagó de más el cliente (en negativo) o 0 si cubrió todo.
    /// Mismo cálculo que el ERP: TotalComprobantes - TotalRecibo, y si es >= 0 el recibo queda cancelado.
    /// </summary>
    public static (decimal Saldo, bool Cancelado) SaldoRecibo(decimal totalComprobantes, decimal totalRecibo)
    {
        var diferencia = Redondear(totalComprobantes - totalRecibo);
        return diferencia >= 0 ? (0m, true) : (diferencia, false);
    }

    private static ResultadoImputacion Total(decimal importeComprobante, decimal importeImputado, decimal saldoRecibo) =>
        new(
            SaldoCtaCte: 0,
            Cancelado: true,
            ImporteImputado: importeImputado,
            SaldoComprobante: 0,
            Parcial: false,
            SaldoReciboRestante: Redondear(saldoRecibo - importeComprobante));
}

/// <param name="SaldoCtaCte">Nuevo saldo de la fila de cta. cte. del comprobante.</param>
/// <param name="Cancelado">Si el comprobante queda cancelado en la cta. cte.</param>
/// <param name="ImporteImputado">Importe del recibo aplicado (EntidadRecibosDocumentosCliente.ImporteRecibo).</param>
/// <param name="SaldoComprobante">Lo que le queda por cobrar al comprobante (EntidadRecibosDocumentosCliente.Saldo).</param>
/// <param name="Parcial">Si fue un cobro parcial (define el estado del comprobante).</param>
/// <param name="SaldoReciboRestante">Importe del recibo que queda para los siguientes comprobantes.</param>
public sealed record ResultadoImputacion(
    decimal SaldoCtaCte,
    bool Cancelado,
    decimal ImporteImputado,
    decimal SaldoComprobante,
    bool Parcial,
    decimal SaldoReciboRestante);
