using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;
using API.SERVICE.Mappings.Ventas;
using API.SERVICE.Models.Ventas;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Ventas;

internal static class PresupuestoContexto
{
    public static async Task<string> ResolverLetraAsync(IVentaRepository ventas, int pv, int? idCategoriaIva, CancellationToken ct) =>
        await ventas.GetLetraAsync(pv, idCategoriaIva ?? 0, ct)
        ?? throw new BusinessException($"No hay letra de presupuesto configurada para la categoría de IVA {idCategoriaIva} (ComprobantesLetras).");

    public static void ValidarItems(PresupuestoDtoBase dto)
    {
        if (dto.Items.Count == 0)
            throw new BusinessException("Agregue al menos un ítem al presupuesto.");
        if (dto.Items.Any(i => i.Relacion is { IdDocumentoCliente: > 0 } || i.IdNroSerie is > 0))
            throw new BusinessException("Las líneas de un presupuesto no llevan comprobante relacionado ni número de serie.");
    }

    /// <summary>
    /// Línea del presupuesto y su saldo pendiente: Total = Saldo = cantidad, Saldo2 = 0 (no mueve stock, como el ERP).
    /// </summary>
    public static async Task GrabarLineasAsync(
        IVentaRepository ventas, IEnumerable<VentaItemDto> items, Db.DocumentosCliente doc, int pv, int estadoLibroActivo, CancellationToken ct)
    {
        var concepto = VentaRules.Concepto("PV", doc.Letra ?? string.Empty, doc.PuntoVenta ?? string.Empty, doc.Numero ?? string.Empty);
        foreach (var item in items)
        {
            var detalle = new Db.DocumentosClienteDetalle
            {
                IdDocumentoCliente = doc.IdDocumentoCliente,
                IdItem = item.IdItem,
                Descripcion = item.Descripcion.ToUpperInvariant(),
                Cantidad = item.Cantidad,
                ListaPrecio = item.ListaPrecio ?? string.Empty,
                PrecioUnitario = item.PrecioUnitario,
                Neto = item.PrecioNeto,
                Ivaalic = item.IvaAlicuota,
                Iva = item.Iva,
                Otros = item.Otros, // en el alta el ERP grababa el "Otros" de la cabecera en cada línea
                Total = item.Total,
                IdImpuestoIva = item.IdImpuestoIva,
                IdListaPrecio = item.IdListaPrecio,
                Metros = 0,
                EstadoLibroIva = estadoLibroActivo,
                Observaciones = string.Empty,
                Porcentaje = item.Porcentaje,
                Descuento = item.MontoDescuento,
            };
            ventas.Add(detalle);
            await ventas.SaveChangesAsync(ct);

            ventas.Add(new Db.EntidadesCtaCteStockMovimientosDetalle
            {
                IdEntidad = doc.IdCliente,
                IdComprobante = doc.IdDocumentoCliente,
                IdComprobanteTipo = pv,
                Concepto = VentaRules.Truncar(concepto, 50),
                IdItem = item.IdItem,
                Total = item.Cantidad,
                Saldo = item.Cantidad,
                Saldo2 = 0,
                Fecha = doc.FechaEmision,
                IdSucursal = doc.IdSucursal,
                IdComprobanteDetalle = (int)detalle.IdDocumentoClienteDetalle,
                IdComprobanteRelacion = 0,
                IdComprobanteRelacionTipo = 0,
                IdComprobanteRelacionDetalle = 0,
            });
        }
        await ventas.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Presupuesto modificable/anulable: GENERADO y sin nada remitido ni facturado (el ERP solo miraba el estado al anular
    /// y nada al modificar).
    /// </summary>
    public static async Task<Db.DocumentosCliente> GetEditableAsync(
        IVentaRepository ventas, IReferenciasRepository referencias, int idPresupuesto, int pv, CancellationToken ct)
    {
        var doc = await ventas.GetDocumentoAsync(idPresupuesto, ct)
            ?? throw new NotFoundException($"Comprobante {idPresupuesto} no existe.");
        if (doc.IdComprobanteTipo != pv)
            throw new BusinessException($"El comprobante {idPresupuesto} no es un presupuesto.");
        if (doc.Estado != await referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "GENERADO", ct))
            throw new ConflictException($"El presupuesto {idPresupuesto} no se puede modificar ni anular en su estado actual.");
        if (await ventas.GetSaldoStockAsync(idPresupuesto, pv, ct) != 0)
            throw new ConflictException($"El presupuesto {idPresupuesto} ya tiene mercadería remitida o facturada.");
        return doc;
    }
}

public interface IIniciarPresupuestoUseCase
{
    Task<NuevaVentaInternaDisplay> ExecuteAsync(int idEntidad, CancellationToken cancellationToken = default);
}

/// <summary>Letra, planilla de caja abierta, punto de venta y número sugerido (IniciarPuntoVenta_WS de FrmPresupuestosABM).</summary>
public sealed class IniciarPresupuestoUseCase : IIniciarPresupuestoUseCase
{
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public IniciarPresupuestoUseCase(IVentaRepository ventas, IReciboCobroRepository comprobantes, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<NuevaVentaInternaDisplay> ExecuteAsync(int idEntidad, CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);
        var pv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "PV", cancellationToken);
        var entidad = await _comprobantes.GetEntidadAsync(idEntidad, cancellationToken)
            ?? throw new NotFoundException($"Entidad {idEntidad} no existe.");
        var letra = await PresupuestoContexto.ResolverLetraAsync(_ventas, pv, entidad.IdCategoriaIva, cancellationToken);
        var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, pv, letra, cancellationToken);
        var manual = await VentaContexto.NumeracionManualAsync(_referencias, cancellationToken, "PV");

        string? sugerido = null;
        if (!manual)
            sugerido = (await _comprobantes.GetProximoNumeroAsync(planilla.PuntoVenta!, letra, pv, cancellationToken))?.ToString().PadLeft(8, '0');

        return new NuevaVentaInternaDisplay(planilla.IdPlanillaCaja, planilla.PuntoVenta!, letra, manual, sugerido);
    }
}

public interface ICreatePresupuestoUseCase
{
    Task<DocumentoClienteDisplay> ExecuteAsync(CreatePresupuestoDto dto, CancellationToken cancellationToken = default);
}

/// <summary>Alta de presupuesto en una transacción (Agregar_Ws de FrmPresupuestosABM).</summary>
public sealed class CreatePresupuestoUseCase : ICreatePresupuestoUseCase
{
    private const int IdEmpresa = 1;

    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public CreatePresupuestoUseCase(
        IVentaRepository ventas, IReciboCobroRepository comprobantes, IReferenciasRepository referencias, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<DocumentoClienteDisplay> ExecuteAsync(CreatePresupuestoDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);
        PresupuestoContexto.ValidarItems(dto);
        var idEntidad = dto.IdEntidad!.Value;

        var presupuesto = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var pv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "PV", ct);
            var entidad = await _comprobantes.GetEntidadAsync(idEntidad, ct)
                ?? throw new NotFoundException($"Entidad {idEntidad} no existe.");
            var idCategoriaIva = dto.IdCategoriaIva ?? entidad.IdCategoriaIva;
            var letra = await PresupuestoContexto.ResolverLetraAsync(_ventas, pv, idCategoriaIva, ct);
            var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, pv, letra, ct);
            var (puntoVenta, numero) = await NumerarAsync(dto, planilla.PuntoVenta!, letra, pv, ct);
            var fecha = dto.FechaEmision!.Value;

            var doc = new Db.DocumentosCliente
            {
                IdComprobanteTipo = pv,
                IdCondicion = VentaRules.Condicion,
                Letra = letra,
                IdPuntoVenta = int.TryParse(puntoVenta, out var idPv) ? idPv : 0,
                PuntoVenta = puntoVenta,
                Numero = numero,
                IdCliente = idEntidad,
                RazonSocial = VentaRules.Truncar(dto.RazonSocial ?? entidad.RazonSocial, 50),
                IdCategoriaIva = idCategoriaIva,
                NroDoc = VentaRules.Truncar(dto.Cuit ?? entidad.Cuit, 50),
                IdProvincia = dto.IdProvincia ?? entidad.IdProvincia,
                IdLocalidad = dto.IdLocalidad ?? entidad.IdLocalidad,
                Calle = VentaRules.Truncar(dto.Calle ?? entidad.Direccion, 50),
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
                Cae = "0",
                VtoCae = fecha.ToString("dd/MM/yyyy"),
                IdTransporte = 0,
                Transporte = string.Empty,
                IdUnidad = 0,
                Unidad = string.Empty,
                IdChofer = 0,
                Chofer = string.Empty,
                IdRemito = 0,
                BarCode = string.Empty,
                Observaciones = VentaRules.Truncar(dto.Observaciones ?? string.Empty, 50),
                Porcentaje = dto.PorcentajeTotal,
                TotalRecargo = dto.TotalRecargo,
                TotalDescuento = dto.TotalDescuento,
                IdSucursal = dto.IdSucursal,
                Remitar = true,
                Facturar = true,
                Pendiente = true,
            };
            _ventas.Add(doc);
            await _ventas.SaveChangesAsync(ct);

            if (!string.IsNullOrEmpty(dto.Observaciones))
                _ventas.Add(new Db.DocumentosClienteObservaciones { IdDocumentoCliente = doc.IdDocumentoCliente, Observaciones = dto.Observaciones });
            _ventas.Add(new Db.DocumentosClienteVencimientos { IdDocumentoCliente = doc.IdDocumentoCliente, FechaVencimiento = fecha.AddDays(30) });

            var estadoLibroActivo = await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "ACTIVO", ct);
            await PresupuestoContexto.GrabarLineasAsync(_ventas, dto.Items, doc, pv, estadoLibroActivo, ct);
            return doc;
        }, cancellationToken);

        return presupuesto.ToDisplay();
    }

    private async Task<(string PuntoVenta, string Numero)> NumerarAsync(CreatePresupuestoDto dto, string puntoVentaPlanilla, string letra, int pv, CancellationToken ct)
    {
        if (await VentaContexto.NumeracionManualAsync(_referencias, ct, "PV"))
        {
            if (string.IsNullOrWhiteSpace(dto.PuntoVenta) || string.IsNullOrWhiteSpace(dto.Numero))
                throw new BusinessException("La numeración de presupuestos es manual (NUMERACION/PV = 1): informar punto de venta y número.");

            await _comprobantes.ReservarNumeroAsync(dto.PuntoVenta, letra, pv, ct);
            return (dto.PuntoVenta, dto.Numero);
        }

        var numero = await _comprobantes.ReservarNumeroAsync(puntoVentaPlanilla, letra, pv, ct)
            ?? throw new BusinessException($"No existe el punto de venta {puntoVentaPlanilla} para presupuestos (letra {letra}).");
        return (puntoVentaPlanilla, numero.ToString().PadLeft(8, '0'));
    }
}

public interface IUpdatePresupuestoUseCase
{
    Task<DocumentoClienteDisplay> ExecuteAsync(int idPresupuesto, UpdatePresupuestoDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Modificación de presupuesto (Modificar_Ws de FrmPresupuestosABM): actualiza la cabecera y reemplaza todas las líneas
/// con sus saldos. Letra, número, fecha y sucursal no cambian.
/// </summary>
public sealed class UpdatePresupuestoUseCase : IUpdatePresupuestoUseCase
{
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public UpdatePresupuestoUseCase(
        IVentaRepository ventas, IReciboCobroRepository comprobantes, IReferenciasRepository referencias, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<DocumentoClienteDisplay> ExecuteAsync(int idPresupuesto, UpdatePresupuestoDto dto, CancellationToken cancellationToken = default)
    {
        VentaContexto.RequireIdUsuario(_currentUser);
        PresupuestoContexto.ValidarItems(dto);
        var idEntidad = dto.IdEntidad!.Value;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var pv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "PV", ct);
            var actual = await _ventas.GetDocumentoAsync(idPresupuesto, ct)
                ?? throw new NotFoundException($"Comprobante {idPresupuesto} no existe.");

            // Lock del cliente actual y del nuevo (siempre en el mismo orden) contra remitos / facturas que consuman el saldo.
            foreach (var id in new[] { actual.IdCliente ?? 0, idEntidad }.Distinct().Order())
                await _comprobantes.BloquearEntidadAsync(id, ct);

            var doc = await PresupuestoContexto.GetEditableAsync(_ventas, _referencias, idPresupuesto, pv, ct);
            var entidad = await _comprobantes.GetEntidadAsync(idEntidad, ct)
                ?? throw new NotFoundException($"Entidad {idEntidad} no existe.");

            var cabecera = new PresupuestoCabeceraRow(
                idEntidad,
                VentaRules.Truncar(dto.RazonSocial ?? entidad.RazonSocial, 50),
                dto.IdCategoriaIva ?? entidad.IdCategoriaIva,
                VentaRules.Truncar(dto.Cuit ?? entidad.Cuit, 50),
                dto.IdProvincia ?? entidad.IdProvincia,
                dto.IdLocalidad ?? entidad.IdLocalidad,
                VentaRules.Truncar(dto.Calle ?? entidad.Direccion, 50),
                dto.Neto, dto.Iva, dto.Otros, dto.Total,
                VentaRules.Truncar(dto.Observaciones ?? string.Empty, 50)!);
            await _ventas.ModificarPresupuestoAsync(idPresupuesto, cabecera, ct);

            await _ventas.BorrarDetallesAsync(idPresupuesto, ct);
            await _ventas.BorrarMovimientosStockAsync(idPresupuesto, pv, ct);

            doc.IdCliente = idEntidad; // para el IdEntidad de los saldos
            var estadoLibroActivo = await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "ACTIVO", ct);
            await PresupuestoContexto.GrabarLineasAsync(_ventas, dto.Items, doc, pv, estadoLibroActivo, ct);
            return true;
        }, cancellationToken);

        var actualizado = await _ventas.GetDocumentoAsync(idPresupuesto, cancellationToken)
            ?? throw new NotFoundException($"Comprobante {idPresupuesto} no existe.");
        return actualizado.ToDisplay();
    }
}

public interface IAnularPresupuestoUseCase
{
    Task ExecuteAsync(int idPresupuesto, CancellationToken cancellationToken = default);
}

/// <summary>Anulación de presupuesto (Anular_Ws de FrmPresupuestosABM): da de baja las líneas, sus saldos y el comprobante.</summary>
public sealed class AnularPresupuestoUseCase : IAnularPresupuestoUseCase
{
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public AnularPresupuestoUseCase(
        IVentaRepository ventas, IReciboCobroRepository comprobantes, IReferenciasRepository referencias, IUnitOfWork unitOfWork,
        IServerClock clock, ICurrentUser currentUser)
    {
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task ExecuteAsync(int idPresupuesto, CancellationToken cancellationToken = default)
    {
        VentaContexto.RequireIdUsuario(_currentUser);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var pv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "PV", ct);
            var previo = await _ventas.GetDocumentoAsync(idPresupuesto, ct)
                ?? throw new NotFoundException($"Comprobante {idPresupuesto} no existe.");
            await _comprobantes.BloquearEntidadAsync(previo.IdCliente ?? 0, ct);
            await PresupuestoContexto.GetEditableAsync(_ventas, _referencias, idPresupuesto, pv, ct);

            var baja = await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "BAJA", ct);
            foreach (var detalle in await _ventas.GetDetallesAsync(idPresupuesto, ct))
                await _ventas.AnularDetalleAsync(detalle.IdDocumentoClienteDetalle, baja, ct);

            // El ERP no tocaba los saldos: quedaban pendientes aunque el presupuesto estuviera anulado.
            await _ventas.AnularMovimientosStockAsync(idPresupuesto, pv, ct);
            await _ventas.AnularDocumentoAsync(idPresupuesto, await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "ANULADO", ct), await _clock.GetNowAsync(ct), ct);
            return true;
        }, cancellationToken);
    }
}
