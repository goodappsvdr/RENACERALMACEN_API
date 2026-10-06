using API.SERVICE.Domain;
using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Models.Clientes;
using Microsoft.Extensions.Logging;

namespace API.SERVICE.UseCases.Clientes;

public interface IGetEntidadesRecibosAutomaticosUseCase
{
    Task<IReadOnlyList<EntidadReciboAutomaticoDisplay>> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>Grilla de la emisión masiva (EntidadesPrincipales_BuscarTodos de FrmRecibosAutomaticos).</summary>
public sealed class GetEntidadesRecibosAutomaticosUseCase : IGetEntidadesRecibosAutomaticosUseCase
{
    private readonly IReciboCobroRepository _repository;

    public GetEntidadesRecibosAutomaticosUseCase(IReciboCobroRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<EntidadReciboAutomaticoDisplay>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        (await _repository.GetSaldosRecibosAutomaticosAsync(cancellationToken))
            .Select(s => new EntidadReciboAutomaticoDisplay(s.IdEntidad, s.RazonSocial, ImputacionRules.Redondear(s.Saldo)))
            .ToList();
}

public interface IGenerarRecibosAutomaticosUseCase
{
    Task<IReadOnlyList<ReciboAutomaticoResultado>> ExecuteAsync(GenerarRecibosAutomaticosDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Emisión masiva de recibos (GenerarRecibos_WS de FrmRecibosAutomaticos): por cada cliente, un recibo en efectivo
/// que cancela TODOS sus comprobantes pendientes con interés 0. Cada recibo se graba con el mismo caso de uso que el
/// recibo manual (<see cref="ICreateReciboUseCase"/>), en su propia transacción: si un cliente falla, se informa y se
/// sigue con los demás.
/// </summary>
public sealed class GenerarRecibosAutomaticosUseCase : IGenerarRecibosAutomaticosUseCase
{
    private const string Observaciones = "RECIBO AUTOMATICO";
    private const string DescripcionEfectivo = "EFECTIVO";
    private const string NumeroEfectivo = "0000000000";

    private readonly IReciboCobroRepository _repository;
    private readonly IReferenciasRepository _referencias;
    private readonly IGetComprobantesPendientesUseCase _pendientes;
    private readonly ICreateReciboUseCase _crearRecibo;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<GenerarRecibosAutomaticosUseCase> _logger;

    public GenerarRecibosAutomaticosUseCase(
        IReciboCobroRepository repository,
        IReferenciasRepository referencias,
        IGetComprobantesPendientesUseCase pendientes,
        ICreateReciboUseCase crearRecibo,
        IServerClock clock,
        ICurrentUser currentUser,
        ILogger<GenerarRecibosAutomaticosUseCase> logger)
    {
        _repository = repository;
        _referencias = referencias;
        _pendientes = pendientes;
        _crearRecibo = crearRecibo;
        _clock = clock;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ReciboAutomaticoResultado>> ExecuteAsync(GenerarRecibosAutomaticosDto dto, CancellationToken cancellationToken = default)
    {
        var ids = dto.IdsEntidad.Distinct().ToList();
        if (ids.Count == 0)
            throw new BusinessException("No hay entidades seleccionadas.");
        if (ids.Count > GenerarRecibosAutomaticosDto.MaximoPorTanda)
            throw new BusinessException($"Se pueden generar hasta {GenerarRecibosAutomaticosDto.MaximoPorTanda} recibos por vez.");

        // Lo que no depende del cliente se valida una sola vez, antes de grabar nada (mismo orden que el ERP).
        var idUsuario = ReciboContexto.RequireIdUsuario(_currentUser);
        var codigos = await CodigosCobranza.LoadAsync(_referencias, cancellationToken);
        var idSucursal = await _repository.GetIdSucursalLocalAsync(idUsuario, cancellationToken)
            ?? throw new BusinessException("El usuario no tiene habilitada la sucursal LOCAL.");
        await ReciboContexto.GetPlanillaAbiertaAsync(_repository, _referencias, idUsuario, codigos.Rec, cancellationToken);
        if (await ReciboContexto.NumeracionManualAsync(_referencias, cancellationToken))
            throw new BusinessException("La numeración de recibos está configurada como manual: no se pueden generar recibos automáticos.");

        var fecha = (await _clock.GetNowAsync(cancellationToken)).Date;

        // Saldo de cada cliente según la grilla, recalculado acá: no se confía en lo que manda la pantalla.
        var saldosGrilla = (await _repository.GetSaldosRecibosAutomaticosAsync(cancellationToken))
            .ToDictionary(s => s.IdEntidad, s => ImputacionRules.Redondear(s.Saldo));

        var resultados = new List<ReciboAutomaticoResultado>();
        foreach (var idEntidad in ids)
            resultados.Add(await GenerarAsync(idEntidad, codigos, saldosGrilla, idSucursal, fecha, cancellationToken));

        return resultados;
    }

    private async Task<ReciboAutomaticoResultado> GenerarAsync(
        int idEntidad, CodigosCobranza codigos, IReadOnlyDictionary<int, decimal> saldosGrilla, int idSucursal, DateTime fecha, CancellationToken ct)
    {
        string? razonSocial = null;
        ReciboAutomaticoResultado Fallo(string mensaje) => new(idEntidad, razonSocial, false, null, null, 0, mensaje);

        try
        {
            var entidad = await _repository.GetEntidadAsync(idEntidad, ct);
            if (entidad is null)
                return Fallo("No se encontró la entidad.");
            razonSocial = entidad.RazonSocial;

            if (!saldosGrilla.TryGetValue(idEntidad, out var saldoGrilla))
                return Fallo("Ya no tiene saldo a cobrar.");

            // Los mismos comprobantes que trae "comprobantes pendientes" en el recibo manual.
            var pendientes = await _pendientes.ExecuteAsync(idEntidad, ct);
            foreach (var p in pendientes)
            {
                var tipo = codigos.Clasificar(p.IdComprobanteTipo);
                if (tipo is TipoImputacion.OrdenPago or TipoImputacion.Compra)
                    return Fallo($"Tiene comprobantes de proveedor pendientes ({p.Comprobante}): hacer el recibo a mano.");
                if (tipo is TipoImputacion.NoSoportado)
                    return Fallo($"Tiene un tipo de comprobante que el recibo automático no sabe cancelar ({p.Comprobante}).");
            }

            if (pendientes.Count == 0)
                return Fallo("No tiene comprobantes pendientes.");

            var total = pendientes.Sum(p => p.Saldo);
            if (total != saldoGrilla)
                return Fallo($"No coincide: saldo en la grilla {Formato.Importe(saldoGrilla)}, comprobantes pendientes {Formato.Importe(total)}.");
            if (total <= 0)
                return Fallo("El saldo no es positivo.");

            // Primero lo que está a favor del cliente (recibos previos, notas de crédito) y después las facturas:
            // así el importe del recibo nunca queda corto y ninguna factura queda cobrada parcial.
            var ordenados = pendientes.Where(p => p.Saldo < 0).Concat(pendientes.Where(p => p.Saldo >= 0));

            var recibo = await _crearRecibo.ExecuteAsync(new CreateReciboDto
            {
                IdEntidad = idEntidad,
                FechaEmision = fecha,
                IdSucursal = idSucursal,
                Observaciones = Observaciones,
                Imputaciones = ordenados.Select(p => new ImputacionDto
                {
                    IdComprobante = p.IdComprobante,
                    IdComprobanteTipo = p.IdComprobanteTipo,
                    ImporteComprobante = p.Saldo,
                    InteresAplicado = 0,
                }).ToList(),
                Elementos =
                [
                    new ElementoCobroDto
                    {
                        IdElementoCobro = codigos.Efectivo ?? throw new BusinessException("No está configurado el parámetro ELEMENTO/EFECTIVO."),
                        Importe = total,
                        Descripcion = DescripcionEfectivo,
                        Numero = NumeroEfectivo,
                    },
                ],
            }, ct);

            return new ReciboAutomaticoResultado(
                idEntidad, razonSocial, true, recibo.IdEntidadRecibo, $"{recibo.Letra}-{recibo.PuntoVenta}-{recibo.Numero}", recibo.Total ?? total, null);
        }
        catch (BusinessException ex)
        {
            return Fallo(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Un error inesperado de un cliente no corta la tanda; el detalle queda solo en el log.
            _logger.LogError(ex, "Error generando el recibo automático de la entidad {IdEntidad}", idEntidad);
            return Fallo("Error inesperado al generar el recibo.");
        }
    }
}
