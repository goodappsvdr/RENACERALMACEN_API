using System.Globalization;
using API.DA.DbContexts;
using API.SERVICE.Interfaces.Sistema;
using Microsoft.EntityFrameworkCore;

namespace API.SERVICE.Repositories.Sistema;

public sealed class ReferenciasRepository : IReferenciasRepository
{
    private readonly ElRenacerDbContext _context;

    // Memoria por request (el repositorio es scoped).
    private readonly Dictionary<string, object?> _memo = new(StringComparer.OrdinalIgnoreCase);

    public ReferenciasRepository(ElRenacerDbContext context)
    {
        _context = context;
    }

    public Task<string?> GetParametroAsync(string categoria, string nombre, CancellationToken cancellationToken = default) =>
        MemoAsync($"P|{categoria}|{nombre}", async () =>
        {
            var cat = categoria.TrimEnd();
            var nom = nombre.TrimEnd();
            return await _context.Parametros.AsNoTracking()
                .Where(p => p.Categoria!.TrimEnd() == cat && p.Nombre!.TrimEnd() == nom)
                .OrderBy(p => p.IdParametro)
                .Select(p => p.Valor)
                .FirstOrDefaultAsync(cancellationToken);
        });

    public Task<string?> GetParametroEmpresaAsync(string categoria, string nombre, int idEmpresa, CancellationToken cancellationToken = default) =>
        MemoAsync($"PE|{categoria}|{nombre}|{idEmpresa}", async () =>
        {
            var cat = categoria.TrimEnd();
            var nom = nombre.TrimEnd();
            return await _context.Parametros.AsNoTracking()
                .Where(p => p.Categoria!.TrimEnd() == cat && p.Nombre!.TrimEnd() == nom && p.IdEmpresa == idEmpresa)
                .OrderBy(p => p.IdParametro)
                .Select(p => p.Valor)
                .FirstOrDefaultAsync(cancellationToken);
        });

    public async Task<int> GetParametroEnteroAsync(string categoria, string nombre, CancellationToken cancellationToken = default)
    {
        var valor = await GetParametroAsync(categoria, nombre, cancellationToken);
        return int.TryParse(valor?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero)
            ? numero
            : throw new InvalidOperationException($"Falta el parámetro {categoria}/{nombre} (o no es numérico) en la tabla Parametros.");
    }

    public async Task<int> GetIdEstadoAsync(string categoria, string nombre, CancellationToken cancellationToken = default)
    {
        var id = await MemoAsync($"E|{categoria}|{nombre}", () =>
            _context.Estados.AsNoTracking()
                .Where(e => e.Categoria == categoria && e.Nombre == nombre && e.Activo == true)
                .OrderBy(e => e.IdEstado)
                .Select(e => (int?)e.IdEstado)
                .FirstOrDefaultAsync(cancellationToken));

        return id ?? throw new InvalidOperationException($"Falta el estado activo {categoria}/{nombre} en la tabla Estados.");
    }

    public async Task<int> GetIdCategoriaAsync(string categoriaTipo, string nombre, CancellationToken cancellationToken = default)
    {
        var id = await MemoAsync($"C|{categoriaTipo}|{nombre}", () =>
            _context.Categorias.AsNoTracking()
                .Where(c => c.CategoriaTipo == categoriaTipo && c.Nombre == nombre)
                .OrderBy(c => c.IdCategoria)
                .Select(c => (int?)c.IdCategoria)
                .FirstOrDefaultAsync(cancellationToken));

        return id ?? throw new InvalidOperationException($"Falta la categoría {categoriaTipo}/{nombre} en la tabla Categorias.");
    }

    private async Task<T> MemoAsync<T>(string key, Func<Task<T>> load)
    {
        if (_memo.TryGetValue(key, out var cached))
            return (T)cached!;

        var value = await load();
        _memo[key] = value;
        return value;
    }
}
