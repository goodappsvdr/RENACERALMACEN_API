using Db = global::API.DA.Entities;

namespace API.SERVICE.Interfaces.Clientes;

/// <summary>
/// Persistencia del recibo de cobro y de todo lo que afecta (cta. cte., caja, cheques, bancos, retenciones,
/// comprobantes imputados). Cada método equivale a un SP del ERP; se usa dentro de <see cref="IUnitOfWork"/>.
/// </summary>
public interface IReciboCobroRepository
{
    // ---------- Lecturas ----------

    /// <summary>Planilla de caja abierta del usuario para el punto de venta de recibos (CajaPlanillas_IniciarPuntoVenta).</summary>
    Task<Db.CajaPlanillas?> GetPlanillaAbiertaAsync(int idUsuario, int idComprobanteTipo, string letra, int estadoAbierta, CancellationToken cancellationToken = default);

    /// <summary>Próximo número del punto de venta, sin reservarlo (PuntosVenta_BuscarNumero).</summary>
    Task<long?> GetProximoNumeroAsync(string puntoVenta, string letra, int idComprobanteTipo, CancellationToken cancellationToken = default);

    Task<Db.Entidades?> GetEntidadAsync(int idEntidad, CancellationToken cancellationToken = default);

    /// <summary>Filas pendientes (no canceladas) de cta. cte. de un comprobante de la entidad.</summary>
    Task<List<Db.EntidadesCtaCte>> GetCtaCtePendienteAsync(int idEntidad, int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default);

    /// <summary>Comprobantes pendientes de la entidad (EntidadesCtaCte_BuscarPorID_Entidad_Recibos).</summary>
    Task<List<ComprobantePendienteRow>> GetComprobantesPendientesAsync(int idEntidad, CancellationToken cancellationToken = default);

    Task<Db.EntidadesRecibos?> GetReciboAsync(int idRecibo, CancellationToken cancellationToken = default);

    /// <summary>Comprobantes imputados por el recibo, con el interés acumulado en su cta. cte.</summary>
    Task<List<ImputacionRow>> GetImputacionesAsync(int idRecibo, CancellationToken cancellationToken = default);

    Task<bool> TieneRelacionAsync(int idDocumentoCliente, CancellationToken cancellationToken = default);

    /// <summary>Si el comprobante sigue imputado por algún recibo.</summary>
    Task<bool> TieneImputacionesAsync(int idDocumentoCliente, CancellationToken cancellationToken = default);

    /// <summary>ID de la fila de cta. cte. de un comprobante (Entidades_BuscarPorID_ComprobanteTipoID_Comprobante, primera fila).</summary>
    Task<long?> GetIdCtaCteAsync(int idComprobanteTipo, int idComprobante, CancellationToken cancellationToken = default);

    // ---------- Altas (se confirman con SaveChangesAsync) ----------

    void Add<TEntity>(TEntity entity) where TEntity : class;

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Incrementa el número del punto de venta y devuelve el número reservado; null si el punto de venta no existe (PuntosVenta_Numerar_PuntoVenta).</summary>
    Task<long?> ReservarNumeroAsync(string puntoVenta, string letra, int idComprobanteTipo, CancellationToken cancellationToken = default);

    // ---------- Actualizaciones de la cobranza ----------

    /// <summary>EntidadesCtaCte_Modificar_Saldo: saldo, fecha de pago, cancelado e interés acumulado.</summary>
    Task ImputarCtaCteAsync(int idComprobante, int idComprobanteTipo, decimal saldo, DateTime fechaPago, bool cancelado, decimal interesAplicado, CancellationToken cancellationToken = default);

    Task SetEstadoDocumentoClienteAsync(int idDocumentoCliente, int estado, CancellationToken cancellationToken = default);

    Task SetEstadoDocumentoProveedorAsync(int idDocumentoProveedor, int estado, CancellationToken cancellationToken = default);

    Task SetEstadoReciboAsync(int idRecibo, int estado, CancellationToken cancellationToken = default);

    Task SetEstadoOrdenPagoAsync(int idProveedorRecibo, int estado, CancellationToken cancellationToken = default);

    // ---------- Anulación ----------

    /// <summary>ClientesRecibosDetalle_Anular sobre todo el detalle del recibo.</summary>
    Task AnularDetalleAsync(int idRecibo, DateTime ahora, CancellationToken cancellationToken = default);

    /// <summary>CajasPlanillasDetalle_Anular sobre los movimientos de caja del comprobante.</summary>
    Task AnularCajaAsync(int idComprobanteTipo, int idComprobante, CancellationToken cancellationToken = default);

    /// <summary>ClientesCheques_ModificarEstado sobre los cheques recibidos con el recibo.</summary>
    Task AnularChequesAsync(int idRecibo, int idComprobanteTipo, int estadoAnulado, CancellationToken cancellationToken = default);

    /// <summary>BancosCuentasMovimientos_Anular sobre los movimientos bancarios del comprobante.</summary>
    Task AnularMovimientosBancoAsync(int idComprobanteTipo, int idComprobante, int estadoAnulado, DateTime ahora, CancellationToken cancellationToken = default);

    /// <summary>Retenciones_Anular sobre las retenciones del comprobante.</summary>
    Task AnularRetencionesAsync(int idComprobanteTipo, int idComprobante, int estadoAnulado, CancellationToken cancellationToken = default);

    /// <summary>EntidadesCtaCte_Anular_Recibo_OrdenPago: devuelve saldo e interés al comprobante imputado.</summary>
    Task RevertirImputacionCtaCteAsync(int idComprobante, int idComprobanteTipo, decimal saldo, decimal interesAplicado, DateTime ahora, CancellationToken cancellationToken = default);

    /// <summary>EntidadRecibosDocumentosCliente_AnularPorID_Recibo (borra las imputaciones).</summary>
    Task BorrarImputacionesAsync(int idRecibo, CancellationToken cancellationToken = default);

    /// <summary>EntidadesCtaCte_Anular + EntidadesCtaCteMovimientos_Anular de la fila de cta. cte. del recibo.</summary>
    Task AnularCtaCteAsync(long idEntidadCtaCte, int estadoAnulado, DateTime ahora, CancellationToken cancellationToken = default);
}

/// <summary>Fila de comprobantes pendientes (columnas del SP del ERP).</summary>
public sealed record ComprobantePendienteRow(
    int? IdComprobante,
    int? IdComprobanteTipo,
    int? IdEntidad,
    string? Concepto,
    string? RazonSocial,
    DateTime? Fecha,
    DateTime? FechaVencimiento,
    decimal? Saldo,
    decimal? InteresAplicado,
    short? InteresCliente,
    byte? DiasInteres);

public sealed record ImputacionRow(
    int? IdEntidad,
    int? IdDocumentoCliente,
    int? IdComprobanteTipo,
    decimal? ImporteRecibo,
    decimal InteresAplicado);
