using Db = global::API.DA.Entities;

namespace API.SERVICE.Interfaces.Compras;

/// <summary>
/// Persistencia de comprobantes de proveedor (DocumentosProveedor) y lo que mueven: detalle, otros tributos, libro IVA compras,
/// relaciones con remitos / órdenes de compra. Stock y cta. cte. usan los repositorios de ventas y cobranzas (mismas tablas).
/// Se usa dentro de <see cref="IUnitOfWork"/>.
/// </summary>
public interface ICompraRepository
{
    Task<Db.DocumentosProveedor?> GetDocumentoAsync(int idDocumentoProveedor, CancellationToken cancellationToken = default);

    Task<List<Db.DocumentosProveedorDetalle>> GetDetallesAsync(int idDocumentoProveedor, CancellationToken cancellationToken = default);

    /// <summary>DocumentosProveedor_ValidarDuplicados: mismo proveedor, tipo, punto de venta y número, no anulado.</summary>
    Task<bool> ExisteDuplicadoAsync(int idProveedor, int idComprobanteTipo, string puntoVenta, string numero, int estadoAnulado, CancellationToken cancellationToken = default);

    /// <summary>ComprobantesLetras_BuscarLetra: letras válidas según la categoría de IVA de la sucursal y la del proveedor.</summary>
    Task<List<string>> GetLetrasAsync(int idComprobanteTipo, int idCategoriaIvaEmpresa, int idCategoriaIvaProveedor, CancellationToken cancellationToken = default);

    /// <summary>CajaPlanillas_BuscarPorID_UsuarioAbierta: cualquier planilla abierta del usuario.</summary>
    Task<Db.CajaPlanillas?> GetPlanillaAbiertaAsync(int idUsuario, int estadoAbierta, CancellationToken cancellationToken = default);

    /// <summary>Comprobantes del proveedor de los tipos indicados, no excluidos por estado y con líneas con saldo pendiente.</summary>
    Task<List<Db.DocumentosProveedor>> GetComprobantesConPendienteAsync(
        int idProveedor, IReadOnlyCollection<int> tipos, IReadOnlyCollection<int> estadosExcluidos, int? idSucursal, CancellationToken cancellationToken = default);

    /// <summary>Líneas con saldo pendiente (..._Pendiente_RemitarNuevo).</summary>
    Task<List<LineaPendienteCompraRow>> GetLineasPendientesAsync(int idDocumentoProveedor, IReadOnlyCollection<int> tipos, CancellationToken cancellationToken = default);

    /// <summary>Relaciones donde el comprobante es el destino (DocumentosProveedorRemitos_BuscarPorID_Remito).</summary>
    Task<List<Db.DocumentosProveedorRemitos>> GetRelacionesComoDestinoAsync(int idDocumentoProveedor, CancellationToken cancellationToken = default);

    /// <summary>Borra una relación por su clave (el SP del ERP borraba por ID_DocumentoProveedor, otra columna).</summary>
    Task BorrarRelacionAsync(int idDocumentoProveedorRemito, CancellationToken cancellationToken = default);

    /// <summary>DocumentosProveedor_ModificarEstado_Pendiente.</summary>
    Task SetEstadoPendienteAsync(int idDocumentoProveedor, int estado, bool pendiente, CancellationToken cancellationToken = default);

    /// <summary>DocumentosProveedor_Determinar_Remitar_Facturar.</summary>
    Task DeterminarRemitarFacturarAsync(int idDocumentoProveedor, bool remitar, bool facturar, bool pendiente, CancellationToken cancellationToken = default);

    /// <summary>DocumentosProveedor_Anular.</summary>
    Task AnularDocumentoAsync(int idDocumentoProveedor, int estado, DateTime ahora, CancellationToken cancellationToken = default);

    /// <summary>LibroIvaCompra_Anular + TxtComprasAlicuotas_Anular (este último filtrando bien por tipo).</summary>
    Task BorrarLibroIvaAsync(int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default);

    /// <summary>DocumentosProveedorOtrosTributos_Anular.</summary>
    Task BorrarOtrosTributosAsync(int idDocumentoProveedor, int idComprobanteTipo, CancellationToken cancellationToken = default);

    /// <summary>Cta. cte. del comprobante (EntidadesCtaCte_BuscarPorID_ComprobanteTipoID_Comprobante).</summary>
    Task<Db.EntidadesCtaCte?> GetCtaCteAsync(int idComprobanteTipo, int idComprobante, CancellationToken cancellationToken = default);

    void Add<TEntity>(TEntity entity) where TEntity : class;

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record LineaPendienteCompraRow(Db.DocumentosProveedorDetalle Detalle, int IdComprobanteTipo, decimal Saldo);
