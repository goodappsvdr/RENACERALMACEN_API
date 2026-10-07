using API.DA.Entities;
using API.SERVICE.Interfaces.Bancos;

namespace API.TESTS.Infrastructure;

/// <summary>Órdenes de depósito / extracción en memoria: registra altas y operaciones para inspeccionarlas en los tests.</summary>
public sealed class FakeOrdenBancariaRepository : IOrdenBancariaRepository
{
    private int _nextId = 600;

    public List<object> Added { get; } = [];
    public List<string> Operaciones { get; } = [];
    public Dictionary<int, BancosCuentas> Cuentas { get; } = [];
    public Dictionary<int, OrdenesDepositos> Depositos { get; } = [];
    public Dictionary<int, OrdenesExrtacciones> Extracciones { get; } = [];

    public T Single<T>() => Added.OfType<T>().Single();
    public IEnumerable<T> All<T>() => Added.OfType<T>();

    public Task<BancosCuentas?> GetCuentaAsync(int idBancoCuenta, CancellationToken cancellationToken = default) =>
        Task.FromResult(Cuentas.GetValueOrDefault(idBancoCuenta));

    public Task<OrdenesDepositos?> GetDepositoAsync(int idOrdenDeposito, CancellationToken cancellationToken = default) =>
        Task.FromResult(Depositos.GetValueOrDefault(idOrdenDeposito));

    public Task<OrdenesExrtacciones?> GetExtraccionAsync(int idOrdenExtraccion, CancellationToken cancellationToken = default) =>
        Task.FromResult(Extracciones.GetValueOrDefault(idOrdenExtraccion));

    public Task<bool> AnularDepositoAsync(int idOrdenDeposito, int estado, int estadoEsperado, DateTime ahora, CancellationToken cancellationToken = default)
    {
        if (Depositos.GetValueOrDefault(idOrdenDeposito) is not { } o || o.Estado != estadoEsperado) return Task.FromResult(false);
        o.Estado = estado;
        o.Total = 0;
        Operaciones.Add($"AnularDeposito {idOrdenDeposito}");
        return Task.FromResult(true);
    }

    public Task<bool> AnularExtraccionAsync(int idOrdenExtraccion, int estado, int estadoEsperado, DateTime ahora, CancellationToken cancellationToken = default)
    {
        if (Extracciones.GetValueOrDefault(idOrdenExtraccion) is not { } o || o.Estado != estadoEsperado) return Task.FromResult(false);
        o.Estado = estado;
        o.Total = 0;
        Operaciones.Add($"AnularExtraccion {idOrdenExtraccion}");
        return Task.FromResult(true);
    }

    public Task AnularDetalleDepositoAsync(int idOrdenDeposito, DateTime ahora, CancellationToken cancellationToken = default) =>
        Op($"AnularDetalleDeposito {idOrdenDeposito}");

    public Task AnularDetalleExtraccionAsync(int idOrdenExtraccion, DateTime ahora, CancellationToken cancellationToken = default) =>
        Op($"AnularDetalleExtraccion {idOrdenExtraccion}");

    public void Add<TEntity>(TEntity entity) where TEntity : class => Added.Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var e in Added)
        {
            switch (e)
            {
                case OrdenesDepositos o when o.IdOrdenDepostio == 0: o.IdOrdenDepostio = _nextId++; break;
                case OrdenesExrtacciones o when o.IdOrdenExtraccion == 0: o.IdOrdenExtraccion = _nextId++; break;
                case BancosCuentasMovimientos m when m.IdBancoCuentaMovimiento == 0: m.IdBancoCuentaMovimiento = _nextId++; break;
            }
        }
        return Task.CompletedTask;
    }

    private Task Op(string descripcion)
    {
        Operaciones.Add(descripcion);
        return Task.CompletedTask;
    }
}
