using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Mappings.Ventas;
using API.SERVICE.Models.Ventas;

namespace API.SERVICE.UseCases.Ventas;

public interface ICreateVentaInternaUseCase
{
    Task<VentaInternaResultado> ExecuteAsync(CreateVentaInternaDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alta de comprobante interno de venta (VEN, letra X) en una transacción (Agregar_Ws de FrmFacturas).
/// Resuelve lo propio del interno (planilla, numeración local) y delega la escritura en <see cref="IVentaWriter"/>.
/// </summary>
public sealed class CreateVentaInternaUseCase : ICreateVentaInternaUseCase
{
    private readonly IVentaWriter _writer;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public CreateVentaInternaUseCase(
        IVentaWriter writer,
        IReciboCobroRepository comprobantes,
        IReferenciasRepository referencias,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser)
    {
        _writer = writer;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<VentaInternaResultado> ExecuteAsync(CreateVentaInternaDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);
        if (dto.Items.Count == 0)
            throw new BusinessException("Agregue al menos un ítem al comprobante.");

        var idEntidad = dto.IdEntidad!.Value;

        var (documento, idRecibo) = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Mismo lock por cliente que los recibos: la venta puede generar uno y mueve su cta. cte.
            await _comprobantes.BloquearEntidadAsync(idEntidad, ct);

            var ven = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "VEN", ct);
            var entidad = await _comprobantes.GetEntidadAsync(idEntidad, ct)
                ?? throw new NotFoundException($"Entidad {idEntidad} no existe.");

            await _writer.ValidarCuentaCorrienteAsync(dto, entidad, ct);

            var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, ven, ct);
            var (puntoVenta, numero) = await NumerarAsync(dto, planilla.PuntoVenta!, ven, ct);

            var ahora = await _clock.GetNowAsync(ct);
            var fechaAlta = dto.FechaEmision!.Value.Date.Add(ahora.TimeOfDay); // fecha elegida + hora del servidor

            var encabezado = new VentaEncabezado(
                ven, "VEN", VentaRules.LetraInterna, puntoVenta, numero, planilla.IdPlanillaCaja, fechaAlta, ahora,
                VtoCae: fechaAlta.ToString("dd/MM/yyyy"));

            return await _writer.GrabarAsync(dto, encabezado, entidad, idUsuario, ct);
        }, cancellationToken);

        return new VentaInternaResultado(documento.ToDisplay(), idRecibo);
    }

    private async Task<(string PuntoVenta, string Numero)> NumerarAsync(CreateVentaInternaDto dto, string puntoVentaPlanilla, int ven, CancellationToken ct)
    {
        if (await VentaContexto.NumeracionManualAsync(_referencias, ct))
        {
            if (string.IsNullOrWhiteSpace(dto.PuntoVenta) || string.IsNullOrWhiteSpace(dto.Numero))
                throw new BusinessException("La numeración de comprobantes es manual (NUMERACION/RV = 1): informar punto de venta y número.");

            // El ERP incrementa igual el contador del punto de venta.
            await _comprobantes.ReservarNumeroAsync(dto.PuntoVenta, VentaRules.LetraInterna, ven, ct);
            return (dto.PuntoVenta, dto.Numero);
        }

        var numero = await _comprobantes.ReservarNumeroAsync(puntoVentaPlanilla, VentaRules.LetraInterna, ven, ct)
            ?? throw new BusinessException($"No existe el punto de venta {puntoVentaPlanilla} para comprobantes internos (letra {VentaRules.LetraInterna}).");
        return (puntoVentaPlanilla, numero.ToString().PadLeft(8, '0'));
    }
}
