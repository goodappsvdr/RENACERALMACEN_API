using API.DA.Entities;
using API.SERVICE.Interfaces.Stock;

namespace API.TESTS.Infrastructure;

/// <summary>Movimientos de stock entre sucursales en memoria: stock por (ítem, sucursal) y operaciones para inspeccionarlas.</summary>
public sealed class FakeMovimientoStockRepository : IMovimientoStockRepository
{
    private int _nextId = 500;

    public List<object> Added { get; } = [];
    public List<string> Operaciones { get; } = [];
    public Dictionary<int, Sucursales> Sucursales { get; } = [];
    public HashSet<(int IdUsuario, int IdSucursal)> UsuariosSucursales { get; } = [];
    public Dictionary<int, (string Descripcion, bool MueveStock)> Items { get; } = [];
    public Dictionary<(int IdItem, int IdSucursal), decimal> Stock { get; } = [];
    public Dictionary<int, DocumentosCliente> Documentos { get; } = [];
    public List<DocumentosClienteDetalle> Detalles { get; } = [];

    public T Single<T>() => Added.OfType<T>().Single();
    public IEnumerable<T> All<T>() => Added.OfType<T>();

    public Task<Sucursales?> GetSucursalAsync(int idSucursal, CancellationToken cancellationToken = default) =>
        Task.FromResult(Sucursales.GetValueOrDefault(idSucursal));

    public Task<bool> OperaSucursalAsync(int idUsuario, int idSucursal, CancellationToken cancellationToken = default) =>
        Task.FromResult(UsuariosSucursales.Contains((idUsuario, idSucursal)));

    public Task<List<ItemStockSucursalRow>> GetItemsAsync(IReadOnlyCollection<int> idItems, int idSucursal, CancellationToken cancellationToken = default) =>
        Task.FromResult(idItems.Where(Items.ContainsKey)
            .Select(id => new ItemStockSucursalRow(id, Items[id].Descripcion, Items[id].MueveStock,
                Stock.TryGetValue((id, idSucursal), out var s) ? s : null))
            .ToList());

    public Task<DocumentosCliente?> GetMovimientoAsync(int idDocumentoCliente, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        Task.FromResult(Documentos.GetValueOrDefault(idDocumentoCliente) is { } d && d.IdComprobanteTipo == idComprobanteTipo ? d : null);

    public Task<List<DocumentosClienteDetalle>> GetDetallesAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        Task.FromResult(Detalles.Concat(All<DocumentosClienteDetalle>()).Where(d => d.IdDocumentoCliente == idDocumentoCliente).Distinct().ToList());

    public Task<List<DocumentosCliente>> GetEnTransitoAsync(int idSucursalDestino, int idComprobanteTipo, int estado, CancellationToken cancellationToken = default) =>
        Task.FromResult(Documentos.Values.Where(d => d.IdCliente == idSucursalDestino && d.IdComprobanteTipo == idComprobanteTipo && d.Estado == estado).ToList());

    public Task<bool> CambiarEstadoAsync(int idDocumentoCliente, int estado, int estadoEsperado, DateTime? fechaAnulacion, CancellationToken cancellationToken = default)
    {
        if (Documentos.GetValueOrDefault(idDocumentoCliente) is not { } d || d.Estado != estadoEsperado) return Task.FromResult(false);
        d.Estado = estado;
        Operaciones.Add($"Estado {idDocumentoCliente}={estado}");
        return Task.FromResult(true);
    }

    public Task MoverStockSucursalAsync(int idItem, int idSucursal, decimal delta, DateTime ahora, CancellationToken cancellationToken = default)
    {
        if (Items.GetValueOrDefault(idItem).MueveStock && Stock.ContainsKey((idItem, idSucursal)))
            Stock[(idItem, idSucursal)] += delta;
        Operaciones.Add(FormattableString.Invariant($"Stock {idItem}@{idSucursal} {delta:+0.##;-0.##}"));
        return Task.CompletedTask;
    }

    public Task AnularDetallesAsync(int idDocumentoCliente, int estadoLibroIva, CancellationToken cancellationToken = default)
    {
        Operaciones.Add($"AnularDetalles {idDocumentoCliente}");
        return Task.CompletedTask;
    }

    public Task BloquearSucursalAsync(int idSucursal, CancellationToken cancellationToken = default)
    {
        Operaciones.Add($"Lock sucursal {idSucursal}");
        return Task.CompletedTask;
    }

    public void Add<TEntity>(TEntity entity) where TEntity : class => Added.Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var e in Added)
        {
            switch (e)
            {
                case DocumentosCliente d when d.IdDocumentoCliente == 0: d.IdDocumentoCliente = _nextId++; break;
                case DocumentosClienteDetalle d when d.IdDocumentoClienteDetalle == 0: d.IdDocumentoClienteDetalle = _nextId++; break;
            }
        }
        return Task.CompletedTask;
    }
}
