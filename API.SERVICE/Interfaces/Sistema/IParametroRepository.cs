namespace API.SERVICE.Interfaces.Sistema;

public partial interface IParametroRepository
{
    /// <summary>
    /// Valor de un parámetro por categoría y nombre (ignora espacios finales, como Parametros_BuscarporCategoriaNombre).
    /// Equivale a SingletonParametro() del WebForms: null si no existe.
    /// </summary>
    Task<string?> GetValorAsync(string categoria, string nombre, CancellationToken cancellationToken = default);
}
