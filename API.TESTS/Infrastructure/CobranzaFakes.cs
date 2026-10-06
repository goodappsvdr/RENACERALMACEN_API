using API.DA.Entities;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;

namespace API.TESTS.Infrastructure;

/// <summary>Transacción que ejecuta el delegado tal cual.</summary>
public sealed class InlineUnitOfWork : IUnitOfWork
{
    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) =>
        operation(cancellationToken);
}

public sealed class FixedServerClock(DateTime now) : IServerClock
{
    public Task<DateTime> GetNowAsync(CancellationToken cancellationToken = default) => Task.FromResult(now);
}

/// <summary>
/// Valores de referencia de prueba. Los IDs de comprobantes / elementos siguen los parámetros de un ERP típico;
/// los estados se numeran automáticamente y se pueden consultar con <see cref="Estado"/>.
/// </summary>
public sealed class FakeReferencias : IReferenciasRepository
{
    public const int Rec = 7, Ven = 11, Fv = 3, Op = 12, Nc = 4, Fc = 20, Com = 21;
    public const int Efectivo = 1, Cheque = 2, Deposito = 3, Tarjeta = 8, Retencion = 5;
    public const int ChequeAutomatico = 900;

    private readonly Dictionary<string, int> _estados = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<(string, string), string?> Parametros { get; } = new()
    {
        [("COMPROBANTE", "REC")] = Rec.ToString(),
        [("COMPROBANTE", "VEN")] = Ven.ToString(),
        [("COMPROBANTE", "FV")] = Fv.ToString(),
        [("COMPROBANTE", "OP")] = Op.ToString(),
        [("COMPROBANTE", "NC")] = Nc.ToString(),
        [("COMPROBANTE", "FC")] = Fc.ToString(),
        [("COMPROBANTE", "COM")] = Com.ToString(),
        [("ELEMENTO", "EFECTIVO")] = Efectivo.ToString(),
        [("ELEMENTO", "CHEQUE")] = Cheque.ToString(),
        [("ELEMENTO", "DEPOSITO BANCARIO")] = Deposito.ToString(),
        [("ELEMENTO", "TARJETAS")] = Tarjeta.ToString(),
        [("ELEMENTO", "RETENCION")] = Retencion.ToString(),
        [("NUMERACION", "REC")] = "0",
    };

    public int Estado((string Categoria, string Nombre) estado) => Estado(estado.Categoria, estado.Nombre);

    public int Estado(string categoria, string nombre)
    {
        var key = $"{categoria}|{nombre}";
        if (!_estados.TryGetValue(key, out var id))
            _estados[key] = id = 1000 + _estados.Count;
        return id;
    }

    public Task<string?> GetParametroAsync(string categoria, string nombre, CancellationToken cancellationToken = default) =>
        Task.FromResult(Parametros.GetValueOrDefault((categoria, nombre)));

    public Task<int> GetParametroEnteroAsync(string categoria, string nombre, CancellationToken cancellationToken = default) =>
        Task.FromResult(int.Parse(Parametros[(categoria, nombre)]!));

    public Task<int> GetIdEstadoAsync(string categoria, string nombre, CancellationToken cancellationToken = default) =>
        Task.FromResult(Estado(categoria, nombre));

    public Task<int> GetIdCategoriaAsync(string categoriaTipo, string nombre, CancellationToken cancellationToken = default) =>
        Task.FromResult(ChequeAutomatico);
}

/// <summary>Repositorio de recibos en memoria: registra altas y operaciones para inspeccionarlas en los tests.</summary>
public sealed class FakeReciboCobroRepository : IReciboCobroRepository
{
    private int _nextId = 100;

    public List<object> Added { get; } = [];
    public List<string> Operaciones { get; } = [];
    public List<EntidadesCtaCte> CtaCte { get; } = [];
    public List<ImputacionRow> Imputaciones { get; } = [];
    public HashSet<int> ConRelacion { get; } = [];
    public HashSet<int> ConOtrasImputaciones { get; } = [];
    public Dictionary<int, int> EstadosDocumentoCliente { get; } = [];
    public Dictionary<int, int> EstadosRecibo { get; } = [];
    public long? Numerador { get; set; } = 41;

    public EntidadesRecibos? Recibo { get; set; }
    public CajaPlanillas? Planilla { get; set; } = new() { IdPlanillaCaja = 77, PuntoVenta = "0003" };
    public int? SucursalLocal { get; set; } = 2;
    public List<SaldoEntidadRow> SaldosGrilla { get; } = [];
    public List<int> Bloqueos { get; } = [];

    public Dictionary<int, Entidades> Entidades { get; } = new()
    {
        [5] = new() { IdEntidad = 5, RazonSocial = "ALMACEN DON PEPE", IdCategoriaIva = 2, Cuit = "20123456789" },
    };

    public T Single<T>() => Added.OfType<T>().Single();
    public IEnumerable<T> All<T>() => Added.OfType<T>();

    public Task<CajaPlanillas?> GetPlanillaAbiertaAsync(int idUsuario, int idComprobanteTipo, string letra, int estadoAbierta, CancellationToken cancellationToken = default) =>
        Task.FromResult(Planilla);

    public Task<long?> GetProximoNumeroAsync(string puntoVenta, string letra, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        Task.FromResult(Numerador + 1);

    public Task<Entidades?> GetEntidadAsync(int idEntidad, CancellationToken cancellationToken = default) =>
        Task.FromResult(Entidades.GetValueOrDefault(idEntidad));

    public Task<List<SaldoEntidadRow>> GetSaldosRecibosAutomaticosAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SaldosGrilla.ToList());

    public Task<int?> GetIdSucursalLocalAsync(int idUsuario, CancellationToken cancellationToken = default) =>
        Task.FromResult(SucursalLocal);

    public Task BloquearEntidadAsync(int idEntidad, CancellationToken cancellationToken = default)
    {
        Bloqueos.Add(idEntidad);
        return Task.CompletedTask;
    }

    public Task<List<EntidadesCtaCte>> GetCtaCtePendienteAsync(int idEntidad, int idComprobante, int idComprobanteTipo, CancellationToken cancellationToken = default) =>
        Task.FromResult(CtaCte.Where(c => c.IdEntidad == idEntidad && c.IdComprobante == idComprobante && c.IdComprobanteTipo == idComprobanteTipo && c.Cancelado == false).ToList());

    public Task<List<ComprobantePendienteRow>> GetComprobantesPendientesAsync(int idEntidad, CancellationToken cancellationToken = default) =>
        Task.FromResult(CtaCte
            .Where(c => c.IdEntidad == idEntidad && c.Cancelado == false)
            .OrderBy(c => API.SERVICE.Domain.Cobranzas.ImputacionRules.SaldoVisible(c.IdComprobanteTipo ?? 0, c.Saldo ?? 0))
            .Select(c => new ComprobantePendienteRow(
                c.IdComprobante, c.IdComprobanteTipo, c.IdEntidad, c.Concepto, Entidades.GetValueOrDefault(idEntidad)?.RazonSocial,
                c.Fecha, c.FechaVencimiento, c.Saldo, c.InteresAplicado, null, null))
            .ToList());

    public Task<EntidadesRecibos?> GetReciboAsync(int idRecibo, CancellationToken cancellationToken = default) =>
        Task.FromResult(Recibo?.IdEntidadRecibo == idRecibo ? Recibo : null);

    public Task<List<ImputacionRow>> GetImputacionesAsync(int idRecibo, CancellationToken cancellationToken = default) =>
        Task.FromResult(Imputaciones.ToList());

    public Task<bool> TieneRelacionAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        Task.FromResult(ConRelacion.Contains(idDocumentoCliente));

    public Task<bool> TieneImputacionesAsync(int idDocumentoCliente, CancellationToken cancellationToken = default) =>
        Task.FromResult(ConOtrasImputaciones.Contains(idDocumentoCliente));

    public Task<long?> GetIdCtaCteAsync(int idComprobanteTipo, int idComprobante, CancellationToken cancellationToken = default) =>
        Task.FromResult<long?>(9000);

    public void Add<TEntity>(TEntity entity) where TEntity : class => Added.Add(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Simula las identity de las tablas que el flujo necesita leer después de grabar.
        foreach (var e in Added)
        {
            switch (e)
            {
                case EntidadesRecibos r when r.IdEntidadRecibo == 0: r.IdEntidadRecibo = _nextId++; break;
                case EntidadesCtaCte c when c.IdEntidadCtaCte == 0: c.IdEntidadCtaCte = _nextId++; break;
            }
        }
        Operaciones.Add("SaveChanges");
        return Task.CompletedTask;
    }

    public Task<long?> ReservarNumeroAsync(string puntoVenta, string letra, int idComprobanteTipo, CancellationToken cancellationToken = default)
    {
        Operaciones.Add($"Numerar {puntoVenta}");
        if (Numerador is null) return Task.FromResult<long?>(null);
        Numerador++;
        return Task.FromResult(Numerador);
    }

    public Task ImputarCtaCteAsync(int idComprobante, int idComprobanteTipo, decimal saldo, DateTime fechaPago, bool cancelado, decimal interesAplicado, CancellationToken cancellationToken = default) =>
        Op($"ImputarCtaCte {idComprobante}/{idComprobanteTipo} saldo={saldo:0.00} cancelado={cancelado} interes={interesAplicado:0.00} fecha={fechaPago:yyyy-MM-dd}");

    public Task SetEstadoDocumentoClienteAsync(int idDocumentoCliente, int estado, CancellationToken cancellationToken = default)
    {
        EstadosDocumentoCliente[idDocumentoCliente] = estado;
        return Op($"EstadoDocumentoCliente {idDocumentoCliente}={estado}");
    }

    public Task SetEstadoDocumentoProveedorAsync(int idDocumentoProveedor, int estado, CancellationToken cancellationToken = default) =>
        Op($"EstadoDocumentoProveedor {idDocumentoProveedor}={estado}");

    public Task SetEstadoReciboAsync(int idRecibo, int estado, CancellationToken cancellationToken = default)
    {
        EstadosRecibo[idRecibo] = estado;
        return Op($"EstadoRecibo {idRecibo}={estado}");
    }

    public Task SetEstadoOrdenPagoAsync(int idProveedorRecibo, int estado, CancellationToken cancellationToken = default) =>
        Op($"EstadoOrdenPago {idProveedorRecibo}={estado}");

    public Task AnularDetalleAsync(int idRecibo, DateTime ahora, CancellationToken cancellationToken = default) => Op($"AnularDetalle {idRecibo}");

    public Task AnularCajaAsync(int idComprobanteTipo, int idComprobante, CancellationToken cancellationToken = default) => Op($"AnularCaja {idComprobanteTipo}/{idComprobante}");

    public Task AnularChequesAsync(int idRecibo, int idComprobanteTipo, int estadoAnulado, CancellationToken cancellationToken = default) => Op($"AnularCheques {idRecibo}");

    public Task AnularMovimientosBancoAsync(int idComprobanteTipo, int idComprobante, int estadoAnulado, DateTime ahora, CancellationToken cancellationToken = default) => Op($"AnularBancos {idComprobante}");

    public Task AnularRetencionesAsync(int idComprobanteTipo, int idComprobante, int estadoAnulado, CancellationToken cancellationToken = default) => Op($"AnularRetenciones {idComprobante}");

    public Task RevertirImputacionCtaCteAsync(int idComprobante, int idComprobanteTipo, decimal saldo, decimal interesAplicado, DateTime ahora, CancellationToken cancellationToken = default) =>
        Op($"RevertirCtaCte {idComprobante}/{idComprobanteTipo} saldo={saldo:0.00} interes={interesAplicado:0.00}");

    public Task BorrarImputacionesAsync(int idRecibo, CancellationToken cancellationToken = default) => Op($"BorrarImputaciones {idRecibo}");

    public Task AnularCtaCteAsync(long idEntidadCtaCte, int estadoAnulado, DateTime ahora, CancellationToken cancellationToken = default) => Op($"AnularCtaCte {idEntidadCtaCte}");

    private Task Op(FormattableString descripcion)
    {
        Operaciones.Add(FormattableString.Invariant(descripcion));
        return Task.CompletedTask;
    }
}
