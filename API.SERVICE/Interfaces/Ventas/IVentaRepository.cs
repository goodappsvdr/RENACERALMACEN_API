using Db = global::API.DA.Entities;

namespace API.SERVICE.Interfaces.Ventas;

/// <summary>
/// Persistencia de los comprobantes de venta (DocumentosCliente) y de lo que mueven: detalle, stock, ofertas,
/// números de serie, remitos/presupuestos relacionados. Cada método equivale a un SP del ERP; se usa dentro de
/// <see cref="IUnitOfWork"/>. Caja, cheques, bancos, retenciones, cta. cte. y numeración están en IReciboCobroRepository.
/// </summary>
public interface IVentaRepository
{
    // ---------- Lecturas ----------

    /// <summary>Saldo de cta. cte. del cliente: suma de Total2 (Entidades_BuscarPorId).</summary>
    Task<decimal> GetSaldoCtaCteAsync(int idEntidad, CancellationToken cancellationToken = default);

    /// <summary>Oferta activa del ítem en la sucursal (ItemsOfertas_BuscarPorSucursalItem); null si no hay.</summary>
    Task<OfertaActivaRow?> GetOfertaActivaAsync(int idSucursal, int idItem, CancellationToken cancellationToken = default);

    /// <summary>Si al comprobante relacionado le quedan ítems sin remitar/facturar (..._Pendiente_Remitar).</summary>
    Task<bool> TienePendienteRemitarAsync(int idDocumentoCliente, CancellationToken cancellationToken = default);

    Task<Db.DocumentosCliente?> GetDocumentoAsync(int idDocumentoCliente, CancellationToken cancellationToken = default);

    Task<List<Db.DocumentosClienteDetalle>> GetDetallesAsync(int idDocumentoCliente, CancellationToken cancellationToken = default);

    Task<List<Db.DocumentosClienteRemitos>> GetRemitosAsociadosAsync(int idDocumentoCliente, CancellationToken cancellationToken = default);

    Task<List<Db.EntidadesCtaCteStockMovimientosDetalle>> GetMovimientosStockAsync(int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default);

    /// <summary>Total − saldo pendiente de stock del comprobante (..._BuscarPorID_Comprobante_Tipo_Saldo).</summary>
    Task<decimal> GetSaldoStockAsync(int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default);

    /// <summary>Recibo que imputó al comprobante (el que se generó al cobrarlo en el momento); null si no hay.</summary>
    Task<int?> GetReciboImputadoAsync(int idDocumentoCliente, int idComprobanteTipo, CancellationToken cancellationToken = default);

    // ---------- Altas ----------

    void Add<TEntity>(TEntity entity) where TEntity : class;

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    // ---------- Stock ----------

    /// <summary>Items_RestarStock + ItemsSucursales_RestarStock (solo ítems que mueven stock).</summary>
    Task RestarStockAsync(int idItem, int idSucursal, decimal cantidad, DateTime ahora, CancellationToken cancellationToken = default);

    /// <summary>Items_SumarStock + ItemsSucursales_SumarStock (solo ítems que mueven stock).</summary>
    Task SumarStockAsync(int idItem, int idSucursal, decimal cantidad, DateTime ahora, CancellationToken cancellationToken = default);

    /// <summary>EntidadesCtaCteStockMovimientosDetalle_Modificar_{Sumar|Restar}_Saldo sobre la línea del comprobante relacionado.</summary>
    Task AjustarSaldoStockAsync(int idComprobante, int idComprobanteDetalle, int idComprobanteTipo, int idItem, decimal delta, CancellationToken cancellationToken = default);

    /// <summary>EntidadesCtaCteStockMovimientosDetalle_Anular.</summary>
    Task AnularMovimientosStockAsync(int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default);

    // ---------- Ofertas y números de serie ----------

    /// <summary>ItemsOfertas_{Descontar|Devolver}Cantidad_Disponible (OfertasAgotamiento).</summary>
    Task AjustarOfertaDisponibleAsync(int idOferta, decimal delta, CancellationToken cancellationToken = default);

    /// <summary>ItemsNroSeries_Modificar: asigna el número de serie a la línea del comprobante.</summary>
    Task AsignarNroSerieAsync(int idItemNroSerie, int idComprobanteTipo, int idComprobante, long idDetalle, int estado, CancellationToken cancellationToken = default);

    /// <summary>ItemsNroSeries_Modificar_Disponibilidad: libera los números de serie del comprobante.</summary>
    Task LiberarNrosSerieAsync(int idComprobanteTipo, int idComprobante, int estado, CancellationToken cancellationToken = default);

    // ---------- Estado de comprobantes ----------

    /// <summary>DocumentosCliente_ModificarEstado_Pendiente.</summary>
    Task SetEstadoPendienteAsync(int idDocumentoCliente, int estado, bool pendiente, CancellationToken cancellationToken = default);

    /// <summary>DocumentosCliente_Determinar_Remitar_Facturar.</summary>
    Task DeterminarRemitarFacturarAsync(int idDocumentoCliente, bool remitar, bool facturar, bool pendiente, CancellationToken cancellationToken = default);

    /// <summary>DocumentosClienteDetalle_Anular.</summary>
    Task AnularDetalleAsync(long idDetalle, int estadoLibroIva, CancellationToken cancellationToken = default);

    /// <summary>DocumentosClienteRemitos_Anular (borra la relación factura-remito).</summary>
    Task BorrarRemitoAsociadoAsync(int idDocumentoClienteRemito, CancellationToken cancellationToken = default);

    /// <summary>DocumentosCliente_Anular.</summary>
    Task AnularDocumentoAsync(int idDocumentoCliente, int estado, DateTime ahora, CancellationToken cancellationToken = default);

    // ---------- Factura electrónica ----------

    /// <summary>Sucursal emisora (Sucursales_BuscarPorID): CUIT, punto de venta AFIP y categoría de IVA.</summary>
    Task<Db.Sucursales?> GetSucursalAsync(int idSucursal, CancellationToken cancellationToken = default);

    /// <summary>
    /// DocumentosCliente_Modificar_DatosAfip: punto de venta, número, CAE y código de barras.
    /// Igual que el SP, no toca ID_PuntoVenta (el SP hace ID_PuntoVenta = ID_PuntoVenta).
    /// </summary>
    Task ModificarDatosAfipAsync(int idDocumentoCliente, string puntoVenta, string numero, string cae, string codigoBarras, CancellationToken cancellationToken = default);

    /// <summary>Facturas del tipo indicado todavía sin CAE (CAE = "0") y no anuladas.</summary>
    Task<List<Db.DocumentosCliente>> GetPendientesAfipAsync(int idComprobanteTipo, int estadoAnulado, CancellationToken cancellationToken = default);

    // ---------- Nota de crédito ----------

    /// <summary>Todos los recibos que imputaron al comprobante (EntidadRecibosDocumentosCliente_BuscarPorID_DocumentoCliente_ComprobanteTipo).</summary>
    Task<List<int>> GetRecibosImputadosAsync(int idDocumentoCliente, int idComprobanteTipo, CancellationToken cancellationToken = default);

    /// <summary>Total de notas de crédito no anuladas ya relacionadas a la factura (DocumentosClienteRelacion), sin contar <paramref name="excluirId"/>.</summary>
    Task<decimal> GetTotalNotasCreditoAsync(int idFactura, int idTipoNotaCredito, int estadoAnulado, int excluirId, CancellationToken cancellationToken = default);

    /// <summary>Factura que acredita la nota de crédito (DocumentosClienteRelacion: ID_DocumentoCliente1 = factura, 2 = NC).</summary>
    Task<int?> GetFacturaDeNotaCreditoAsync(int idNotaCredito, CancellationToken cancellationToken = default);

    Task BorrarRelacionAsync(int idFactura, int idNotaCredito, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lock exclusivo de la factura mientras se pide el CAE (sp_getapplock de sesión: dura lo que dura la llamada a AFIP,
    /// que va fuera de toda transacción). Si otro request ya la está autorizando, lanza ConflictException.
    /// </summary>
    Task<IAsyncDisposable> BloquearAutorizacionAsync(int idDocumentoCliente, CancellationToken cancellationToken = default);
}

public sealed record OfertaActivaRow(int IdOferta, int TipoOferta);
