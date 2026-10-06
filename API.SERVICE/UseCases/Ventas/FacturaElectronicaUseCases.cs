using API.SERVICE.Domain.Afip;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Afip;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;
using API.SERVICE.Mappings.Ventas;
using API.SERVICE.Models.Ventas;

namespace API.SERVICE.UseCases.Ventas;

/// <summary>Quién emite la factura: datos AFIP de la sucursal y tipo de comprobante según la letra.</summary>
public sealed record EmisorFactura(AfipEmisor Afip, int CbteTipo, bool ResponsableInscripto, string PuntoVenta);

internal static class FacturaElectronicaContexto
{
    public static async Task<EmisorFactura> ResolverAsync(
        IVentaRepository ventas, IReferenciasRepository referencias, int idSucursal, string letra, CancellationToken ct)
    {
        if (!AfipRules.ParametroPorLetra.TryGetValue(letra, out var parametroTipo))
            throw new BusinessException($"No es posible determinar el tipo de comprobante para la letra {letra}.");

        var sucursal = await ventas.GetSucursalAsync(idSucursal, ct)
            ?? throw new NotFoundException($"Sucursal {idSucursal} no existe.");
        if (!long.TryParse(sucursal.Cuit?.Replace("-", string.Empty), out var cuit))
            throw new BusinessException($"La sucursal {idSucursal} no tiene un CUIT válido para facturar.");
        if (!int.TryParse(sucursal.PuntoVentaAfip, out var puntoVenta))
            throw new BusinessException($"La sucursal {idSucursal} no tiene punto de venta AFIP.");

        var cbteTipo = await referencias.GetParametroEnteroAsync("AFIP", parametroTipo, ct);
        var ri = sucursal.IdCategoriaIva == await referencias.GetIdCategoriaAsync("CATIVA", "RESP. INSCRIPTO", ct);

        // Certificado por sucursal (el ERP usa SingletonParametro con el ID de sucursal como ID_Empresa).
        var carpeta = await referencias.GetParametroEmpresaAsync("API", "CARPETA", idSucursal, ct);
        var certificado = await referencias.GetParametroEmpresaAsync("API", "CERTFICADO", idSucursal, ct);

        return new EmisorFactura(new AfipEmisor(cuit, puntoVenta, carpeta, certificado), cbteTipo, ri, puntoVenta.ToString().PadLeft(4, '0'));
    }

    /// <summary>Último autorizado + 1. Si AFIP no responde no se puede facturar (el ERP tampoco dejaba).</summary>
    public static async Task<long> ProximoNumeroAsync(IAfipGateway afip, EmisorFactura emisor, CancellationToken ct)
    {
        try
        {
            return await afip.GetUltimoAutorizadoAsync(emisor.Afip, emisor.CbteTipo, ct) + 1;
        }
        catch (AfipNoDisponibleException)
        {
            throw new ServiceUnavailableException("No se pueden generar facturas: AFIP no responde.");
        }
    }
}

public interface IIniciarFacturaElectronicaUseCase
{
    Task<NuevaFacturaElectronicaDisplay> ExecuteAsync(string letra, int idSucursal, CancellationToken cancellationToken = default);
}

/// <summary>Datos para empezar una factura electrónica (IniciarPuntoVenta_WS de FrmFacturasAFIP).</summary>
public sealed class IniciarFacturaElectronicaUseCase : IIniciarFacturaElectronicaUseCase
{
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IAfipGateway _afip;
    private readonly ICurrentUser _currentUser;

    public IniciarFacturaElectronicaUseCase(IVentaRepository ventas, IReciboCobroRepository comprobantes, IReferenciasRepository referencias, IAfipGateway afip, ICurrentUser currentUser)
    {
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _afip = afip;
        _currentUser = currentUser;
    }

    public async Task<NuevaFacturaElectronicaDisplay> ExecuteAsync(string letra, int idSucursal, CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);
        var fv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "FV", cancellationToken);
        var emisor = await FacturaElectronicaContexto.ResolverAsync(_ventas, _referencias, idSucursal, letra, cancellationToken);
        var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, fv, letra, cancellationToken);
        var proximo = await FacturaElectronicaContexto.ProximoNumeroAsync(_afip, emisor, cancellationToken);

        return new NuevaFacturaElectronicaDisplay(planilla.IdPlanillaCaja, emisor.PuntoVenta, letra, emisor.CbteTipo, proximo.ToString().PadLeft(8, '0'));
    }
}

public interface ICreateFacturaElectronicaUseCase
{
    Task<FacturaElectronicaResultado> ExecuteAsync(CreateFacturaElectronicaDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alta de factura electrónica (Agregar_Ws de FrmFacturasAFIP) en dos fases:
/// 1) se graba la factura completa con CAE = "0" en una transacción (igual que el comprobante interno);
/// 2) se pide el CAE con <see cref="IAutorizarFacturaElectronicaUseCase"/>.
/// Así nunca queda un CAE emitido sin su factura (el ERP llamaba a AFIP con la transacción abierta).
/// </summary>
public sealed class CreateFacturaElectronicaUseCase : ICreateFacturaElectronicaUseCase
{
    private readonly IVentaWriter _writer;
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IAfipGateway _afip;
    private readonly IAutorizarFacturaElectronicaUseCase _autorizar;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public CreateFacturaElectronicaUseCase(
        IVentaWriter writer,
        IVentaRepository ventas,
        IReciboCobroRepository comprobantes,
        IReferenciasRepository referencias,
        IAfipGateway afip,
        IAutorizarFacturaElectronicaUseCase autorizar,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser)
    {
        _writer = writer;
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _afip = afip;
        _autorizar = autorizar;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<FacturaElectronicaResultado> ExecuteAsync(CreateFacturaElectronicaDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);
        if (dto.Items.Count == 0)
            throw new BusinessException("Agregue al menos un ítem a la factura.");

        var idEntidad = dto.IdEntidad!.Value;
        var fv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "FV", cancellationToken);
        var emisor = await FacturaElectronicaContexto.ResolverAsync(_ventas, _referencias, dto.IdSucursal!.Value, dto.Letra, cancellationToken);

        // Número esperado (lo confirma AFIP al autorizar). Si AFIP no responde no se graba nada.
        var esperado = await FacturaElectronicaContexto.ProximoNumeroAsync(_afip, emisor, cancellationToken);

        // Fase 1: la factura completa, sin CAE.
        var (documento, idRecibo) = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _comprobantes.BloquearEntidadAsync(idEntidad, ct);

            var entidad = await _comprobantes.GetEntidadAsync(idEntidad, ct)
                ?? throw new NotFoundException($"Entidad {idEntidad} no existe.");
            await _writer.ValidarCuentaCorrienteAsync(dto, entidad, ct);

            var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, fv, dto.Letra, ct);
            var ahora = await _clock.GetNowAsync(ct);
            var fechaAlta = dto.FechaEmision!.Value; // el ERP usa la fecha tal como llega (sin la hora del servidor)

            var encabezado = new VentaEncabezado(
                fv, "FV", dto.Letra, emisor.PuntoVenta, esperado.ToString().PadLeft(8, '0'), planilla.IdPlanillaCaja, fechaAlta, ahora,
                VtoCae: fechaAlta.AddDays(30).ToString("dd/MM/yyyy"));

            return await _writer.GrabarAsync(dto, encabezado, entidad, idUsuario, ct);
        }, cancellationToken);

        // Fase 2: CAE. Si AFIP rechaza, el caso de uso revierte la factura y lanza 422.
        var autorizacion = await _autorizar.ExecuteAsync(documento.IdDocumentoCliente, reintento: false, cancellationToken);

        var actualizado = await _ventas.GetDocumentoAsync(documento.IdDocumentoCliente, cancellationToken) ?? documento;
        return new FacturaElectronicaResultado(actualizado.ToDisplay(), idRecibo, autorizacion.Estado, autorizacion.Mensaje);
    }
}

public interface IGetFacturasPendientesAfipUseCase
{
    Task<IReadOnlyList<DocumentoClienteDisplay>> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>Facturas electrónicas grabadas que todavía no tienen CAE (AFIP no respondió al emitirlas).</summary>
public sealed class GetFacturasPendientesAfipUseCase : IGetFacturasPendientesAfipUseCase
{
    private readonly IVentaRepository _ventas;
    private readonly IReferenciasRepository _referencias;

    public GetFacturasPendientesAfipUseCase(IVentaRepository ventas, IReferenciasRepository referencias)
    {
        _ventas = ventas;
        _referencias = referencias;
    }

    public async Task<IReadOnlyList<DocumentoClienteDisplay>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var fv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "FV", cancellationToken);
        var anulado = await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "ANULADO", cancellationToken);
        return (await _ventas.GetPendientesAfipAsync(fv, anulado, cancellationToken)).Select(d => d.ToDisplay()).ToList();
    }
}
