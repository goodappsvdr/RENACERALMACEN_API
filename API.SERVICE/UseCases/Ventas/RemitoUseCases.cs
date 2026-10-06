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

internal static class RemitoContexto
{
    /// <summary>Letra del remito según la categoría de IVA del cliente (en la base es siempre "R").</summary>
    public static async Task<string> ResolverLetraAsync(IVentaRepository ventas, int rv, int? idCategoriaIva, CancellationToken ct) =>
        await ventas.GetLetraAsync(rv, idCategoriaIva ?? 0, ct)
        ?? throw new BusinessException($"No hay letra de remito configurada para la categoría de IVA {idCategoriaIva} (ComprobantesLetras).");

    /// <summary>Tipos que se pueden remitir (el SP los tiene hardcodeados: 3, 11, 1).</summary>
    public static async Task<int[]> TiposRemitiblesAsync(IReferenciasRepository referencias, CancellationToken ct) =>
    [
        await referencias.GetParametroEnteroAsync("COMPROBANTE", "FV", ct),
        await referencias.GetParametroEnteroAsync("COMPROBANTE", "VEN", ct),
        await referencias.GetParametroEnteroAsync("COMPROBANTE", "PV", ct),
    ];

    public static async Task<int[]> EstadosNoRemitiblesAsync(IReferenciasRepository referencias, CancellationToken ct) =>
    [
        await referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "ANULADO", ct),
        await referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "CANCELADO", ct),
    ];
}

public interface IIniciarRemitoUseCase
{
    Task<NuevaVentaInternaDisplay> ExecuteAsync(int idEntidad, CancellationToken cancellationToken = default);
}

/// <summary>Datos para empezar un remito: letra, planilla de caja abierta, punto de venta y número (IniciarPuntoVenta_WS de FrmRemitos).</summary>
public sealed class IniciarRemitoUseCase : IIniciarRemitoUseCase
{
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public IniciarRemitoUseCase(IVentaRepository ventas, IReciboCobroRepository comprobantes, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<NuevaVentaInternaDisplay> ExecuteAsync(int idEntidad, CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);
        var rv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "RV", cancellationToken);
        var entidad = await _comprobantes.GetEntidadAsync(idEntidad, cancellationToken)
            ?? throw new NotFoundException($"Entidad {idEntidad} no existe.");
        var letra = await RemitoContexto.ResolverLetraAsync(_ventas, rv, entidad.IdCategoriaIva, cancellationToken);
        var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, rv, letra, cancellationToken);
        var manual = await VentaContexto.NumeracionManualAsync(_referencias, cancellationToken);

        string? sugerido = null;
        if (!manual)
            sugerido = (await _comprobantes.GetProximoNumeroAsync(planilla.PuntoVenta!, letra, rv, cancellationToken))?.ToString().PadLeft(8, '0');

        return new NuevaVentaInternaDisplay(planilla.IdPlanillaCaja, planilla.PuntoVenta!, letra, manual, sugerido);
    }
}

public interface ICreateRemitoUseCase
{
    Task<DocumentoClienteDisplay> ExecuteAsync(CreateRemitoDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alta de remito de venta en una transacción (Agregar_Ws de FrmRemitos). Por línea, como el ERP:
/// directa → descuenta stock y queda con saldo para facturar; de un presupuesto → consume su saldo, descuenta stock y queda
/// con saldo para facturar; de un comprobante de venta → solo consume su saldo (el stock lo movió la venta).
/// </summary>
public sealed class CreateRemitoUseCase : ICreateRemitoUseCase
{
    private const int IdEmpresa = 1;

    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public CreateRemitoUseCase(
        IVentaRepository ventas,
        IReciboCobroRepository comprobantes,
        IReferenciasRepository referencias,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser)
    {
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<DocumentoClienteDisplay> ExecuteAsync(CreateRemitoDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);
        if (dto.Items.Count == 0)
            throw new BusinessException("Agregue al menos un ítem al remito.");

        var idEntidad = dto.IdEntidad!.Value;
        var idSucursal = dto.IdSucursal!.Value;

        var remito = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Lock por cliente: dos remitos simultáneos no pueden entregar dos veces el mismo saldo pendiente.
            await _comprobantes.BloquearEntidadAsync(idEntidad, ct);

            var rv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "RV", ct);
            var pv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "PV", ct);
            var entidad = await _comprobantes.GetEntidadAsync(idEntidad, ct)
                ?? throw new NotFoundException($"Entidad {idEntidad} no existe.");
            var idCategoriaIva = dto.IdCategoriaIva ?? entidad.IdCategoriaIva;
            var letra = await RemitoContexto.ResolverLetraAsync(_ventas, rv, idCategoriaIva, ct);

            var origenes = await ValidarOrigenesAsync(dto, idEntidad, ct);

            var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, rv, letra, ct);
            var (puntoVenta, numero) = await NumerarAsync(dto, planilla.PuntoVenta!, letra, rv, ct);
            var ahora = await _clock.GetNowAsync(ct);
            var fechaAlta = dto.FechaEmision!.Value;
            var concepto = VentaRules.Concepto("RV", letra, puntoVenta, numero);

            var doc = new Db.DocumentosCliente
            {
                IdComprobanteTipo = rv,
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
                FechaEmision = fechaAlta,
                IdUsuario = idUsuario,
                IdEmpresa = IdEmpresa,
                IdPlanillaCaja = planilla.IdPlanillaCaja,
                Estado = VentaRules.EstadoVentaNueva,
                Cae = "0",
                VtoCae = fechaAlta.ToString("dd/MM/yyyy"),
                IdTransporte = 0,
                Transporte = string.Empty,
                IdUnidad = 0,
                Unidad = string.Empty,
                IdChofer = 0,
                Chofer = string.Empty,
                IdRemito = 0,
                BarCode = string.Empty,
                Observaciones = dto.Observaciones ?? string.Empty,
                Porcentaje = 0,
                TotalRecargo = 0,
                TotalDescuento = 0,
                IdSucursal = idSucursal,
                Remitar = false,
                Facturar = false,
                Pendiente = false,
            };
            _ventas.Add(doc);
            await _ventas.SaveChangesAsync(ct);

            _ventas.Add(new Db.ComprobantesCarga { IdComprobante = doc.IdDocumentoCliente, IdComprobanteTipo = rv, FechaCarga = ahora, IdUsuario = idUsuario });

            var estadoLibroActivo = await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "ACTIVO", ct);
            foreach (var item in dto.Items)
                await GrabarItemAsync(item, doc, origenes, rv, pv, idUsuario, concepto, ahora, estadoLibroActivo, ct);

            await ActualizarOrigenesAsync(doc.IdDocumentoCliente, origenes.Values, pv, ct);

            await _ventas.SaveChangesAsync(ct);
            return doc;
        }, cancellationToken);

        return remito.ToDisplay();
    }

    /// <summary>
    /// Los comprobantes que se entregan tienen que ser del cliente y estar pendientes de remitir; cada línea relacionada,
    /// de uno de ellos y sin superar su saldo. El ERP no validaba nada de esto en el servidor.
    /// </summary>
    private async Task<Dictionary<int, Db.DocumentosCliente>> ValidarOrigenesAsync(CreateRemitoDto dto, int idEntidad, CancellationToken ct)
    {
        var tipos = await RemitoContexto.TiposRemitiblesAsync(_referencias, ct);
        var estadosExcluidos = await RemitoContexto.EstadosNoRemitiblesAsync(_referencias, ct);
        var origenes = new Dictionary<int, Db.DocumentosCliente>();

        foreach (var idOrigen in dto.Comprobantes.Select(c => c.IdDocumentoCliente!.Value).Distinct())
        {
            var origen = await _ventas.GetDocumentoAsync(idOrigen, ct)
                ?? throw new NotFoundException($"Comprobante {idOrigen} no existe.");
            if (origen.IdCliente != idEntidad)
                throw new BusinessException($"El comprobante {idOrigen} no es del cliente {idEntidad}.");
            if (!tipos.Contains(origen.IdComprobanteTipo ?? 0) || origen.Remitar != true || origen.Pendiente != true
                || estadosExcluidos.Contains(origen.Estado ?? 0))
                throw new ConflictException($"El comprobante {idOrigen} no tiene mercadería pendiente de remitir.");
            origenes[idOrigen] = origen;
        }

        var lineasPorOrigen = new Dictionary<int, List<LineaPendienteRow>>();
        foreach (var grupo in dto.Items.Where(i => i.Relacion is { IdDocumentoCliente: > 0 })
                     .GroupBy(i => (Doc: i.Relacion!.IdDocumentoCliente!.Value, Det: i.Relacion.IdDocumentoClienteDetalle!.Value)))
        {
            if (!origenes.ContainsKey(grupo.Key.Doc))
                throw new BusinessException($"La línea relacionada al comprobante {grupo.Key.Doc} requiere incluirlo en Comprobantes.");

            if (!lineasPorOrigen.TryGetValue(grupo.Key.Doc, out var lineas))
                lineasPorOrigen[grupo.Key.Doc] = lineas = await _ventas.GetLineasPendientesAsync(grupo.Key.Doc, ct);

            var linea = lineas.FirstOrDefault(l => l.Detalle.IdDocumentoClienteDetalle == grupo.Key.Det)
                ?? throw new BusinessException($"La línea {grupo.Key.Det} del comprobante {grupo.Key.Doc} no tiene saldo pendiente.");
            if (grupo.Any(i => i.IdItem != linea.Detalle.IdItem))
                throw new BusinessException($"La línea {grupo.Key.Det} del comprobante {grupo.Key.Doc} es de otro ítem.");
            var cantidad = grupo.Sum(i => i.Cantidad);
            if (cantidad > linea.Saldo)
                throw new BusinessException($"Se remiten {cantidad} de {linea.Detalle.Descripcion} y el pendiente es {linea.Saldo}.");
        }

        var sinLineas = origenes.Keys.Except(lineasPorOrigen.Keys).ToList();
        if (sinLineas.Count > 0)
            throw new BusinessException($"El remito no entrega ninguna línea de los comprobantes {string.Join(", ", sinLineas)}.");

        return origenes;
    }

    private async Task<(string PuntoVenta, string Numero)> NumerarAsync(CreateRemitoDto dto, string puntoVentaPlanilla, string letra, int rv, CancellationToken ct)
    {
        if (await VentaContexto.NumeracionManualAsync(_referencias, ct))
        {
            if (string.IsNullOrWhiteSpace(dto.PuntoVenta) || string.IsNullOrWhiteSpace(dto.Numero))
                throw new BusinessException("La numeración de remitos es manual (NUMERACION/RV = 1): informar punto de venta y número.");

            await _comprobantes.ReservarNumeroAsync(dto.PuntoVenta, letra, rv, ct);
            return (dto.PuntoVenta, dto.Numero);
        }

        var numero = await _comprobantes.ReservarNumeroAsync(puntoVentaPlanilla, letra, rv, ct)
            ?? throw new BusinessException($"No existe el punto de venta {puntoVentaPlanilla} para remitos (letra {letra}).");
        return (puntoVentaPlanilla, numero.ToString().PadLeft(8, '0'));
    }

    private async Task GrabarItemAsync(
        VentaItemDto item, Db.DocumentosCliente doc, IReadOnlyDictionary<int, Db.DocumentosCliente> origenes, int rv, int pv,
        int idUsuario, string concepto, DateTime ahora, int estadoLibroActivo, CancellationToken ct)
    {
        var idItem = item.IdItem!.Value;
        var descripcion = item.Descripcion.ToUpperInvariant();
        var cantidad = item.Cantidad;
        var idSucursal = doc.IdSucursal ?? 0;
        var fecha = doc.FechaEmision!.Value;

        var detalle = new Db.DocumentosClienteDetalle
        {
            IdDocumentoCliente = doc.IdDocumentoCliente,
            IdItem = idItem,
            Descripcion = descripcion,
            Cantidad = cantidad,
            ListaPrecio = item.ListaPrecio ?? string.Empty,
            PrecioUnitario = item.PrecioUnitario,
            Neto = item.PrecioNeto,
            Ivaalic = item.IvaAlicuota,
            Iva = item.Iva,
            Otros = item.Otros, // el ERP grababa el "Otros" de la cabecera en cada línea
            Total = item.Total,
            IdImpuestoIva = item.IdImpuestoIva,
            IdListaPrecio = item.IdListaPrecio,
            Metros = 0,
            EstadoLibroIva = estadoLibroActivo,
            Observaciones = string.Empty,
            Porcentaje = item.Porcentaje,
            Descuento = item.MontoDescuento,
        };
        _ventas.Add(detalle);
        await _ventas.SaveChangesAsync(ct);

        if (item.IdNroSerie is > 0)
        {
            var noDisponible = await _referencias.GetIdEstadoAsync("ITEMSNROSERIE", "NO DISPONIBLE", ct);
            await _ventas.AsignarNroSerieAsync(item.IdNroSerie.Value, rv, doc.IdDocumentoCliente, detalle.IdDocumentoClienteDetalle, noDisponible, ct);
        }

        var relacion = item.Relacion is { IdDocumentoCliente: > 0 } r ? r : null;
        if (relacion is null)
        {
            // Directo: descuenta stock y queda pendiente de facturar.
            AgregarStockDetalle(doc, rv, concepto, idItem, cantidad, cantidad, cantidad, fecha, detalle.IdDocumentoClienteDetalle, 0, 0, 0);
            await DescontarStockAsync(idItem, idSucursal, cantidad, doc.IdDocumentoCliente, rv, detalle.IdDocumentoClienteDetalle, idUsuario, concepto, descripcion, ahora, ct);
            return;
        }

        var idRel = relacion.IdDocumentoCliente!.Value;
        var tipoRel = origenes[idRel].IdComprobanteTipo ?? 0; // el del comprobante, no el que manda el cliente
        var detRel = relacion.IdDocumentoClienteDetalle!.Value;
        await _ventas.AjustarSaldoStockAsync(idRel, detRel, tipoRel, idItem, -cantidad, ct);

        if (tipoRel != pv)
        {
            // Entrega de una venta: el stock ya lo descontó la venta y no queda nada por facturar.
            AgregarStockDetalle(doc, rv, concepto, idItem, cantidad, 0, -cantidad, fecha, detalle.IdDocumentoClienteDetalle, idRel, tipoRel, detRel);
            return;
        }

        // Entrega de un presupuesto: descuenta stock y queda pendiente de facturar.
        AgregarStockDetalle(doc, rv, concepto, idItem, cantidad, cantidad, cantidad, fecha, detalle.IdDocumentoClienteDetalle, idRel, tipoRel, detRel);
        await DescontarStockAsync(idItem, idSucursal, cantidad, doc.IdDocumentoCliente, rv, detalle.IdDocumentoClienteDetalle, idUsuario, concepto, descripcion, ahora, ct);
    }

    /// <summary>Estado de cada comprobante entregado y relación con el remito; el remito queda habilitado para facturar según el origen.</summary>
    private async Task ActualizarOrigenesAsync(int idRemito, IEnumerable<Db.DocumentosCliente> origenes, int pv, CancellationToken ct)
    {
        var lista = origenes.ToList();
        if (lista.Count == 0)
        {
            await _ventas.DeterminarRemitarFacturarAsync(idRemito, remitar: false, facturar: true, pendiente: true, ct);
            return;
        }

        await _ventas.SaveChangesAsync(ct); // los saldos ya se ajustaron con ExecuteUpdate; las altas de stock quedan visibles
        foreach (var origen in lista)
        {
            var pendiente = await _ventas.TienePendienteRemitarAsync(origen.IdDocumentoCliente, ct);
            _ventas.Add(new Db.DocumentosClienteRemitos { IdDocumentoCliente = origen.IdDocumentoCliente, IdRemito = idRemito, IdComprobanteTipo = origen.IdComprobanteTipo });

            var estado = await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", pendiente ? "ENTREGADO PARCIAL" : "ENTREGADO", ct);
            await _ventas.SetEstadoPendienteAsync(origen.IdDocumentoCliente, estado, pendiente, ct);

            // Lo que viene de un presupuesto todavía no se facturó: el remito queda para facturar.
            if (origen.IdComprobanteTipo == pv)
                await _ventas.DeterminarRemitarFacturarAsync(idRemito, remitar: false, facturar: true, pendiente: true, ct);
        }
    }

    private void AgregarStockDetalle(
        Db.DocumentosCliente doc, int rv, string concepto, int idItem, decimal total, decimal saldo, decimal saldo2,
        DateTime fecha, long idDetalle, int idRelacion, int idRelacionTipo, int idRelacionDetalle) =>
        _ventas.Add(new Db.EntidadesCtaCteStockMovimientosDetalle
        {
            IdEntidad = doc.IdCliente,
            IdComprobante = doc.IdDocumentoCliente,
            IdComprobanteTipo = rv,
            Concepto = VentaRules.Truncar(concepto, 50),
            IdItem = idItem,
            Total = total,
            Saldo = saldo,
            Saldo2 = saldo2,
            Fecha = fecha,
            IdSucursal = doc.IdSucursal,
            IdComprobanteDetalle = (int)idDetalle,
            IdComprobanteRelacion = idRelacion,
            IdComprobanteRelacionTipo = idRelacionTipo,
            IdComprobanteRelacionDetalle = idRelacionDetalle,
        });

    private async Task DescontarStockAsync(
        int idItem, int idSucursal, decimal cantidad, int idComprobante, int idTipo, long idDetalle, int idUsuario,
        string concepto, string descripcion, DateTime ahora, CancellationToken ct)
    {
        await _ventas.RestarStockAsync(idItem, idSucursal, cantidad, ahora, ct);
        _ventas.Add(new Db.ItemsMovimientosDetalles
        {
            IdItem = idItem,
            IdComprobante = idComprobante,
            IdComprobanteTipo = idTipo,
            IdComprobanteDetalle = (int)idDetalle,
            FechaAlta = ahora,
            IdUsuario = idUsuario,
            IdSucursal = idSucursal,
            Concepto = VentaRules.Truncar(concepto, 100),
            Item = VentaRules.Truncar(descripcion, 500),
            Total = cantidad,
            Debe = 0,
            Haber = cantidad,
            Total2 = -cantidad,
            Automatico = true,
        });
    }
}

public interface IAnularRemitoUseCase
{
    Task ExecuteAsync(int idRemito, CancellationToken cancellationToken = default);
}

/// <summary>
/// Anulación de remito (Editar_Ws de FrmRemitos): devuelve el saldo a los comprobantes entregados y el stock que había
/// descontado (líneas directas y de presupuesto), libera números de serie y anula el remito. Un remito ya facturado no se anula.
/// </summary>
public sealed class AnularRemitoUseCase : IAnularRemitoUseCase
{
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public AnularRemitoUseCase(
        IVentaRepository ventas,
        IReciboCobroRepository comprobantes,
        IReferenciasRepository referencias,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser)
    {
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task ExecuteAsync(int idRemito, CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var rv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "RV", ct);
            var pv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "PV", ct);
            var doc = await _ventas.GetDocumentoAsync(idRemito, ct)
                ?? throw new NotFoundException($"Comprobante {idRemito} no existe.");
            if (doc.IdComprobanteTipo != rv)
                throw new BusinessException($"El comprobante {idRemito} no es un remito.");

            await _comprobantes.BloquearEntidadAsync(doc.IdCliente ?? 0, ct);

            // El ERP permite anular en GENERADO y FACTURADO; facturado lo rechazamos abajo porque dejaría la factura colgada.
            var estadosAnulables = new[]
            {
                await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "GENERADO", ct),
                await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "FACTURADO", ct),
            };
            if (!estadosAnulables.Contains(doc.Estado ?? 0))
                throw new ConflictException($"El remito {idRemito} no se puede anular en su estado actual.");

            // DocumentosClienteRemitos guarda con ID_Remito = este remito tanto lo que entregó (tipo del comprobante entregado)
            // como las facturas que lo facturaron (tipo RV). El ERP las trataba igual; acá una factura viva bloquea la anulación.
            var relaciones = await _ventas.GetRelacionesComoRemitoAsync(idRemito, ct);
            var facturas = relaciones.Where(r => r.IdComprobanteTipo == rv).Select(r => r.IdDocumentoCliente ?? 0).ToList();
            if (facturas.Count > 0)
                throw new ConflictException($"El remito {idRemito} está facturado (comprobante {string.Join(", ", facturas)}): anule primero la factura.");

            var ahora = await _clock.GetNowAsync(ct);
            var detalles = (await _ventas.GetDetallesAsync(idRemito, ct)).ToDictionary(d => d.IdDocumentoClienteDetalle);

            foreach (var mov in await _ventas.GetMovimientosStockAsync(idRemito, rv, ct))
            {
                var cantidad = mov.Total ?? 0m;
                var idItem = mov.IdItem ?? 0;
                var tipoRel = mov.IdComprobanteRelacionTipo ?? 0;
                var directa = (mov.IdComprobanteRelacion ?? 0) == 0;

                if (!directa)
                    await _ventas.AjustarSaldoStockAsync(mov.IdComprobanteRelacion ?? 0, mov.IdComprobanteRelacionDetalle ?? 0, tipoRel, idItem, cantidad, ct);

                // Devuelve el stock que el remito había descontado (líneas directas y de presupuesto).
                if (directa || tipoRel == pv)
                {
                    await _ventas.SumarStockAsync(idItem, doc.IdSucursal ?? 0, cantidad, ahora, ct);
                    var item = directa
                        ? (detalles.GetValueOrDefault(mov.IdComprobanteDetalle ?? 0)?.Descripcion ?? string.Empty).ToUpperInvariant()
                        : "DEVOLUCION POR ANULACION";
                    _ventas.Add(new Db.ItemsMovimientosDetalles
                    {
                        IdItem = idItem,
                        IdComprobante = idRemito,
                        IdComprobanteTipo = rv,
                        IdComprobanteDetalle = mov.IdComprobanteDetalle ?? 0,
                        FechaAlta = ahora,
                        IdUsuario = idUsuario,
                        IdSucursal = doc.IdSucursal ?? 0,
                        Concepto = VentaRules.Truncar(mov.Concepto, 100),
                        Item = VentaRules.Truncar(item, 500),
                        Total = cantidad,
                        Debe = cantidad,
                        Haber = 0,
                        Total2 = cantidad,
                        Automatico = true,
                    });
                }
            }
            await _ventas.SaveChangesAsync(ct);

            // Comprobantes entregados: vuelven a GENERADO si no les queda nada entregado, si no a ENTREGADO PARCIAL.
            foreach (var relacion in relaciones)
            {
                var idOrigen = relacion.IdDocumentoCliente ?? 0;
                var entregado = await _ventas.GetSaldoStockAsync(idOrigen, relacion.IdComprobanteTipo ?? 0, ct);
                var estado = await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", entregado == 0 ? "GENERADO" : "ENTREGADO PARCIAL", ct);
                await _ventas.SetEstadoPendienteAsync(idOrigen, estado, pendiente: true, ct);
                await _ventas.BorrarRemitoAsociadoAsync(relacion.IdDocumentoClienteRemito, ct);
            }

            await _ventas.AnularMovimientosStockAsync(idRemito, rv, ct);
            await _ventas.LiberarNrosSerieAsync(rv, idRemito, await _referencias.GetIdEstadoAsync("ITEMSNROSERIE", "DISPONIBLE", ct), ct);
            await _ventas.AnularDocumentoAsync(idRemito, await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", "ANULADO", ct), ahora, ct);
            return true;
        }, cancellationToken);
    }
}

public interface IGetComprobantesParaRemitirUseCase
{
    Task<IReadOnlyList<ComprobanteParaRemitirDisplay>> ExecuteAsync(int idEntidad, CancellationToken cancellationToken = default);
}

/// <summary>Comprobantes del cliente con mercadería pendiente de remitir (BuscarComprobantes_WS de FrmRemitos).</summary>
public sealed class GetComprobantesParaRemitirUseCase : IGetComprobantesParaRemitirUseCase
{
    private readonly IVentaRepository _ventas;
    private readonly IReferenciasRepository _referencias;

    public GetComprobantesParaRemitirUseCase(IVentaRepository ventas, IReferenciasRepository referencias)
    {
        _ventas = ventas;
        _referencias = referencias;
    }

    public async Task<IReadOnlyList<ComprobanteParaRemitirDisplay>> ExecuteAsync(int idEntidad, CancellationToken cancellationToken = default)
    {
        var tipos = await RemitoContexto.TiposRemitiblesAsync(_referencias, cancellationToken);
        var excluidos = await RemitoContexto.EstadosNoRemitiblesAsync(_referencias, cancellationToken);
        var docs = await _ventas.GetComprobantesParaRemitirAsync(idEntidad, tipos, excluidos, cancellationToken);
        return docs.Select(d => new ComprobanteParaRemitirDisplay(
            d.IdDocumentoCliente, d.IdComprobanteTipo ?? 0, $"{d.Letra}-{d.PuntoVenta}-{d.Numero}", d.RazonSocial, d.FechaEmision, d.Estado, d.TotalGeneral)).ToList();
    }
}

public interface IGetLineasPendientesUseCase
{
    Task<IReadOnlyList<LineaPendienteDisplay>> ExecuteAsync(int idDocumentoCliente, CancellationToken cancellationToken = default);
}

/// <summary>
/// Líneas de un comprobante con saldo pendiente, listas para cargar en un remito o en una factura
/// (BuscarRemito_PorID_Remito_Seleccionar_Ws de FrmRemitos / FrmFacturas).
/// </summary>
public sealed class GetLineasPendientesUseCase : IGetLineasPendientesUseCase
{
    private readonly IVentaRepository _ventas;

    public GetLineasPendientesUseCase(IVentaRepository ventas)
    {
        _ventas = ventas;
    }

    public async Task<IReadOnlyList<LineaPendienteDisplay>> ExecuteAsync(int idDocumentoCliente, CancellationToken cancellationToken = default)
    {
        _ = await _ventas.GetDocumentoAsync(idDocumentoCliente, cancellationToken)
            ?? throw new NotFoundException($"Comprobante {idDocumentoCliente} no existe.");

        return (await _ventas.GetLineasPendientesAsync(idDocumentoCliente, cancellationToken))
            .Select(l => new LineaPendienteDisplay(
                l.Detalle.IdItem, l.Detalle.Descripcion, l.Saldo, l.Detalle.ListaPrecio, l.Detalle.IdListaPrecio,
                l.Detalle.PrecioUnitario, l.Detalle.Neto, l.Detalle.Ivaalic, l.Detalle.Iva, l.Detalle.Otros, l.Detalle.Total,
                l.Detalle.IdImpuestoIva, l.Detalle.Porcentaje, l.Detalle.Descuento,
                new LineaRelacionDisplay(idDocumentoCliente, l.IdComprobanteTipo, (int)l.Detalle.IdDocumentoClienteDetalle)))
            .ToList();
    }
}
