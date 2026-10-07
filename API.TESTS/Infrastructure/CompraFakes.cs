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

    public HashSet<int> ConRelacionesComoOrigen { get; } = [];

    public Task SetPendienteAsync(int idDocumentoProveedor, bool pendiente, CancellationToken cancellationToken = default) =>
        Op($"Pendiente {idDocumentoProveedor}={pendiente}");

    public Task<bool> TieneRelacionesComoOrigenAsync(int idDocumentoProveedor, CancellationToken cancellationToken = default) =>
        Task.FromResult(ConRelacionesComoOrigen.Contains(idDocumentoProveedor));

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

/// <summary>Repositorio de órdenes de pago en memoria.</summary>
public sealed class FakeOrdenPagoRepository : IOrdenPagoRepository
{
    private int _nextId = 900;

    public List<object> Added { get; } = [];
    public List<string> Operaciones { get; } = [];
    public Dictionary<int, ProveedoresRecibos> Ordenes { get; } = [];
    public List<EntidadesCtaCte> Pendientes { get; } = [];
    public List<EntidadOrdenPagoDocumentosProveedores> Imputaciones { get; } = [];
    public HashSet<(int Id, int Tipo)> ConOtrasImputaciones { get; } = [];
    public Dictionary<int, EntidadesCheques> ChequesTerceros { get; } = [];
    public Dictionary<int, ChequePropioRow> ChequesPropios { get; } = [];

    public T Single<T>() => Added.OfType<T>().Single();
    public IEnumerable<T> All<T>() => Added.OfType<T>();

    public Task<ProveedoresRecibos?> GetOrdenAsync(int idOrdenPago, CancellationToken cancellationToken = default) =>
        Task.FromResult(Ordenes.GetValueOrDefault(idOrdenPago));

    public Task<List<EntidadesCtaCte>> GetComprobantesPendientesAsync(int idEntidad, int estadoCtaCte, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pendientes.Where(c => c.IdEntidad == idEntidad).ToList());

    public Task<List<EntidadOrdenPagoDocumentosProveedores>> GetImputacionesAsync(int idOrdenPago, CancellationToken cancellationToken = default) =>
        Task.FromResult(Imputaciones.Where(i => i.IdEntidadOrdenPago == idOrdenPago).ToList());

    public Task BorrarImputacionesAsync(int idOrdenPago, CancellationToken cancellationToken = default) => Op($"BorrarImputaciones {idOrdenPago}");

    public Task<bool> TieneImputacionesAsync(int idDocumento, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        Task.FromResult(ConOtrasImputaciones.Contains((idDocumento, idComprobanteTipo)));

    public Task<EntidadesCheques?> GetChequeTerceroAsync(int idEntidadCheque, CancellationToken cancellationToken = default) =>
        Task.FromResult(ChequesTerceros.GetValueOrDefault(idEntidadCheque));

    public Task<bool> AsignarChequeTerceroAsync(int idEntidadCheque, int idOrdenPago, int idComprobanteTipo, int estado, int estadoEsperado, CancellationToken cancellationToken = default)
    {
        if (ChequesTerceros.GetValueOrDefault(idEntidadCheque) is not { } c || c.Estado != estadoEsperado) return Task.FromResult(false);
        c.Estado = estado;
        Operaciones.Add($"EntregarChequeTercero {idEntidadCheque} -> OP {idOrdenPago}");
        return Task.FromResult(true);
    }

    public Task DevolverChequesTercerosAsync(int idOrdenPago, int idComprobanteTipo, int estadoEnCartera, CancellationToken cancellationToken = default) =>
        Op($"DevolverChequesTerceros {idOrdenPago}");

    public Task<ChequePropioRow?> GetChequePropioAsync(int idBancoCheque, CancellationToken cancellationToken = default) =>
        Task.FromResult(ChequesPropios.GetValueOrDefault(idBancoCheque));

    public Task<bool> EntregarChequePropioAsync(
        int idBancoCheque, string nroCheque, int idOrdenPago, int idComprobanteTipo, DateTime fechaEmision, DateTime fechaVencimiento, decimal importe, int estado,
        int estadoEsperado, CancellationToken cancellationToken = default)
    {
        if (ChequesPropios.GetValueOrDefault(idBancoCheque) is not { } c || c.Cheque.Estado != estadoEsperado) return Task.FromResult(false);
        c.Cheque.Estado = estado;
        Operaciones.Add(FormattableString.Invariant($"EntregarChequePropio {idBancoCheque} nro={nroCheque} importe={importe:0.00} -> OP {idOrdenPago}"));
        return Task.FromResult(true);
    }

    public Task AnularChequesPropiosAsync(int idOrdenPago, int idComprobanteTipo, int estado, CancellationToken cancellationToken = default) => Op($"AnularChequesPropios {idOrdenPago}");

    public Task AnularChequesProveedorAsync(int idOrdenPago, int idComprobanteTipo, int estado, CancellationToken cancellationToken = default) => Op($"AnularChequesProveedor {idOrdenPago}");

    public Task AnularDetalleAsync(int idOrdenPago, DateTime ahora, CancellationToken cancellationToken = default) => Op($"AnularDetalle {idOrdenPago}");

    public void Add<TEntity>(TEntity entity) where TEntity : class => Added.Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var e in Added)
        {
            switch (e)
            {
                case ProveedoresRecibos o when o.IdProveedorRecibo == 0: o.IdProveedorRecibo = _nextId++; break;
                case EntidadesCtaCte c when c.IdEntidadCtaCte == 0: c.IdEntidadCtaCte = _nextId++; break;
                case ProveedoresCheques c when c.IdProveedorCheque == 0: c.IdProveedorCheque = _nextId++; break;
                case BancosCuentasMovimientos m when m.IdBancoCuentaMovimiento == 0: m.IdBancoCuentaMovimiento = _nextId++; break;
                case Retenciones r when r.IdRetencion == 0: r.IdRetencion = _nextId++; break;
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
