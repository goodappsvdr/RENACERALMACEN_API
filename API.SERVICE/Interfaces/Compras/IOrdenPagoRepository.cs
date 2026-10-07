using Db = global::API.DA.Entities;

namespace API.SERVICE.Interfaces.Compras;

/// <summary>
/// Lo propio de la orden de pago (ProveedoresRecibos): imputaciones, cheques de terceros entregados y cheques propios.
/// Cta. cte., caja, bancos, retenciones y numeración usan IReciboCobroRepository (mismas tablas que el recibo).
/// </summary>
public interface IOrdenPagoRepository
{
    Task<Db.ProveedoresRecibos?> GetOrdenAsync(int idOrdenPago, CancellationToken cancellationToken = default);

    /// <summary>Comprobantes pendientes de la cta. cte. del proveedor (EntidadesCtaCte_BuscarPorID_Entidad_OrdenPago).</summary>
    Task<List<Db.EntidadesCtaCte>> GetComprobantesPendientesAsync(int idEntidad, int estadoCtaCte, CancellationToken cancellationToken = default);

    Task<List<Db.EntidadOrdenPagoDocumentosProveedores>> GetImputacionesAsync(int idOrdenPago, CancellationToken cancellationToken = default);

    /// <summary>EntidadOrdenPagoDocumentosProveedores_AnularPorID_OP.</summary>
    Task BorrarImputacionesAsync(int idOrdenPago, CancellationToken cancellationToken = default);

    /// <summary>Si el comprobante sigue imputado por alguna orden de pago (el SP del ERP comparaba la columna consigo misma).</summary>
    Task<bool> TieneImputacionesAsync(int idDocumento, int idComprobanteTipo, CancellationToken cancellationToken = default);

    Task<Db.EntidadesCheques?> GetChequeTerceroAsync(int idEntidadCheque, CancellationToken cancellationToken = default);

    /// <summary>EntidadesCheques_ModificarOP, solo si el cheque sigue en <paramref name="estadoEsperado"/>. False si otro ya lo tomó.</summary>
    Task<bool> AsignarChequeTerceroAsync(int idEntidadCheque, int idOrdenPago, int idComprobanteTipo, int estado, int estadoEsperado, CancellationToken cancellationToken = default);

    /// <summary>Devuelve a cartera los cheques de terceros entregados con la orden.</summary>
    Task DevolverChequesTercerosAsync(int idOrdenPago, int idComprobanteTipo, int estadoEnCartera, CancellationToken cancellationToken = default);

    /// <summary>Cheque propio con los datos de su cuenta (BancosCheques_BuscarPorID).</summary>
    Task<ChequePropioRow?> GetChequePropioAsync(int idBancoCheque, CancellationToken cancellationToken = default);

    /// <summary>BancosCheques_Modificar al entregarlo, solo si sigue en <paramref name="estadoEsperado"/>. False si otro ya lo usó.</summary>
    Task<bool> EntregarChequePropioAsync(int idBancoCheque, string nroCheque, int idOrdenPago, int idComprobanteTipo, DateTime fechaEmision, DateTime fechaVencimiento, decimal importe, int estado, int estadoEsperado, CancellationToken cancellationToken = default);

    /// <summary>BancosCheques_Anular de los cheques propios entregados con la orden.</summary>
    Task AnularChequesPropiosAsync(int idOrdenPago, int idComprobanteTipo, int estado, CancellationToken cancellationToken = default);

    /// <summary>Marca anulados los cheques registrados como entregados al proveedor (ProveedoresCheques).</summary>
    Task AnularChequesProveedorAsync(int idOrdenPago, int idComprobanteTipo, int estado, CancellationToken cancellationToken = default);

    /// <summary>ProveedoresRecibosDetalle_Anular de todo el detalle.</summary>
    Task AnularDetalleAsync(int idOrdenPago, DateTime ahora, CancellationToken cancellationToken = default);

    void Add<TEntity>(TEntity entity) where TEntity : class;

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record ChequePropioRow(Db.BancosCheques Cheque, int? IdBanco, int? IdBancoSucursal, int? IdCuentaTipo, string? NroCuenta);
