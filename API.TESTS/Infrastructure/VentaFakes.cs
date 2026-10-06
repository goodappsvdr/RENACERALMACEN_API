using API.DA.Entities;
using API.SERVICE.Interfaces.Ventas;

namespace API.TESTS.Infrastructure;

/// <summary>
/// Repositorio de ventas en memoria. Comparte la cta. cte. con el fake de recibos: lo que graba la venta
/// es visible para el recibo del cobro en el momento, como en la misma transacción real.
/// </summary>
public sealed class FakeVentaRepository(FakeReciboCobroRepository recibos) : IVentaRepository
{
    private int _nextId = 500;

    public List<object> Added { get; } = [];
    public List<string> Operaciones { get; } = [];

    public decimal SaldoCtaCte { get; set; }
    public OfertaActivaRow? Oferta { get; set; }
    public HashSet<int> ConPendienteRemitar { get; } = [];
    public Dictionary<int, DocumentosCliente> Documentos { get; } = [];
    public List<DocumentosClienteDetalle> Detalles { get; } = [];
    public List<DocumentosClienteRemitos> RemitosAsociados { get; } = [];
    public List<EntidadesCtaCteStockMovimientosDetalle> MovimientosStock { get; } = [];
    public Dictionary<int, decimal> SaldosStock { get; } = [];
    public int? ReciboImputado { get; set; }

    public T Single<T>() => Added.OfType<T>().Single();
    public IEnumerable<T> All<T>() => Added.OfType<T>();

    public Task<decimal> GetSaldoCtaCteAsync(int idEntidad, CancellationToken cancellationToken = default) => Task.FromResult(SaldoCtaCte);

    public Task<OfertaActivaRow?> GetOfertaActivaAsync(int idSucursal, int idItem, CancellationToken cancellationToken = default) => Task.FromResult(Oferta);

    public Task<bool> TienePendienteRemitarAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        Task.FromResult(ConPendienteRemitar.Contains(idDocumentoCliente));

    public Task<DocumentosCliente?> GetDocumentoAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        Task.FromResult(Documentos.GetValueOrDefault(idDocumentoCliente));

    public Task<List<DocumentosClienteDetalle>> GetDetallesAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        Task.FromResult(Detalles.Where(d => d.IdDocumentoCliente == idDocumentoCliente).ToList());

    public Task<List<DocumentosClienteRemitos>> GetRemitosAsociadosAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        Task.FromResult(RemitosAsociados.Where(r => r.IdDocumentoCliente == idDocumentoCliente).ToList());

    public Task<List<EntidadesCtaCteStockMovimientosDetalle>> GetMovimientosStockAsync(int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        Task.FromResult(MovimientosStock.Where(m => m.IdComprobante == idComprobante && m.IdComprobanteTipo == idComprobanteTipo).ToList());

    public Task<decimal> GetSaldoStockAsync(int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaldosStock.GetValueOrDefault(idComprobante));

    public Task<int?> GetReciboImputadoAsync(int idDocumentoCliente, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReciboImputado);

    public void Add<TEntity>(TEntity entity) where TEntity : class => Added.Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var e in Added)
        {
            switch (e)
            {
                case DocumentosCliente d when d.IdDocumentoCliente == 0:
                    d.IdDocumentoCliente = _nextId++;
                    Documentos[d.IdDocumentoCliente] = d; // visible para lecturas posteriores (fase 2 de AFIP)
                    break;
                case DocumentosClienteDetalle d when d.IdDocumentoClienteDetalle == 0 && !Detalles.Contains(d):
                    d.IdDocumentoClienteDetalle = _nextId++;
                    Detalles.Add(d);
                    break;
                case EntidadesCtaCte c when c.IdEntidadCtaCte == 0:
                    c.IdEntidadCtaCte = _nextId++;
                    recibos.CtaCte.Add(c);
                    break;
            }
        }
        return Task.CompletedTask;
    }

    public Task RestarStockAsync(int idItem, int idSucursal, decimal cantidad, DateTime ahora, CancellationToken cancellationToken = default) =>
        Op($"RestarStock {idItem}@{idSucursal} {cantidad:0.###}");

    public Task SumarStockAsync(int idItem, int idSucursal, decimal cantidad, DateTime ahora, CancellationToken cancellationToken = default) =>
        Op($"SumarStock {idItem}@{idSucursal} {cantidad:0.###}");

    public Task AjustarSaldoStockAsync(int idComprobante, int idComprobanteDetalle, int idComprobanteTipo, int idItem, decimal delta, CancellationToken cancellationToken = default) =>
        Op($"AjustarSaldoStock {idComprobante}/{idComprobanteDetalle}/{idComprobanteTipo} item={idItem} {delta:0.###}");

    public Task AnularMovimientosStockAsync(int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        Op($"AnularMovimientosStock {idComprobante}/{idComprobanteTipo}");

    public Task AjustarOfertaDisponibleAsync(int idOferta, decimal delta, CancellationToken cancellationToken = default) =>
        Op($"AjustarOferta {idOferta} {delta:0.###}");

    public Task AsignarNroSerieAsync(int idItemNroSerie, int idComprobanteTipo, int idComprobante, long idDetalle, int estado, CancellationToken cancellationToken = default) =>
        Op($"AsignarNroSerie {idItemNroSerie} -> {idComprobante}/{idDetalle}");

    public Task LiberarNrosSerieAsync(int idComprobanteTipo, int idComprobante, int estado, CancellationToken cancellationToken = default) =>
        Op($"LiberarNrosSerie {idComprobanteTipo}/{idComprobante}");

    public Task SetEstadoPendienteAsync(int idDocumentoCliente, int estado, bool pendiente, CancellationToken cancellationToken = default) =>
        Op($"EstadoPendiente {idDocumentoCliente}={estado} pendiente={pendiente}");

    public Task DeterminarRemitarFacturarAsync(int idDocumentoCliente, bool remitar, bool facturar, bool pendiente, CancellationToken cancellationToken = default) =>
        Op($"DeterminarRemitar {idDocumentoCliente}");

    public Task AnularDetalleAsync(long idDetalle, int estadoLibroIva, CancellationToken cancellationToken = default) =>
        Op($"AnularDetalle {idDetalle}");

    public Task BorrarRemitoAsociadoAsync(int idDocumentoClienteRemito, CancellationToken cancellationToken = default) =>
        Op($"BorrarRemitoAsociado {idDocumentoClienteRemito}");

    public Task AnularDocumentoAsync(int idDocumentoCliente, int estado, DateTime ahora, CancellationToken cancellationToken = default) =>
        Op($"AnularDocumento {idDocumentoCliente}={estado}");

    // ---------- Factura electrónica ----------

    public Dictionary<int, Sucursales> Sucursales { get; } = new()
    {
        [1] = new Sucursales { IdSucursal = 1, Cuit = "30712345678", PuntoVentaAfip = "2", IdCategoriaIva = FakeReferencias.CategoriaResponsableInscripto },
    };

    public Task<Sucursales?> GetSucursalAsync(int idSucursal, CancellationToken cancellationToken = default) =>
        Task.FromResult(Sucursales.GetValueOrDefault(idSucursal));

    public Task ModificarDatosAfipAsync(int idDocumentoCliente, string puntoVenta, string numero, string cae, string codigoBarras, CancellationToken cancellationToken = default)
    {
        // Refleja el cambio en el documento, como lo vería una lectura posterior.
        var doc = Documentos.GetValueOrDefault(idDocumentoCliente) ?? Added.OfType<DocumentosCliente>().FirstOrDefault(d => d.IdDocumentoCliente == idDocumentoCliente);
        if (doc is not null)
        {
            doc.PuntoVenta = puntoVenta;
            doc.Numero = numero;
            doc.Cae = cae;
            doc.BarCode = codigoBarras;
        }
        return Op($"DatosAfip {idDocumentoCliente} {puntoVenta}-{numero} CAE={cae} BAR={codigoBarras}");
    }

    public Task<List<DocumentosCliente>> GetPendientesAfipAsync(int idComprobanteTipo, int estadoAnulado, CancellationToken cancellationToken = default) =>
        Task.FromResult(Documentos.Values.Where(d => d.IdComprobanteTipo == idComprobanteTipo && d.Cae == "0" && d.Estado != estadoAnulado).ToList());

    // ---------- Nota de crédito ----------

    public List<int> RecibosImputados { get; } = [];
    public decimal TotalNotasCreditoPrevias { get; set; }

    public Task<List<int>> GetRecibosImputadosAsync(int idDocumentoCliente, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        Task.FromResult(RecibosImputados.ToList());

    public Task<decimal> GetTotalNotasCreditoAsync(int idFactura, int idTipoNotaCredito, int estadoAnulado, int excluirId, CancellationToken cancellationToken = default) =>
        Task.FromResult(TotalNotasCreditoPrevias);

    public Task<int?> GetFacturaDeNotaCreditoAsync(int idNotaCredito, CancellationToken cancellationToken = default) =>
        Task.FromResult(Added.OfType<DocumentosClienteRelacion>().LastOrDefault(r => r.IdDocumentoCliente2 == idNotaCredito)?.IdDocumentoCliente1);

    public Task BorrarRelacionAsync(int idFactura, int idNotaCredito, CancellationToken cancellationToken = default)
    {
        Added.RemoveAll(e => e is DocumentosClienteRelacion r && r.IdDocumentoCliente1 == idFactura && r.IdDocumentoCliente2 == idNotaCredito);
        return Op($"BorrarRelacion {idFactura}/{idNotaCredito}");
    }

    // ---------- Remitos ----------

    public string? Letra { get; set; } = "R";
    public Dictionary<int, List<LineaPendienteRow>> LineasPendientes { get; } = [];

    public Task<string?> GetLetraAsync(int idComprobanteTipo, int idCategoriaIvaCliente, CancellationToken cancellationToken = default) =>
        Task.FromResult(Letra);

    public Task<List<DocumentosClienteRemitos>> GetRelacionesComoRemitoAsync(int idRemito, CancellationToken cancellationToken = default) =>
        Task.FromResult(RemitosAsociados.Where(r => r.IdRemito == idRemito).ToList());

    public Task<List<DocumentosCliente>> GetComprobantesParaRemitirAsync(
        int idEntidad, IReadOnlyCollection<int> tipos, IReadOnlyCollection<int> estadosExcluidos, CancellationToken cancellationToken = default) =>
        Task.FromResult(Documentos.Values
            .Where(d => d.IdCliente == idEntidad && tipos.Contains(d.IdComprobanteTipo ?? 0) && d.Remitar == true && d.Pendiente == true && !estadosExcluidos.Contains(d.Estado ?? 0))
            .OrderByDescending(d => d.IdDocumentoCliente).ToList());

    public Task<List<LineaPendienteRow>> GetLineasPendientesAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        Task.FromResult(LineasPendientes.GetValueOrDefault(idDocumentoCliente) ?? []);

    public Task<IAsyncDisposable> BloquearAutorizacionAsync(int idDocumentoCliente, CancellationToken cancellationToken = default)
    {
        Operaciones.Add($"BloquearAutorizacion {idDocumentoCliente}");
        return Task.FromResult<IAsyncDisposable>(new Liberar(() => Operaciones.Add($"LiberarAutorizacion {idDocumentoCliente}")));
    }

    private sealed class Liberar(Action alLiberar) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            alLiberar();
            return ValueTask.CompletedTask;
        }
    }

    private Task Op(FormattableString descripcion)
    {
        Operaciones.Add(FormattableString.Invariant(descripcion));
        return Task.CompletedTask;
    }
}
