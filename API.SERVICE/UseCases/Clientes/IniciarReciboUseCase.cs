using API.DA.Entities;
using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Models.Clientes;

namespace API.SERVICE.UseCases.Clientes;

public interface IIniciarReciboUseCase
{
    Task<NuevoReciboDisplay> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Datos para empezar un recibo (IniciarPuntoVenta_WS): planilla de caja abierta del usuario, punto de venta
/// y número sugerido. El número definitivo se reserva recién al grabar.
/// </summary>
public sealed class IniciarReciboUseCase : IIniciarReciboUseCase
{
    private readonly IReciboCobroRepository _repository;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public IniciarReciboUseCase(IReciboCobroRepository repository, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _repository = repository;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<NuevoReciboDisplay> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var idUsuario = ReciboContexto.RequireIdUsuario(_currentUser);
        var idRecibo = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "REC", cancellationToken);
        var planilla = await ReciboContexto.GetPlanillaAbiertaAsync(_repository, _referencias, idUsuario, idRecibo, cancellationToken);
        var manual = await ReciboContexto.NumeracionManualAsync(_referencias, cancellationToken);

        string? sugerido = null;
        if (!manual)
        {
            var proximo = await _repository.GetProximoNumeroAsync(planilla.PuntoVenta!, ReciboContexto.Letra, idRecibo, cancellationToken);
            sugerido = proximo is null ? null : ReciboContexto.FormatearNumero(proximo.Value);
        }

        return new NuevoReciboDisplay(planilla.IdPlanillaCaja, planilla.PuntoVenta!, ReciboContexto.Letra, manual, sugerido);
    }
}

/// <summary>Reglas comunes de los casos de uso de recibos.</summary>
internal static class ReciboContexto
{
    /// <summary>Los recibos de cobro siempre se emiten con letra X.</summary>
    public const string Letra = "X";

    public static int RequireIdUsuario(ICurrentUser currentUser) =>
        currentUser.IdUsuario
        ?? throw new ForbiddenException("El usuario no tiene un registro en Usuarios del ERP; no puede operar recibos.");

    public static async Task<CajaPlanillas> GetPlanillaAbiertaAsync(
        IReciboCobroRepository repository, IReferenciasRepository referencias, int idUsuario, int idRecibo, CancellationToken ct)
    {
        var abierta = await referencias.IdAsync(EstadosCobranza.PlanillaAbierta, ct);
        return await repository.GetPlanillaAbiertaAsync(idUsuario, idRecibo, Letra, abierta, ct)
            ?? throw new BusinessException("No se pueden generar recibos: no existe una planilla de caja abierta para este usuario.");
    }

    /// <summary>NUMERACION/REC = 1: el usuario carga punto de venta y número a mano.</summary>
    public static async Task<bool> NumeracionManualAsync(IReferenciasRepository referencias, CancellationToken ct) =>
        (await referencias.GetParametroAsync("NUMERACION", "REC", ct))?.Trim() == "1";

    public static string FormatearNumero(long numero) => numero.ToString().PadLeft(8, '0');
}
