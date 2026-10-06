using Microsoft.EntityFrameworkCore;

namespace API.SERVICE.Repositories.Sistema;

public sealed partial class ParametroRepository
{
    public Task<string?> GetValorAsync(string categoria, string nombre, CancellationToken cancellationToken = default)
    {
        var cat = categoria.TrimEnd();
        var nom = nombre.TrimEnd();

        // Sin cache a propósito: los flujos tienen que leer el valor vigente en el momento.
        return Set.AsNoTracking()
            .Where(p => p.Categoria!.TrimEnd() == cat && p.Nombre!.TrimEnd() == nom)
            .OrderBy(p => p.IdParametro)
            .Select(p => p.Valor)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
