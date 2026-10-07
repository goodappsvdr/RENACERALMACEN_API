using API.SERVICE.Domain;
using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Compras;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Mappings.Proveedores;
using API.SERVICE.Models.Compras;
using API.SERVICE.Models.Proveedores;
using API.SERVICE.UseCases.Ventas;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Compras;

internal static class OrdenPagoContexto
{
    public const string Letra = "X";

    public static Task<int> EstadoAsync(IReferenciasRepository r, string categoria, string nombre, CancellationToken ct) =>
        r.GetIdEstadoAsync(categoria, nombre, ct);
}

public interface IIniciarOrdenPagoUseCase
{
    Task<NuevaOrdenPagoDisplay> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>Planilla de caja abierta, punto de venta y número sugerido (IniciarPuntoVenta_WS de FrmOrdendePago).</summary>
public sealed class IniciarOrdenPagoUseCase : IIniciarOrdenPagoUseCase
{
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public IniciarOrdenPagoUseCase(IReciboCobroRepository comprobantes, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _comprobantes = comprobantes;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<NuevaOrdenPagoDisplay> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var idUsuario = CompraContexto.RequireIdUsuario(_currentUser);
        var op = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "OP", cancellationToken);
        var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, op, OrdenPagoContexto.Letra, cancellationToken);
        var manual = await VentaContexto.NumeracionManualAsync(_referencias, cancellationToken, "OP");
        string? sugerido = null;
        if (!manual)
            sugerido = (await _comprobantes.GetProximoNumeroAsync(planilla.PuntoVenta!, OrdenPagoContexto.Letra, op, cancellationToken))?.ToString().PadLeft(8, '0');
        return new NuevaOrdenPagoDisplay(planilla.IdPlanillaCaja, planilla.PuntoVenta!, OrdenPagoContexto.Letra, manual, sugerido);
    }
}

public interface IGetComprobantesPendientesPagoUseCase
{
    Task<IReadOnlyList<ComprobantePendientePagoDisplay>> ExecuteAsync(int idProveedor, CancellationToken cancellationToken = default);
}

/// <summary>Comprobantes pendientes de la cta. cte. del proveedor (BuscarComprobantes_WS → EntidadesCtaCte_BuscarPorID_Entidad_OrdenPago).</summary>
public sealed class GetComprobantesPendientesPagoUseCase : IGetComprobantesPendientesPagoUseCase
{
    private readonly IOrdenPagoRepository _ordenes;
    private readonly IReferenciasRepository _referencias;

    public GetComprobantesPendientesPagoUseCase(IOrdenPagoRepository ordenes, IReferenciasRepository referencias)
    {
        _ordenes = ordenes;
        _referencias = referencias;
    }

    public async Task<IReadOnlyList<ComprobantePendientePagoDisplay>> ExecuteAsync(int idProveedor, CancellationToken cancellationToken = default)
    {
        var codigos = await CodigosOrdenPago.LoadAsync(_referencias, cancellationToken);
        return (await _ordenes.GetComprobantesPendientesAsync(idProveedor, ImputacionRules.EstadoCtaCtePendiente, cancellationToken))
            .Select(c => new ComprobantePendientePagoDisplay(
                c.IdComprobante ?? 0, c.IdComprobanteTipo ?? 0, c.Concepto, c.Fecha,
                ImputacionRules.Redondear(codigos.SaldoVisible(c.IdComprobanteTipo ?? 0, c.Saldo ?? 0))))
            .ToList();
    }
}

public interface IOrdenPagoWriter
{
    /// <summary>
    /// Graba una orden de pago completa dentro de la transacción en curso (no abre una propia ni toma el lock del proveedor).
    /// Lo usan el alta de orden de pago y la compra con pago en el momento.
    /// </summary>
    Task<Db.ProveedoresRecibos> GrabarAsync(CreateOrdenPagoDto dto, int idUsuario, CancellationToken cancellationToken = default);
}

public interface ICreateOrdenPagoUseCase
{
    Task<ProveedorReciboDisplay> ExecuteAsync(CreateOrdenPagoDto dto, CancellationToken cancellationToken = default);
}

/// <summary>Alta de orden de pago en una transacción, con el lock del proveedor (Agregar_Ws de FrmOrdendePago).</summary>
public sealed class CreateOrdenPagoUseCase : ICreateOrdenPagoUseCase
{
    private readonly IOrdenPagoWriter _writer;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public CreateOrdenPagoUseCase(IOrdenPagoWriter writer, IReciboCobroRepository comprobantes, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _writer = writer;
        _comprobantes = comprobantes;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ProveedorReciboDisplay> ExecuteAsync(CreateOrdenPagoDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = CompraContexto.RequireIdUsuario(_currentUser);
        var orden = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _comprobantes.BloquearEntidadAsync(dto.IdProveedor!.Value, ct);
            return await _writer.GrabarAsync(dto, idUsuario, ct);
        }, cancellationToken);
        return orden.ToDisplay();
    }
}

/// <summary>
/// Escritura de la orden de pago (Agregar_Ws de FrmOrdendePago y generarOrdenDePago de FrmCompras): orden, imputación a comprobantes
/// (cta. cte. + estados), cta. cte. de la orden, formas de pago (caja, cheques de terceros entregados, cheques propios, bancos,
/// retenciones), detalle, movimientos y numeración. Espejo del recibo de cobro.
/// </summary>
public sealed class OrdenPagoWriter : IOrdenPagoWriter
{
    private const int IdEmpresa = 1;
    private const decimal Tolerancia = 0.01m;
    private const int MovimientoBancoDeposito = 1;
    private const int MovimientoBancoChequePropio = 2;

    private readonly IOrdenPagoRepository _ordenes;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IServerClock _clock;

    public OrdenPagoWriter(IOrdenPagoRepository ordenes, IReciboCobroRepository comprobantes, IReferenciasRepository referencias, IServerClock clock)
    {
        _ordenes = ordenes;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _clock = clock;
    }

    public async Task<Db.ProveedoresRecibos> GrabarAsync(CreateOrdenPagoDto dto, int idUsuario, CancellationToken ct = default)
    {
        if (dto.Imputaciones.Count == 0 && dto.Elementos.Count == 0)
            throw new BusinessException("La orden de pago tiene que tener al menos un comprobante o una forma de pago.");
        var repetidos = dto.Imputaciones.GroupBy(i => (i.IdComprobante, i.IdComprobanteTipo)).Where(g => g.Count() > 1).Select(g => g.Key.IdComprobante).ToList();
        if (repetidos.Count > 0)
            throw new BusinessException($"Comprobantes repetidos en la orden de pago: {string.Join(", ", repetidos)}.");

        var idProveedor = dto.IdProveedor!.Value;
        var c = await CodigosOrdenPago.LoadAsync(_referencias, ct);
        var proveedor = await _comprobantes.GetEntidadAsync(idProveedor, ct) ?? throw new NotFoundException($"Proveedor {idProveedor} no existe.");
        var planilla = await VentaContexto.GetPlanillaAbiertaAsync(_comprobantes, _referencias, idUsuario, c.Op, OrdenPagoContexto.Letra, ct);
        var imputaciones = await ValidarImputacionesAsync(dto, idProveedor, c, ct);
        var (puntoVenta, numero) = await NumerarAsync(dto, planilla.PuntoVenta!, c.Op, ct);
        var ahora = await _clock.GetNowAsync(ct);
        var fecha = dto.FechaEmision!.Value;
        var nroCompleto = $"{OrdenPagoContexto.Letra}-{puntoVenta}-{numero}";

        // El ERP tomaba los totales del navegador; acá salen de los elementos y de los comprobantes validados.
        var totalOrden = ImputacionRules.Redondear(dto.Elementos.Sum(e => e.Importe));
        var totalComprobantes = ImputacionRules.Redondear(dto.Imputaciones.Sum(i => i.ImporteComprobante));

        var nueva = new Db.ProveedoresRecibos
        {
            IdProveedor = idProveedor,
            Letra = OrdenPagoContexto.Letra,
            PuntoVenta = puntoVenta,
            Numero = numero,
            RazonSocial = VentaRules.Truncar(proveedor.RazonSocial, 50),
            IdCategoriaIva = proveedor.IdCategoriaIva,
            NroDoc = VentaRules.Truncar(proveedor.Cuit, 50),
            FechaEmision = fecha,
            IdUsuario = idUsuario,
            IdEmpresa = IdEmpresa,
            IdPlanillaCaja = planilla.IdPlanillaCaja,
            Total = totalOrden,
            Estado = await OrdenPagoContexto.EstadoAsync(_referencias, "ORDENESPAGO", "GENERADO", ct),
            IdOrdenPagoTipo = await _referencias.GetIdCategoriaAsync("ORDENPAGOTIPO", "PROVEEDOR", ct),
            Observaciones = dto.Observaciones ?? string.Empty,
            IdSucursal = dto.IdSucursal,
        };
        _ordenes.Add(nueva);
        await _ordenes.SaveChangesAsync(ct);

        // Imputación en orden, consumiendo el importe de la orden.
        var saldoOrden = totalOrden;
        foreach (var (imputacion, tipo, concepto) in imputaciones)
        {
            var idComprobante = imputacion.IdComprobante!.Value;
            var idTipo = imputacion.IdComprobanteTipo!.Value;
            var r = OrdenPagoRules.Imputar(tipo, imputacion.ImporteComprobante, saldoOrden);
            await _comprobantes.ImputarCtaCteAsync(idComprobante, idTipo, r.SaldoCtaCte, fecha, r.Cancelado, 0, ct);

            _ordenes.Add(new Db.EntidadOrdenPagoDocumentosProveedores
            {
                IdEntidadOrdenPago = nueva.IdProveedorRecibo,
                IdEntidad = idProveedor,
                NumeroOrdenPago = nroCompleto,
                ImporteOrdenPago = r.ImporteImputado,
                IdDocumentoProveedor = idComprobante,
                IdComprobanteTipo = idTipo,
                NumeroComprobante = concepto,
                ImporteComprobante = imputacion.ImporteComprobante,
                Saldo = r.SaldoComprobante,
            });

            await ActualizarEstadoImputadoAsync(tipo, idComprobante, r.Parcial, ct);
            saldoOrden = r.SaldoReciboRestante;
        }

        // Cta. cte. de la orden: lo pagado de más queda como saldo (negativo) a favor de la empresa.
        var (saldoCtaCte, cancelado) = ImputacionRules.SaldoRecibo(totalComprobantes, totalOrden);
        var ctaCte = new Db.EntidadesCtaCte
        {
            IdEntidad = idProveedor,
            IdComprobanteTipo = c.Op,
            IdComprobante = nueva.IdProveedorRecibo,
            Concepto = $"OP-{nroCompleto}",
            NroCuota = 1,
            Total = totalOrden,
            Saldo = saldoCtaCte,
            Cancelado = cancelado,
            Fecha = ahora,
            FechaVencimiento = fecha,
            FechaAnulacion = fecha,
            FechaPago = fecha,
            InteresAplicado = 0,
            Estado = await _referencias.IdAsync(EstadosCobranza.CtaCteGenerado, ct),
            Total2 = totalOrden,
            IdEmpresa = IdEmpresa,
            IdSucursal = dto.IdSucursal,
            IdUsuario = idUsuario,
        };
        _ordenes.Add(ctaCte);
        await _ordenes.SaveChangesAsync(ct);

        foreach (var e in dto.Elementos)
        {
            var idElemento = await RegistrarElementoAsync(e, c, nueva, planilla.IdPlanillaCaja, nroCompleto, fecha, ahora, idUsuario, ct);

            _ordenes.Add(new Db.ProveedoresRecibosDetalle
            {
                IdProveedorRecibo = nueva.IdProveedorRecibo,
                IdElementoCobroPago = e.IdElementoCobro,
                Descripcion = e.Descripcion ?? string.Empty,
                Detalle = e.Descripcion ?? string.Empty,
                IdBanco = e.IdBancoOrigen,
                IdSucursal = e.IdSucursalOrigen,
                Banco = e.Banco ?? string.Empty,
                Sucursal = e.Sucursal ?? string.Empty,
                Recepcion = e.FechaRecepcion ?? fecha,
                Emision = e.FechaEmision ?? fecha,
                Vto = e.FechaVencimiento ?? fecha,
                Nro = e.Numero ?? string.Empty,
                IdElemento = idElemento,
                Total = e.Importe,
            });

            _ordenes.Add(new Db.EntidadesCtaCteMovimientos
            {
                IdEntidadCtaCte = ctaCte.IdEntidadCtaCte,
                Concepto = $"OP -{nroCompleto}",
                AfavorEntidad = e.Importe,
                EnContraEntidad = 0,
                Fecha = ahora,
                IdElementoCobroPago = e.IdElementoCobro,
                IdElemento = idElemento,
                IdComprobanteTipo = c.Op,
                IdComprobante = nueva.IdProveedorRecibo,
            });
        }

        await _ordenes.SaveChangesAsync(ct);
        return nueva;
    }

    /// <summary>Cada comprobante tiene que ser del proveedor, estar pendiente y el importe tiene que ser su saldo (el ERP confiaba en el navegador).</summary>
    private async Task<List<(ImputacionPagoDto Imputacion, TipoImputacionPago Tipo, string? Concepto)>> ValidarImputacionesAsync(
        CreateOrdenPagoDto dto, int idProveedor, CodigosOrdenPago c, CancellationToken ct)
    {
        var resultado = new List<(ImputacionPagoDto, TipoImputacionPago, string?)>();
        foreach (var imputacion in dto.Imputaciones)
        {
            var idComprobante = imputacion.IdComprobante!.Value;
            var idTipo = imputacion.IdComprobanteTipo!.Value;
            var tipo = c.Clasificar(idTipo);
            if (tipo == TipoImputacionPago.NoSoportado)
                throw new BusinessException($"El tipo de comprobante {idTipo} no se puede imputar en una orden de pago.");

            var filas = await _comprobantes.GetCtaCtePendienteAsync(idProveedor, idComprobante, idTipo, ct);
            if (filas.Count == 0)
                throw new BusinessException($"El comprobante {idComprobante} (tipo {idTipo}) no está pendiente en la cuenta corriente del proveedor.");

            var fila = filas.FirstOrDefault(f => Math.Abs(ImputacionRules.Redondear(c.SaldoVisible(idTipo, f.Saldo ?? 0)) - imputacion.ImporteComprobante) <= Tolerancia);
            if (fila is null)
            {
                var saldo = ImputacionRules.Redondear(c.SaldoVisible(idTipo, filas[0].Saldo ?? 0));
                throw new BusinessException(
                    $"El importe del comprobante {filas[0].Concepto} ({Formato.Importe(imputacion.ImporteComprobante)}) no coincide con su saldo pendiente ({Formato.Importe(saldo)}).");
            }
            resultado.Add((imputacion, tipo, fila.Concepto));
        }
        return resultado;
    }

    private async Task<(string PuntoVenta, string Numero)> NumerarAsync(CreateOrdenPagoDto dto, string puntoVentaPlanilla, int op, CancellationToken ct)
    {
        if (await VentaContexto.NumeracionManualAsync(_referencias, ct, "OP"))
        {
            if (string.IsNullOrWhiteSpace(dto.PuntoVenta) || string.IsNullOrWhiteSpace(dto.Numero))
                throw new BusinessException("La numeración de órdenes de pago es manual (NUMERACION/OP = 1): informar punto de venta y número.");
            await _comprobantes.ReservarNumeroAsync(dto.PuntoVenta, OrdenPagoContexto.Letra, op, ct);
            return (dto.PuntoVenta, dto.Numero);
        }

        var numero = await _comprobantes.ReservarNumeroAsync(puntoVentaPlanilla, OrdenPagoContexto.Letra, op, ct)
            ?? throw new BusinessException($"No existe el punto de venta {puntoVentaPlanilla} para órdenes de pago (letra {OrdenPagoContexto.Letra}).");
        return (puntoVentaPlanilla, numero.ToString().PadLeft(8, '0'));
    }

    private async Task ActualizarEstadoImputadoAsync(TipoImputacionPago tipo, int idComprobante, bool parcial, CancellationToken ct)
    {
        switch (tipo)
        {
            case TipoImputacionPago.Compra:
                await _comprobantes.SetEstadoDocumentoProveedorAsync(idComprobante,
                    await OrdenPagoContexto.EstadoAsync(_referencias, "DOCUMENTOSPROVEEDOR", parcial ? "PAGADO PARCIAL" : "PAGADO", ct), ct);
                break;
            case TipoImputacionPago.DocumentoCliente:
                await _comprobantes.SetEstadoDocumentoClienteAsync(idComprobante, await _referencias.IdAsync(EstadosCobranza.DocumentoCobrado, ct), ct);
                break;
            case TipoImputacionPago.Recibo:
                await _comprobantes.SetEstadoReciboAsync(idComprobante, await _referencias.IdAsync(EstadosCobranza.ReciboRelacionado, ct), ct);
                break;
            case TipoImputacionPago.OrdenPago:
                await _comprobantes.SetEstadoOrdenPagoAsync(idComprobante, await _referencias.IdAsync(EstadosCobranza.OrdenPagoRelacionada, ct), ct);
                break;
        }
    }

    /// <summary>Registra la forma de pago y devuelve el ID que el detalle guarda en ID_Elemento.</summary>
    private async Task<int> RegistrarElementoAsync(
        ElementoPagoDto e, CodigosOrdenPago c, Db.ProveedoresRecibos orden, int idPlanillaCaja, string nroCompleto,
        DateTime fecha, DateTime ahora, int idUsuario, CancellationToken ct)
    {
        var op = c.Op;
        var idOrden = orden.IdProveedorRecibo;
        var clase = c.ClasificarElemento(e.IdElementoCobro!.Value);
        var idElemento = 1;
        var fechaCaja = ahora;

        switch (clase)
        {
            case ElementoPago.ChequeTercero:
            {
                var cheque = await _ordenes.GetChequeTerceroAsync(e.IdCheque, ct) ?? throw new NotFoundException($"Cheque de terceros {e.IdCheque} no existe.");
                if (cheque.Importe != e.Importe)
                    throw new BusinessException($"El importe informado no coincide con el del cheque {cheque.Nro} ({Formato.Importe(cheque.Importe ?? 0)}).");
                var enCartera = await OrdenPagoContexto.EstadoAsync(_referencias, "CLIENTECHEQUE", "EN CARTERA", ct);
                if (!await _ordenes.AsignarChequeTerceroAsync(e.IdCheque, idOrden, op, await OrdenPagoContexto.EstadoAsync(_referencias, "CLIENTECHEQUE", "ENTREGADO", ct), enCartera, ct))
                    throw new ConflictException($"El cheque {cheque.Nro} ya no está en cartera.");

                var entregado = new Db.ProveedoresCheques
                {
                    IdProveedor = orden.IdProveedor,
                    IdBanco = cheque.IdBanco,
                    IdSucursal = cheque.IdSucursal, // el ERP grababa la sucursal de la empresa
                    Nro = cheque.Nro,
                    Importe = cheque.Importe,
                    FechaRecepcion = cheque.FechaRecepcion,
                    FechaEmision = cheque.FechaEmision,
                    FechaVencimiento = cheque.FechaVencimiento,
                    ValorToma = cheque.Importe,
                    IdEmpresa = IdEmpresa,
                    IdUsuario = idUsuario,
                    Tipo = await _referencias.GetIdCategoriaAsync("PROVEEDORCHEQUE", "AUTOMATICO", ct),
                    IdProveedorRecibo = idOrden,
                    Estado = await OrdenPagoContexto.EstadoAsync(_referencias, "PROVEEDORCHEQUE", "ENTREGADO", ct),
                    IdClienteRecibo = 0,
                    IdComprobanteTipo = op,
                };
                _ordenes.Add(entregado);
                await _ordenes.SaveChangesAsync(ct);
                idElemento = entregado.IdProveedorCheque;
                break;
            }

            case ElementoPago.ChequePropio:
            {
                var row = await _ordenes.GetChequePropioAsync(e.IdCheque, ct) ?? throw new NotFoundException($"Cheque propio {e.IdCheque} no existe.");
                var enCartera = await OrdenPagoContexto.EstadoAsync(_referencias, "PERSONALCHEQUE", "EN CARTERA", ct);
                var emision = e.FechaEmision ?? fecha;
                var vencimiento = e.FechaVencimiento ?? fecha;
                if (!await _ordenes.EntregarChequePropioAsync(e.IdCheque, e.Numero ?? row.Cheque.NroCheque ?? string.Empty, idOrden, op, emision, vencimiento, e.Importe,
                        await OrdenPagoContexto.EstadoAsync(_referencias, "PERSONALCHEQUE", "ENTREGADO", ct), enCartera, ct))
                    throw new ConflictException($"El cheque propio {row.Cheque.NroCheque} no está disponible.");

                var mov = MovimientoBanco(row.Cheque.IdBancoCuenta ?? 0, MovimientoBancoChequePropio, e.Importe, fecha, op, idOrden,
                    row.IdBanco ?? 0, row.IdBancoSucursal ?? 0, row.IdCuentaTipo ?? 0, row.NroCuenta, 0, 0, 0, null,
                    await _referencias.IdAsync(EstadosCobranza.MovimientoBancoActivo, ct));
                _ordenes.Add(mov);
                idElemento = row.Cheque.IdBancoCheque;
                fechaCaja = fecha;
                break;
            }

            case ElementoPago.DepositoBancario:
            case ElementoPago.Tarjeta:
            {
                if (e.IdBancoCuenta <= 0)
                    throw new BusinessException("Las transferencias / depósitos y las tarjetas requieren la cuenta bancaria (IdBancoCuenta).");
                var tarjeta = clase == ElementoPago.Tarjeta; // el ERP no guarda el origen en los pagos con tarjeta
                var mov = MovimientoBanco(e.IdBancoCuenta, MovimientoBancoDeposito, e.Importe, ahora, op, idOrden,
                    tarjeta ? 0 : e.IdBancoOrigen, tarjeta ? 0 : e.IdSucursalOrigen, tarjeta ? 0 : e.IdTipoOrigen, tarjeta ? null : e.NroCuentaOrigen,
                    e.IdBancoDestino, e.IdSucursalDestino, e.IdTipoDestino, e.NroCuentaDestino,
                    await _referencias.IdAsync(EstadosCobranza.MovimientoBancoActivo, ct));
                _ordenes.Add(mov);
                await _ordenes.SaveChangesAsync(ct);
                idElemento = mov.IdBancoCuentaMovimiento;
                break;
            }

            case ElementoPago.Retencion:
            {
                if (e.IdRetencionTipo <= 0)
                    throw new BusinessException("Las retenciones requieren el tipo de retención (IdRetencionTipo).");
                var retencion = new Db.Retenciones
                {
                    IdRetencionTipo = e.IdRetencionTipo,
                    Descripcion = e.Descripcion ?? string.Empty,
                    IdComprobanteTipo = op,
                    IdComprobante = idOrden,
                    NroComprobante = e.Numero ?? string.Empty,
                    FechaEmision = e.FechaEmision ?? fecha,
                    FechaRecepcion = e.FechaRecepcion ?? fecha,
                    Total = e.Importe,
                    Estado = await _referencias.IdAsync(EstadosCobranza.RetencionGenerada, ct),
                    IdEntidad = orden.IdProveedor,
                };
                _ordenes.Add(retencion);
                await _ordenes.SaveChangesAsync(ct);
                idElemento = retencion.IdRetencion;
                break;
            }
        }

        // Toda forma de pago conocida sale de la caja: Haber = importe, Total2 negativo.
        if (clase != ElementoPago.Otro)
            _ordenes.Add(new Db.CajasPlanillasDetalle
            {
                IdCajaPlanilla = idPlanillaCaja,
                IdElementoCobro = e.IdElementoCobro,
                IdComprobanteTipo = op,
                IdComprobante = idOrden,
                Reducida = "OP",
                Descripcion = nroCompleto,
                Obsevaciones = string.Empty,
                Automatico = true,
                Fecha = fechaCaja,
                Total = e.Importe,
                Debe = 0,
                Haber = e.Importe,
                Total2 = -e.Importe,
            });

        return idElemento;
    }

    private static Db.BancosCuentasMovimientos MovimientoBanco(
        int idCuenta, int tipoMovimiento, decimal importe, DateTime fecha, int op, int idOrden,
        int bancoOrigen, int sucursalOrigen, int tipoOrigen, string? nroOrigen, int bancoDestino, int sucursalDestino, int tipoDestino, string? nroDestino, int estado) => new()
    {
        IdBancoCuenta = idCuenta,
        IdMovimientoTipo = tipoMovimiento,
        Debe = 0,
        Haber = importe,
        Importe = importe,
        Fecha = fecha,
        IdBancoOrigen = bancoOrigen,
        IdBancoSucursalOrigen = sucursalOrigen,
        CuentaTipoOrigen = tipoOrigen,
        NroCuentaOrigen = nroOrigen ?? string.Empty,
        IdBancoDestino = bancoDestino,
        IdBancoSucursalDestino = sucursalDestino,
        CuentaTipoDestino = tipoDestino,
        NroCuentaDestino = nroDestino ?? string.Empty,
        IdComprobanteTipo = op,
        IdComprobante = idOrden,
        Estado = estado,
        Total = -importe,
    };
}

public interface IAnularOrdenPagoUseCase
{
    Task ExecuteAsync(int idOrdenPago, CancellationToken cancellationToken = default);
}

/// <summary>
/// Anulación de orden de pago (Editar_Ws de FrmOrdendePago): orden, detalle, caja, cheques (de terceros vuelven a cartera,
/// propios se anulan), bancos, retenciones, devolución del saldo a los comprobantes imputados con su estado, y cta. cte. de la orden.
/// </summary>
public sealed class AnularOrdenPagoUseCase : IAnularOrdenPagoUseCase
{
    private readonly IOrdenPagoRepository _ordenes;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public AnularOrdenPagoUseCase(
        IOrdenPagoRepository ordenes, IReciboCobroRepository comprobantes, IReferenciasRepository referencias,
        IUnitOfWork unitOfWork, IServerClock clock, ICurrentUser currentUser)
    {
        _ordenes = ordenes;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task ExecuteAsync(int idOrdenPago, CancellationToken cancellationToken = default)
    {
        CompraContexto.RequireIdUsuario(_currentUser);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var c = await CodigosOrdenPago.LoadAsync(_referencias, ct);
            var orden = await _ordenes.GetOrdenAsync(idOrdenPago, ct) ?? throw new NotFoundException($"Orden de pago {idOrdenPago} no existe.");
            await _comprobantes.BloquearEntidadAsync(orden.IdProveedor ?? 0, ct);

            // El ERP anulaba en cualquier estado distinto del que mandaba la pantalla (incluso RELACIONADA, ya usada en otro comprobante).
            if (orden.Estado != await OrdenPagoContexto.EstadoAsync(_referencias, "ORDENESPAGO", "GENERADO", ct))
                throw new ConflictException($"La orden de pago {idOrdenPago} no se puede anular en su estado actual (anulada o ya imputada en otro comprobante).");

            var op = c.Op;
            var ahora = await _clock.GetNowAsync(ct);
            await _comprobantes.SetEstadoOrdenPagoAsync(idOrdenPago, await OrdenPagoContexto.EstadoAsync(_referencias, "ORDENESPAGO", "ANULADO", ct), ct);
            await _ordenes.AnularDetalleAsync(idOrdenPago, ahora, ct);
            await _comprobantes.AnularCajaAsync(op, idOrdenPago, ct);
            await _ordenes.DevolverChequesTercerosAsync(idOrdenPago, op, await OrdenPagoContexto.EstadoAsync(_referencias, "CLIENTECHEQUE", "EN CARTERA", ct), ct);
            await _ordenes.AnularChequesProveedorAsync(idOrdenPago, op, await OrdenPagoContexto.EstadoAsync(_referencias, "PROVEEDORCHEQUE", "ANULADO", ct), ct);
            await _ordenes.AnularChequesPropiosAsync(idOrdenPago, op, await OrdenPagoContexto.EstadoAsync(_referencias, "PERSONALCHEQUE", "ANULADO", ct), ct);
            await _comprobantes.AnularMovimientosBancoAsync(op, idOrdenPago, await _referencias.IdAsync(EstadosCobranza.MovimientoBancoAnulado, ct), ahora, ct);
            await _comprobantes.AnularRetencionesAsync(op, idOrdenPago, await _referencias.IdAsync(EstadosCobranza.RetencionAnulada, ct), ct);

            var imputaciones = await _ordenes.GetImputacionesAsync(idOrdenPago, ct);
            if (imputaciones.Count > 0)
                await _ordenes.BorrarImputacionesAsync(idOrdenPago, ct);

            foreach (var imputacion in imputaciones)
            {
                var idComprobante = imputacion.IdDocumentoProveedor ?? 0;
                var idTipo = imputacion.IdComprobanteTipo ?? 0;
                await _comprobantes.RevertirImputacionCtaCteAsync(idComprobante, idTipo, imputacion.ImporteOrdenPago ?? 0, 0, ahora, ct);
                await RestaurarEstadoAsync(c, idTipo, idComprobante, ct);
            }

            var idCtaCte = await _comprobantes.GetIdCtaCteAsync(op, idOrdenPago, ct);
            if (idCtaCte is not null)
                await _comprobantes.AnularCtaCteAsync(idCtaCte.Value, await _referencias.IdAsync(EstadosCobranza.CtaCteAnulado, ct), ahora, ct);
            return true;
        }, cancellationToken);
    }

    private async Task RestaurarEstadoAsync(CodigosOrdenPago c, int idTipo, int idComprobante, CancellationToken ct)
    {
        switch (c.Clasificar(idTipo))
        {
            case TipoImputacionPago.Compra:
                // Si otra orden de pago lo sigue pagando queda PAGADO PARCIAL (el SP del ERP miraba todas las imputaciones de la base).
                var parcial = await _ordenes.TieneImputacionesAsync(idComprobante, idTipo, ct);
                await _comprobantes.SetEstadoDocumentoProveedorAsync(idComprobante,
                    await OrdenPagoContexto.EstadoAsync(_referencias, "DOCUMENTOSPROVEEDOR", parcial ? "PAGADO PARCIAL" : "GENERADO", ct), ct);
                break;
            case TipoImputacionPago.DocumentoCliente:
                var estado = idTipo == c.Fv && await _comprobantes.TieneRelacionAsync(idComprobante, ct)
                    ? EstadosCobranza.DocumentoCancelado
                    : EstadosCobranza.DocumentoGenerado;
                await _comprobantes.SetEstadoDocumentoClienteAsync(idComprobante, await _referencias.IdAsync(estado, ct), ct);
                break;
            case TipoImputacionPago.Recibo:
                await _comprobantes.SetEstadoReciboAsync(idComprobante, await _referencias.IdAsync(EstadosCobranza.ReciboGenerado, ct), ct);
                break;
            case TipoImputacionPago.OrdenPago:
                await _comprobantes.SetEstadoOrdenPagoAsync(idComprobante, await _referencias.IdAsync(EstadosCobranza.OrdenPagoGenerada, ct), ct);
                break;
        }
    }
}
