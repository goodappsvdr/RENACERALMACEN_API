namespace API.SERVICE.Interfaces.Sistema;

/// <summary>
/// Valores de referencia que el ERP resuelve por nombre en tiempo de ejecución:
/// SingletonParametro (Parametros), ValorEstado (Estados) y ValorCategoria (Categorias).
/// Los resultados se memorizan durante el request: un flujo los consulta muchas veces.
/// </summary>
public interface IReferenciasRepository
{
    /// <summary>Valor de Parametros por categoría y nombre; null si no existe.</summary>
    Task<string?> GetParametroAsync(string categoria, string nombre, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valor de Parametros por categoría, nombre e ID_Empresa (Parametros_BuscarporCategoriaNombreEmpresa).
    /// El ERP lo usa con el ID de sucursal para los datos del certificado de AFIP (API/CARPETA, API/CERTFICADO).
    /// </summary>
    Task<string?> GetParametroEmpresaAsync(string categoria, string nombre, int idEmpresa, CancellationToken cancellationToken = default);

    /// <summary>Parámetro numérico obligatorio (p. ej. COMPROBANTE/REC). Lanza si falta o no es un entero.</summary>
    Task<int> GetParametroEnteroAsync(string categoria, string nombre, CancellationToken cancellationToken = default);

    /// <summary>ID_Estado activo por categoría y nombre (Estados_BuscarPorCategoriaNombre). Lanza si no existe.</summary>
    Task<int> GetIdEstadoAsync(string categoria, string nombre, CancellationToken cancellationToken = default);

    /// <summary>ID_Categoria por tipo y nombre (BuscarPorCategoriaTipoNombre). Lanza si no existe.</summary>
    Task<int> GetIdCategoriaAsync(string categoriaTipo, string nombre, CancellationToken cancellationToken = default);
}
