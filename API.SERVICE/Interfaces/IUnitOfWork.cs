namespace API.SERVICE.Interfaces;

/// <summary>
/// Transacción de base de datos para flujos que escriben en varias tablas (alta de ítem, facturación, etc.).
/// Reemplaza al IniciaTransaccion/FinalizaTransaccion/CancelaTransaccion del WebForms.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Ejecuta <paramref name="operation"/> dentro de una transacción: commit si termina bien, rollback si lanza.
    /// Ante un error transitorio de SQL la operación completa puede reintentarse, así que todas las lecturas
    /// y escrituras del flujo tienen que hacerse adentro del delegado (no traer entidades de antes).
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
