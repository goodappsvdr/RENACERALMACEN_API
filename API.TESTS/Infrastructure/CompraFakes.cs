using API.DA.Entities;
using API.SERVICE.Interfaces.Compras;

namespace API.TESTS.Infrastructure;

/// <summary>Repositorio de compras en memoria. Las altas de stock / cta. cte. quedan en <see cref="Added"/>, las actualizaciones en <see cref="Operaciones"/>.</summary>
public sealed class FakeCompraRepository : ICompraRepository
{
    private int _nextId = 700;

    public List<object> Added { get; } = [];
    public List<string> Operaciones { get; } = [];
    public Dictionary<int, DocumentosProveedor> Documentos { get; } = [];
    public List<DocumentosProveedorDetalle> Detalles { get; } = [];
    public Dictionary<int, List<LineaPendienteCompraRow>> LineasPendientes { get; } = [];
    public List<DocumentosProveedorRemitos> Relaciones { get; } = [];
    public Dictionary<(int Tipo, int Id), EntidadesCtaCte> CtaCte { get; } = [];
    public List<string> Letras { get; } = ["A"];
    public CajaPlanillas? Planilla { get; set; } = new() { IdPlanillaCaja = 77, PuntoVenta = "0003" };
    public HashSet<(int Proveedor, string PuntoVenta, string Numero)> Registradas { get; } = [];

    public T Single<T>() => Added.OfType<T>().Single();
    public IEnumerable<T> All<T>() => Added.OfType<T>();

    public Task<DocumentosProveedor?> GetDocumentoAsync(int idDocumentoProveedor, CancellationToken cancellationToken = default) =>
        Task.FromResult(Documentos.GetValueOrDefault(idDocumentoProveedor));

    public Task<List<DocumentosProveedorDetalle>> GetDetallesAsync(int idDocumentoProveedor, CancellationToken cancellationToken = default) =>
        Task.FromResult(Detalles.Where(d => d.IdDocumentoProveedor == idDocumentoProveedor).ToList());

    public Task<bool> ExisteDuplicadoAsync(int idProveedor, int idComprobanteTipo, string puntoVenta, string numero, int estadoAnulado, CancellationToken cancellationToken = default) =>
        Task.FromResult(Registradas.Contains((idProveedor, puntoVenta, numero)));

    public Task<List<string>> GetLetrasAsync(int idComprobanteTipo, int idCategoriaIvaEmpresa, int idCategoriaIvaProveedor, CancellationToken cancellationToken = default) =>
        Task.FromResult(Letras.ToList());

    public Task<CajaPlanillas?> GetPlanillaAbiertaAsync(int idUsuario, int estadoAbierta, CancellationToken cancellationToken = default) =>
        Task.FromResult(Planilla);

    public Task<List<DocumentosProveedor>> GetComprobantesConPendienteAsync(
        int idProveedor, IReadOnlyCollection<int> tipos, IReadOnlyCollection<int> estadosExcluidos, int? idSucursal, CancellationToken cancellationToken = default) =>
        Task.FromResult(Documentos.Values
            .Where(d => d.IdProveedor == idProveedor && tipos.Contains(d.IdComprobanteTipo ?? 0) && !estadosExcluidos.Contains(d.Estado ?? 0)
                        && (idSucursal == null || d.IdSucursal == idSucursal) && LineasPendientes.GetValueOrDefault(d.IdDocumentoProveedor)?.Count > 0)
            .ToList());

    public Task<List<LineaPendienteCompraRow>> GetLineasPendientesAsync(int idDocumentoProveedor, IReadOnlyCollection<int> tipos, CancellationToken cancellationToken = default) =>
        Task.FromResult(LineasPendientes.GetValueOrDefault(idDocumentoProveedor) ?? []);

    public Task<List<DocumentosProveedorRemitos>> GetRelacionesComoDestinoAsync(int idDocumentoProveedor, CancellationToken cancellationToken = default) =>
        Task.FromResult(Relaciones.Where(r => r.IdRemito == idDocumentoProveedor).ToList());

    public Task BorrarRelacionAsync(int idDocumentoProveedorRemito, CancellationToken cancellationToken = default) => Op($"BorrarRelacion {idDocumentoProveedorRemito}");

    public Task SetEstadoPendienteAsync(int idDocumentoProveedor, int estado, bool pendiente, CancellationToken cancellationToken = default) =>
        Op($"EstadoPendiente {idDocumentoProveedor}={estado} pendiente={pendiente}");

    public Task DeterminarRemitarFacturarAsync(int idDocumentoProveedor, bool remitar, bool facturar, bool pendiente, CancellationToken cancellationToken = default) =>
        Op($"Determinar {idDocumentoProveedor} remitar={remitar} facturar={facturar} pendiente={pendiente}");

    public Task AnularDocumentoAsync(int idDocumentoProveedor, int estado, DateTime ahora, CancellationToken cancellationToken = default) =>
        Op($"AnularDocumento {idDocumentoProveedor}={estado}");

    public Task BorrarLibroIvaAsync(int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        Op($"BorrarLibroIva {idComprobante}/{idComprobanteTipo}");

    public Task BorrarOtrosTributosAsync(int idDocumentoProveedor, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        Op($"BorrarOtrosTributos {idDocumentoProveedor}/{idComprobanteTipo}");

    public Task<EntidadesCtaCte?> GetCtaCteAsync(int idComprobanteTipo, int idComprobante, CancellationToken cancellationToken = default) =>
        Task.FromResult(CtaCte.GetValueOrDefault((idComprobanteTipo, idComprobante)));

    public void Add<TEntity>(TEntity entity) where TEntity : class => Added.Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var e in Added)
        {
            switch (e)
            {
                case DocumentosProveedor d when d.IdDocumentoProveedor == 0:
                    d.IdDocumentoProveedor = _nextId++;
                    Documentos[d.IdDocumentoProveedor] = d;
                    break;
                case DocumentosProveedorDetalle d when d.IdDocumentoProveedorDetalle == 0:
                    d.IdDocumentoProveedorDetalle = _nextId++;
                    Detalles.Add(d);
                    break;
                case EntidadesCtaCte c when c.IdEntidadCtaCte == 0:
                    c.IdEntidadCtaCte = _nextId++;
                    break;
            }
        }
        return Task.CompletedTask;
    }

    private Task Op(FormattableString descripcion)
    {
        Operaciones.Add(FormattableString.Invariant(descripcion));
        return Task.CompletedTask;
    }
}
