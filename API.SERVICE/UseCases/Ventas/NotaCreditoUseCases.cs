using API.SERVICE.Domain;
using API.SERVICE.Domain.Afip;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Afip;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;
using API.SERVICE.Mappings.Ventas;
using API.SERVICE.Models.Ventas;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Ventas;

internal static class NotaCreditoContexto
{
    /// <summary>La factura a acreditar: FV autorizada y no anulada.</summary>
    public static async Task<Db.DocumentosCliente> GetFacturaAcreditableAsync(
        IVentaRepository ventas, IReferenciasRepository referencias, int idFactura, CancellationToken ct)
    {
        var fv = await referencias.GetParametroEnteroAsync("COMPROBANTE", "FV", ct);
        var factura = await ventas.GetDocumentoAsync(idFactura, ct)
            ?? throw new NotFoundException($"Factura {idFactura} no existe.");
        if (factura.IdComprobanteTipo != fv)
            throw new BusinessException($"El comprobante {idFactura} no es una factura electrónica.");
        if (factura.Estado == await referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "ANULADO", ct))
            throw new ConflictException($"La factura {idFactura} está anulada.");
        if (string.IsNullOrEmpty(factura.Cae) || factura.Cae == AfipRules.CaePendiente)
            throw new ConflictException($"La factura {idFactura} no está autorizada por AFIP: no se le puede hacer nota de crédito.");
        return factura;
    }
}

public interface IIniciarNotaCreditoUseCase
{
    Task<NuevaFacturaElectronicaDisplay> ExecuteAsync(int idFactura, CancellationToken cancellationToken = default);
}

/// <summary>Datos para empezar una nota de crédito sobre una factura (IniciarPuntoVenta_WS de FrmNotasCreditoAFIP).</summary>
public sealed class IniciarNotaCreditoUseCase : IIniciarNotaCreditoUseCase
{
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IAfipGateway _afip;
    private readonly ICurrentUser _currentUser;

    public IniciarNotaCreditoUseCase(IVentaRepository ventas, IReciboCobroRepository comprobantes, IReferenciasRepository referencias, IAfipGateway afip, ICurrentUser currentUser)
    {
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _afip = afip;
        _currentUser = currentUser;
    }

    public async Task<NuevaFacturaElectronicaDisplay> ExecuteAsync(int idFactura, CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);
        var factura = await NotaCreditoContexto.GetFacturaAcreditableAsync(_ventas, _referencias, idFactura, cancellationToken);
        var letra = factura.Letra ?? string.Empty;
        var nc = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "NC", cancellationToken);
        var emisor = await FacturaElectronicaContexto.ResolverAsync(_ventas, _referencias, factura.IdSucursal ?? 0, letra, cancellationToken, AfipRules.NotaCredito);
        var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, nc, letra, cancellationToken);
        var proximo = await FacturaElectronicaContexto.ProximoNumeroAsync(_afip, emisor, cancellationToken);

        return new NuevaFacturaElectronicaDisplay(planilla.IdPlanillaCaja, emisor.PuntoVenta, letra, emisor.CbteTipo, proximo.ToString().PadLeft(8, '0'));
    }
}

public interface ICreateNotaCreditoUseCase
{
    Task<NotaCreditoResultado> ExecuteAsync(CreateNotaCreditoDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alta de nota de crédito electrónica (Agregar_Ws de FrmNotasCreditoAFIP) en dos fases:
/// 1) se graba el borrador (encabezado, detalle y relación con la factura) con CAE = "0", sin efectos;
/// 2) <see cref="IAutorizarNotaCreditoUseCase"/> pide el CAE y, si AFIP aprueba, aplica los efectos.
/// </summary>
public sealed class CreateNotaCreditoUseCase : ICreateNotaCreditoUseCase
{
    private const int IdEmpresa = 1;

    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IAfipGateway _afip;
    private readonly IAutorizarNotaCreditoUseCase _autorizar;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public CreateNotaCreditoUseCase(
        IVentaRepository ventas,
        IReciboCobroRepository comprobantes,
        IReferenciasRepository referencias,
        IAfipGateway afip,
        IAutorizarNotaCreditoUseCase autorizar,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _afip = afip;
        _autorizar = autorizar;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<NotaCreditoResultado> ExecuteAsync(CreateNotaCreditoDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);
        if (dto.Items.Count == 0)
            throw new BusinessException("Agregue al menos un ítem a la nota de crédito.");

        var idFactura = dto.IdFactura!.Value;
        var factura = await NotaCreditoContexto.GetFacturaAcreditableAsync(_ventas, _referencias, idFactura, cancellationToken);
        var letra = factura.Letra ?? string.Empty;
        var idSucursal = factura.IdSucursal ?? 0;
        var ncTipo = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "NC", cancellationToken);
        var emisor = await FacturaElectronicaContexto.ResolverAsync(_ventas, _referencias, idSucursal, letra, cancellationToken, AfipRules.NotaCredito);
        var esperado = await FacturaElectronicaContexto.ProximoNumeroAsync(_afip, emisor, cancellationToken);

        var nc = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _comprobantes.BloquearEntidadAsync(factura.IdCliente ?? 0, ct);

            var lineasFactura = (await _ventas.GetDetallesAsync(idFactura, ct)).ToDictionary(l => l.IdDocumentoClienteDetalle);
            ValidarItems(dto, lineasFactura);

            // Lo ya acreditado se lee bajo el lock de la entidad: dos notas de crédito simultáneas no pueden pasarse del total.
            var anulado = await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "ANULADO", ct);
            var previas = await _ventas.GetTotalNotasCreditoAsync(idFactura, ncTipo, anulado, excluirId: 0, ct);
            var totalFactura = factura.TotalGeneral ?? 0m;
            if (previas + dto.Total > totalFactura + 0.01m)
                throw new BusinessException(
                    $"La nota de crédito ({Formato.Importe(dto.Total)}) supera el saldo acreditable de la factura ({Formato.Importe(totalFactura - previas)}).");

            var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, ncTipo, letra, ct);
            var fecha = dto.FechaEmision!.Value;

            var doc = new Db.DocumentosCliente
            {
                IdComprobanteTipo = ncTipo,
                IdCondicion = VentaRules.Condicion,
                Letra = letra,
                IdPuntoVenta = int.TryParse(emisor.PuntoVenta, out var pv) ? pv : 0,
                PuntoVenta = emisor.PuntoVenta,
                Numero = esperado.ToString().PadLeft(8, '0'),
                IdCliente = factura.IdCliente,
                RazonSocial = factura.RazonSocial,
                IdCategoriaIva = factura.IdCategoriaIva,
                NroDoc = factura.NroDoc,
                IdProvincia = factura.IdProvincia,
                IdLocalidad = factura.IdLocalidad,
                Calle = factura.Calle,
                Nro = "0",
                TotalNeto = dto.Neto,
                TotalIva = dto.Iva,
                TotalOtrosImpuestos = dto.Otros,
                TotalGeneral = dto.Total,
                FechaEmision = fecha,
                IdUsuario = idUsuario,
                IdEmpresa = IdEmpresa,
                IdPlanillaCaja = planilla.IdPlanillaCaja,
                Estado = VentaRules.EstadoVentaNueva,
                Cae = AfipRules.CaePendiente,
                VtoCae = fecha.AddDays(30).ToString("dd/MM/yyyy"),
                IdTransporte = 0,
                Transporte = string.Empty,
                IdUnidad = 0,
                Unidad = string.Empty,
                IdChofer = 0,
                Chofer = string.Empty,
                IdRemito = 0,
                BarCode = string.Empty,
                Observaciones = VentaRules.Truncar(dto.Observaciones ?? string.Empty, 50),
                Porcentaje = dto.TotalEnvases, // el ERP guarda acá el total de envases
                TotalRecargo = 0,
                TotalDescuento = 0,
                IdSucursal = idSucursal,
                Remitar = false,
                Facturar = false,
                Pendiente = false,
            };
            _ventas.Add(doc);
            await _ventas.SaveChangesAsync(ct);

            if (!string.IsNullOrEmpty(dto.Observaciones))
                _ventas.Add(new Db.DocumentosClienteObservaciones { IdDocumentoCliente = doc.IdDocumentoCliente, Observaciones = dto.Observaciones });
            _ventas.Add(new Db.DocumentosClienteVencimientos { IdDocumentoCliente = doc.IdDocumentoCliente, FechaVencimiento = fecha.AddDays(30) });

            var estadoLibroActivo = await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "ACTIVO", ct);
            foreach (var item in dto.Items)
            {
                var linea = lineasFactura[item.IdDocumentoClienteDetalle!.Value];
                _ventas.Add(new Db.DocumentosClienteDetalle
                {
                    IdDocumentoCliente = doc.IdDocumentoCliente,
                    IdItem = linea.IdItem,
                    Descripcion = linea.Descripcion,
                    Cantidad = item.Cantidad,
                    ListaPrecio = linea.ListaPrecio ?? string.Empty,
                    PrecioUnitario = item.PrecioUnitario,
                    Neto = item.PrecioNeto,
                    Ivaalic = item.IvaAlicuota,
                    Iva = item.Iva,
                    Otros = item.Otros,
                    Total = item.Total,
                    IdImpuestoIva = linea.IdImpuestoIva,
                    IdListaPrecio = linea.IdListaPrecio,
                    Metros = 0,
                    EstadoLibroIva = estadoLibroActivo,
                    Observaciones = string.Empty,
                    Porcentaje = item.Porcentaje,
                    Descuento = item.MontoDescuento,
                });
            }

            _ventas.Add(new Db.DocumentosClienteRelacion { IdDocumentoCliente1 = idFactura, IdDocumentoCliente2 = doc.IdDocumentoCliente });
            await _ventas.SaveChangesAsync(ct);
            return doc;
        }, cancellationToken);

        // Fase 2: CAE y efectos. Si AFIP rechaza, se anula el borrador y se lanza 422.
        var autorizacion = await _autorizar.ExecuteAsync(nc.IdDocumentoCliente, reintento: false, cancellationToken);

        var actualizado = await _ventas.GetDocumentoAsync(nc.IdDocumentoCliente, cancellationToken) ?? nc;
        return new NotaCreditoResultado(actualizado.ToDisplay(), autorizacion.Estado, autorizacion.Mensaje);
    }

    /// <summary>Cada línea tiene que ser de la factura y no acreditar más cantidad que la facturada.</summary>
    private static void ValidarItems(CreateNotaCreditoDto dto, IReadOnlyDictionary<long, Db.DocumentosClienteDetalle> lineasFactura)
    {
        foreach (var grupo in dto.Items.GroupBy(i => i.IdDocumentoClienteDetalle!.Value))
        {
            if (!lineasFactura.TryGetValue(grupo.Key, out var linea))
                throw new BusinessException($"La línea {grupo.Key} no pertenece a la factura {dto.IdFactura}.");
            var cantidad = grupo.Sum(i => i.Cantidad);
            if (cantidad > (linea.Cantidad ?? 0m))
                throw new BusinessException($"Se acreditan {cantidad} de {linea.Descripcion} y la factura tiene {linea.Cantidad}.");
        }
    }
}

public interface IAutorizarComprobanteElectronicoUseCase
{
    Task<AutorizacionAfipResultado> ExecuteAsync(int idDocumentoCliente, CancellationToken cancellationToken = default);
}

/// <summary>Reintento de autorización: según el tipo del comprobante delega en la factura o en la nota de crédito.</summary>
public sealed class AutorizarComprobanteElectronicoUseCase : IAutorizarComprobanteElectronicoUseCase
{
    private readonly IVentaRepository _ventas;
    private readonly IReferenciasRepository _referencias;
    private readonly IAutorizarFacturaElectronicaUseCase _factura;
    private readonly IAutorizarNotaCreditoUseCase _notaCredito;

    public AutorizarComprobanteElectronicoUseCase(
        IVentaRepository ventas, IReferenciasRepository referencias, IAutorizarFacturaElectronicaUseCase factura, IAutorizarNotaCreditoUseCase notaCredito)
    {
        _ventas = ventas;
        _referencias = referencias;
        _factura = factura;
        _notaCredito = notaCredito;
    }

    public async Task<AutorizacionAfipResultado> ExecuteAsync(int idDocumentoCliente, CancellationToken cancellationToken = default)
    {
        var doc = await _ventas.GetDocumentoAsync(idDocumentoCliente, cancellationToken)
            ?? throw new NotFoundException($"Comprobante {idDocumentoCliente} no existe.");
        var nc = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "NC", cancellationToken);
        return doc.IdComprobanteTipo == nc
            ? await _notaCredito.ExecuteAsync(idDocumentoCliente, reintento: true, cancellationToken)
            : await _factura.ExecuteAsync(idDocumentoCliente, reintento: true, cancellationToken);
    }
}
