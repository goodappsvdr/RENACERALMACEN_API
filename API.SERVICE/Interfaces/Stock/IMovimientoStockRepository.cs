using Db = global::API.DA.Entities;

namespace API.SERVICE.Interfaces.Stock;

/// <summary>Ítem a transferir: descripción, si mueve stock y el stock en la sucursal consultada (null si el ítem no tiene fila en ItemsSucursales para esa sucursal).</summary>
public sealed record ItemStockSucursalRow(int IdItem, string Descripcion, bool MueveStock, decimal? Stock);

/// <summary>
/// Movimientos de stock entre sucursales (comprobante MS en DocumentosCliente) — FrmMovimientoStockABM / FrmMovimientoStockRecibirABM.
/// La numeración usa IReciboCobroRepository (PuntosVenta).
/// </summary>
public interface IMovimientoStockRepository
{
    Task<Db.Sucursales?> GetSucursalAsync(int idSucursal, CancellationToken cancellationToken = default);

    /// <summary>Si el usuario tiene la sucursal asignada en UsuariosSucursales.</summary>
    Task<bool> OperaSucursalAsync(int idUsuario, int idSucursal, CancellationToken cancellationToken = default);

    Task<List<ItemStockSucursalRow>> GetItemsAsync(IReadOnlyCollection<int> idItems, int idSucursal, CancellationToken cancellationToken = default);

    Task<Db.DocumentosCliente?> GetMovimientoAsync(int idDocumentoCliente, int idComprobanteTipo, CancellationToken cancellationToken = default);

    Task<List<Db.DocumentosClienteDetalle>> GetDetallesAsync(int idDocumentoCliente, CancellationToken cancellationToken = default);

    /// <summary>Movimientos en tránsito (<paramref name="estado"/>) hacia la sucursal destino (ID_Cliente).</summary>
    Task<List<Db.DocumentosCliente>> GetEnTransitoAsync(int idSucursalDestino, int idComprobanteTipo, int estado, CancellationToken cancellationToken = default);

    /// <summary>Cambia el estado solo si sigue en <paramref name="estadoEsperado"/>. False si otro lo cambió antes.</summary>
    Task<bool> CambiarEstadoAsync(int idDocumentoCliente, int estado, int estadoEsperado, DateTime? fechaAnulacion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suma <paramref name="delta"/> (negativo para restar) al stock del ítem en la sucursal (ItemsSucursales, solo si mueve stock).
    /// No toca Items.StockActual: una transferencia no cambia el stock total de la empresa.
    /// </summary>
    Task MoverStockSucursalAsync(int idItem, int idSucursal, decimal delta, DateTime ahora, CancellationToken cancellationToken = default);

    /// <summary>DocumentosClienteDetalle_Anular de todo el detalle.</summary>
    Task AnularDetallesAsync(int idDocumentoCliente, int estadoLibroIva, CancellationToken cancellationToken = default);

    /// <summary>Lock de transacción (sp_getapplock) por sucursal de origen: dos transferencias simultáneas no validan el mismo stock.</summary>
    Task BloquearSucursalAsync(int idSucursal, CancellationToken cancellationToken = default);

    void Add<TEntity>(TEntity entity) where TEntity : class;

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
