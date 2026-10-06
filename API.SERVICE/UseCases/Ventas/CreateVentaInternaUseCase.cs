using API.SERVICE.Domain;
using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;
using API.SERVICE.Mappings.Ventas;
using API.SERVICE.Models.Clientes;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Clientes;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Ventas;

public interface ICreateVentaInternaUseCase
{
    Task<VentaInternaResultado> ExecuteAsync(CreateVentaInternaDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alta de comprobante interno de venta (VEN, letra X) en una transacción. Port de Agregar_Ws (FrmFacturas):
/// cabecera, observación, vencimiento, detalle (con ofertas, números de serie y stock según venga de un presupuesto o
/// remito), estado de los comprobantes relacionados, cta. cte., cobro en el momento (recibo) y numeración.
/// </summary>
public sealed class CreateVentaInternaUseCase : ICreateVentaInternaUseCase
{
    private const int IdEmpresa = 1;

    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReciboCobroWriter _recibos;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public CreateVentaInternaUseCase(
        IVentaRepository ventas,
        IReciboCobroRepository comprobantes,
        IReciboCobroWriter recibos,
        IReferenciasRepository referencias,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser)
    {
        _ventas = ventas;
        _comprobantes = comprobantes;
        _recibos = recibos;
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
        var idSucursal = dto.IdSucursal!.Value;

        var (documento, idRecibo) = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Mismo lock por cliente que los recibos: la venta puede generar uno y mueve su cta. cte.
            await _comprobantes.BloquearEntidadAsync(idEntidad, ct);

            var ven = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "VEN", ct);
            var entidad = await _comprobantes.GetEntidadAsync(idEntidad, ct)
                ?? throw new NotFoundException($"Entidad {idEntidad} no existe.");

            await ValidarCuentaCorrienteAsync(dto, entidad, ct);

            var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, ven, ct);
            var (puntoVenta, numero) = await NumerarAsync(dto, planilla.PuntoVenta!, ven, ct);

            var ahora = await _clock.GetNowAsync(ct);
            var fechaAlta = dto.FechaEmision!.Value.Date.Add(ahora.TimeOfDay); // fecha elegida + hora del servidor
            var diasInteres = entidad.DiasInteres ?? 0;
            var concepto = VentaRules.Concepto("VEN", VentaRules.LetraInterna, puntoVenta, numero);

            // 1. Cabecera
            var doc = new Db.DocumentosCliente
            {
                IdComprobanteTipo = ven,
                IdCondicion = VentaRules.Condicion,
                Letra = VentaRules.LetraInterna,
                IdPuntoVenta = int.TryParse(puntoVenta, out var pv) ? pv : 0,
                PuntoVenta = puntoVenta,
                Numero = numero,
                IdCliente = idEntidad,
                RazonSocial = VentaRules.Truncar(dto.RazonSocial ?? entidad.RazonSocial, 50),
                IdCategoriaIva = dto.IdCategoriaIva ?? entidad.IdCategoriaIva,
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
                Observaciones = VentaRules.Truncar(dto.Observaciones ?? string.Empty, 50), // el SP recibe varchar(50)
                Porcentaje = dto.PorcentajeTotal,
                TotalRecargo = dto.TotalRecargo,
                TotalDescuento = dto.TotalDescuento,
                IdSucursal = idSucursal,
                Remitar = false,
                Facturar = false,
                Pendiente = false,
            };
            _ventas.Add(doc);
            await _ventas.SaveChangesAsync(ct);

            if (!string.IsNullOrEmpty(dto.Observaciones))
                _ventas.Add(new Db.DocumentosClienteObservaciones { IdDocumentoCliente = doc.IdDocumentoCliente, Observaciones = dto.Observaciones });

            _ventas.Add(new Db.DocumentosClienteVencimientos { IdDocumentoCliente = doc.IdDocumentoCliente, FechaVencimiento = fechaAlta.AddDays(diasInteres) });

            // 2. Detalle y stock
            var estadoLibroActivo = await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "ACTIVO", ct);
            foreach (var item in dto.Items)
                await GrabarItemAsync(item, doc, ven, idEntidad, idSucursal, idUsuario, concepto, fechaAlta, ahora, estadoLibroActivo, ct);

            // 3. Presupuestos / remitos facturados
            await ActualizarRelacionadosAsync(dto, doc.IdDocumentoCliente, ct);

            // 4. Cta. cte.
            var ctaCte = new Db.EntidadesCtaCte
            {
                IdEntidad = idEntidad,
                IdComprobanteTipo = ven,
                IdComprobante = doc.IdDocumentoCliente,
                Concepto = concepto,
                NroCuota = 1,
                Total = dto.Total,
                Saldo = dto.Total,
                Cancelado = false,
                Fecha = fechaAlta,
                FechaVencimiento = fechaAlta.AddDays(diasInteres),
                FechaAnulacion = fechaAlta,
                FechaPago = fechaAlta,
                InteresAplicado = 0,
                Estado = await _referencias.IdAsync(EstadosCobranza.CtaCteGenerado, ct),
                Total2 = dto.Total,
                IdEmpresa = IdEmpresa,
                IdSucursal = idSucursal,
                IdUsuario = idUsuario,
            };
            _ventas.Add(ctaCte);
            await _ventas.SaveChangesAsync(ct);

            _ventas.Add(new Db.EntidadesCtaCteMovimientos
            {
                IdEntidadCtaCte = ctaCte.IdEntidadCtaCte,
                Concepto = concepto,
                AfavorEntidad = 0,
                EnContraEntidad = dto.Total,
                Fecha = fechaAlta,
                IdElementoCobroPago = await _referencias.GetParametroEnteroAsync("ELEMENTO", "CTACTE", ct),
                IdElemento = 1,
                IdComprobanteTipo = ven,
                IdComprobante = doc.IdDocumentoCliente,
            });
            await _ventas.SaveChangesAsync(ct);

            // 5. Cobro en el momento: recibo que imputa este comprobante, en la misma transacción
            int? idRecibo = null;
            if (dto.Elementos.Count > 0)
            {
                var recibo = await _recibos.GrabarAsync(new CreateReciboDto
                {
                    IdEntidad = idEntidad,
                    FechaEmision = fechaAlta,
                    IdSucursal = idSucursal,
                    Observaciones = dto.Observaciones,
                    Imputaciones =
                    [
                        new ImputacionDto
                        {
                            IdComprobante = doc.IdDocumentoCliente,
                            IdComprobanteTipo = ven,
                            ImporteComprobante = dto.Total,
                            InteresAplicado = 0,
                        },
                    ],
                    Elementos = dto.Elementos,
                }, idUsuario, ct);
                idRecibo = recibo.IdEntidadRecibo;
            }

            return (doc, idRecibo);
        }, cancellationToken);

        return new VentaInternaResultado(documento.ToDisplay(), idRecibo);
    }

    /// <summary>
    /// Reglas que el ERP aplicaba solo en el navegador: sin cta. cte. habilitada la venta es al contado, y con cta. cte.
    /// el saldo + la venta no puede superar el límite salvo confirmación explícita.
    /// </summary>
    private async Task ValidarCuentaCorrienteAsync(CreateVentaInternaDto dto, Db.Entidades entidad, CancellationToken ct)
    {
        if (entidad.CtaCte != true)
        {
            if (dto.Elementos.Count == 0)
                throw new BusinessException("El cliente no tiene cuenta corriente habilitada: agregue una forma de pago para realizar la venta al contado.");
            return;
        }

        var saldo = await _ventas.GetSaldoCtaCteAsync(entidad.IdEntidad, ct);
        var limite = entidad.LimiteCtaCte ?? 0m;
        if (saldo + dto.Total > limite && !dto.ConfirmarExcesoLimite)
        {
            throw new ConflictException(
                $"El cliente superó el límite de cta. cte. (límite {Formato.Importe(limite)}, saldo {Formato.Importe(saldo)}, venta {Formato.Importe(dto.Total)}). " +
                "Para continuar igual, reenviar con confirmarExcesoLimite = true.");
        }
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

    private async Task GrabarItemAsync(
        VentaItemDto item, Db.DocumentosCliente doc, int ven, int idEntidad, int idSucursal, int idUsuario,
        string concepto, DateTime fechaAlta, DateTime ahora, int estadoLibroActivo, CancellationToken ct)
    {
        var idItem = item.IdItem!.Value;
        var descripcion = item.Descripcion.ToUpperInvariant();

        // Oferta por agotamiento: descuenta unidades disponibles (el ERP lo hacía fuera de la transacción).
        var oferta = await _ventas.GetOfertaActivaAsync(idSucursal, idItem, ct);
        if (oferta?.TipoOferta == VentaRules.OfertaPorAgotamiento)
            await _ventas.AjustarOfertaDisponibleAsync(oferta.IdOferta, -item.Cantidad, ct);

        var detalle = new Db.DocumentosClienteDetalle
        {
            IdDocumentoCliente = doc.IdDocumentoCliente,
            IdItem = idItem,
            Descripcion = descripcion,
            Cantidad = item.Cantidad,
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
            await _ventas.AsignarNroSerieAsync(item.IdNroSerie.Value, ven, doc.IdDocumentoCliente, detalle.IdDocumentoClienteDetalle, noDisponible, ct);
        }

        var relacion = item.Relacion is { IdDocumentoCliente: > 0 } r ? r : null;
        var cantidad = item.Cantidad;

        if (relacion is null)
        {
            // Venta directa: descuenta stock.
            AgregarStockDetalle(idEntidad, doc.IdDocumentoCliente, ven, concepto, idItem, cantidad, cantidad, cantidad, fechaAlta, idSucursal, detalle.IdDocumentoClienteDetalle, 0, 0, 0);
            await DescontarStockAsync(idItem, idSucursal, cantidad, doc.IdDocumentoCliente, ven, detalle.IdDocumentoClienteDetalle, idUsuario, concepto, descripcion, ahora, ct);
            return;
        }

        var idRel = relacion.IdDocumentoCliente!.Value;
        var tipoRel = relacion.IdComprobanteTipo!.Value;
        var detRel = relacion.IdDocumentoClienteDetalle!.Value;
        var pv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "PV", ct);

        if (tipoRel != pv)
        {
            // Viene de un remito: el stock ya se movió con el remito, solo se consume su saldo pendiente.
            AgregarStockDetalle(idEntidad, doc.IdDocumentoCliente, ven, concepto, idItem, cantidad, 0, -cantidad, fechaAlta, idSucursal, detalle.IdDocumentoClienteDetalle, idRel, tipoRel, detRel);
            await _ventas.AjustarSaldoStockAsync(idRel, detRel, tipoRel, idItem, -cantidad, ct);
            return;
        }

        // Viene de un presupuesto: consume su saldo y además descuenta stock.
        AgregarStockDetalle(idEntidad, doc.IdDocumentoCliente, ven, concepto, idItem, cantidad, cantidad, cantidad, fechaAlta, idSucursal, detalle.IdDocumentoClienteDetalle, idRel, tipoRel, detRel);
        await _ventas.AjustarSaldoStockAsync(idRel, detRel, tipoRel, idItem, -cantidad, ct);
        await DescontarStockAsync(idItem, idSucursal, cantidad, doc.IdDocumentoCliente, ven, detalle.IdDocumentoClienteDetalle, idUsuario, concepto, descripcion, ahora, ct);
    }

    private void AgregarStockDetalle(
        int idEntidad, int idComprobante, int idTipo, string concepto, int idItem, decimal total, decimal saldo, decimal saldo2,
        DateTime fecha, int idSucursal, long idDetalle, int idRelacion, int idRelacionTipo, int idRelacionDetalle) =>
        _ventas.Add(new Db.EntidadesCtaCteStockMovimientosDetalle
        {
            IdEntidad = idEntidad,
            IdComprobante = idComprobante,
            IdComprobanteTipo = idTipo,
            Concepto = VentaRules.Truncar(concepto, 50),
            IdItem = idItem,
            Total = total,
            Saldo = saldo,
            Saldo2 = saldo2,
            Fecha = fecha,
            IdSucursal = idSucursal,
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
        await _ventas.SaveChangesAsync(ct);
    }

    private async Task ActualizarRelacionadosAsync(CreateVentaInternaDto dto, int idDocumento, CancellationToken ct)
    {
        if (dto.Remitos.Count == 0)
        {
            await _ventas.DeterminarRemitarFacturarAsync(idDocumento, remitar: true, facturar: false, pendiente: true, ct);
            return;
        }

        var pv = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "PV", ct);
        foreach (var remito in dto.Remitos)
        {
            var idRel = remito.IdDocumentoCliente!.Value;
            var tipoRel = remito.IdComprobanteTipo!.Value;

            var pendiente = await _ventas.TienePendienteRemitarAsync(idRel, ct);
            _ventas.Add(new Db.DocumentosClienteRemitos { IdDocumentoCliente = idDocumento, IdRemito = idRel, IdComprobanteTipo = tipoRel });

            var estado = await _referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", pendiente ? "FACTURADO PARCIAL" : "FACTURADO", ct);
            await _ventas.SetEstadoPendienteAsync(idRel, estado, pendiente, ct);

            // Si se factura un presupuesto, el comprobante interno queda habilitado para remitarse.
            if (tipoRel == pv)
                await _ventas.DeterminarRemitarFacturarAsync(idDocumento, remitar: true, facturar: false, pendiente: true, ct);
        }
        await _ventas.SaveChangesAsync(ct);
    }
}
