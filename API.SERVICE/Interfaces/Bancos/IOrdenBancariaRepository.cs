using Db = global::API.DA.Entities;

namespace API.SERVICE.Interfaces.Bancos;

/// <summary>
/// Órdenes de depósito (OrdenesDepositos) y de extracción (OrdenesExrtacciones) — FrmOrdendeDeposito / FrmOrdendeExtraccion.
/// Movimientos bancarios, cheques y numeración usan los repositorios de cobranzas y órdenes de pago (mismas tablas).
/// </summary>
public interface IOrdenBancariaRepository
{
    Task<Db.BancosCuentas?> GetCuentaAsync(int idBancoCuenta, CancellationToken cancellationToken = default);

    Task<Db.OrdenesDepositos?> GetDepositoAsync(int idOrdenDeposito, CancellationToken cancellationToken = default);

    Task<Db.OrdenesExrtacciones?> GetExtraccionAsync(int idOrdenExtraccion, CancellationToken cancellationToken = default);

    /// <summary>OrdenesDepositos_Anular, solo si sigue en <paramref name="estadoEsperado"/>. False si ya no estaba.</summary>
    Task<bool> AnularDepositoAsync(int idOrdenDeposito, int estado, int estadoEsperado, DateTime ahora, CancellationToken cancellationToken = default);

    /// <summary>OrdenesExtracciones_Anular, solo si sigue en <paramref name="estadoEsperado"/>. False si ya no estaba.</summary>
    Task<bool> AnularExtraccionAsync(int idOrdenExtraccion, int estado, int estadoEsperado, DateTime ahora, CancellationToken cancellationToken = default);

    /// <summary>OrdenesDepositosDetalle_Anular de todo el detalle.</summary>
    Task AnularDetalleDepositoAsync(int idOrdenDeposito, DateTime ahora, CancellationToken cancellationToken = default);

    /// <summary>OrdenesExtraccionesDetalle_Anular de todo el detalle.</summary>
    Task AnularDetalleExtraccionAsync(int idOrdenExtraccion, DateTime ahora, CancellationToken cancellationToken = default);

    void Add<TEntity>(TEntity entity) where TEntity : class;

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
