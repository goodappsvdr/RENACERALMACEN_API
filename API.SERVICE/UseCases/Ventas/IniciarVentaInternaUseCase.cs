using API.DA.Entities;
using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Models.Ventas;

namespace API.SERVICE.UseCases.Ventas;

public interface IIniciarVentaInternaUseCase
{
    Task<NuevaVentaInternaDisplay> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>Datos para empezar un comprobante interno (IniciarPuntoVenta_WS de FrmFacturas).</summary>
public sealed class IniciarVentaInternaUseCase : IIniciarVentaInternaUseCase
{
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public IniciarVentaInternaUseCase(IReciboCobroRepository comprobantes, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _comprobantes = comprobantes;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<NuevaVentaInternaDisplay> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);
        var ven = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "VEN", cancellationToken);
        var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, ven, cancellationToken);
        var manual = await VentaContexto.NumeracionManualAsync(_referencias, cancellationToken);

        string? sugerido = null;
        if (!manual)
        {
            var proximo = await _comprobantes.GetProximoNumeroAsync(planilla.PuntoVenta!, VentaRules.LetraInterna, ven, cancellationToken);
            sugerido = proximo?.ToString().PadLeft(8, '0');
        }

        return new NuevaVentaInternaDisplay(planilla.IdPlanillaCaja, planilla.PuntoVenta!, VentaRules.LetraInterna, manual, sugerido);
    }
}

/// <summary>Reglas comunes de los comprobantes de venta internos.</summary>
internal static class VentaContexto
{
    public static int RequireIdUsuario(ICurrentUser currentUser) =>
        currentUser.IdUsuario
        ?? throw new ForbiddenException("El usuario no tiene un registro en Usuarios del ERP; no puede operar comprobantes de venta.");

    public static Task<CajaPlanillas> GetPlanillaAbiertaAsync(
        IReciboCobroRepository comprobantes, IReferenciasRepository referencias, int idUsuario, int idTipo, CancellationToken ct) =>
        GetPlanillaAbiertaAsync(comprobantes, referencias, idUsuario, idTipo, VentaRules.LetraInterna, ct);

    /// <summary>Planilla abierta del usuario cuyo punto de venta emite el tipo y la letra (CajaPlanillas_IniciarPuntoVenta).</summary>
    public static async Task<CajaPlanillas> GetPlanillaAbiertaAsync(
        IReciboCobroRepository comprobantes, IReferenciasRepository referencias, int idUsuario, int idTipo, string letra, CancellationToken ct)
    {
        var abierta = await referencias.IdAsync(EstadosCobranza.PlanillaAbierta, ct);
        return await comprobantes.GetPlanillaAbiertaAsync(idUsuario, idTipo, letra, abierta, ct)
            ?? throw new BusinessException("No se pueden generar comprobantes: no existe una planilla de caja abierta para este usuario.");
    }

    /// <summary>NUMERACION/RV = 1: el usuario carga punto de venta y número a mano.</summary>
    public static async Task<bool> NumeracionManualAsync(IReferenciasRepository referencias, CancellationToken ct) =>
        (await referencias.GetParametroAsync("NUMERACION", "RV", ct))?.Trim() == "1";
}
