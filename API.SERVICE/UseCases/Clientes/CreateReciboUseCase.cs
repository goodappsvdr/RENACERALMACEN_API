using API.SERVICE.Domain;
using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Mappings.Clientes;
using API.SERVICE.Models.Clientes;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Clientes;

public interface ICreateReciboUseCase
{
    Task<EntidadReciboDisplay> ExecuteAsync(CreateReciboDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alta de recibo de cobro en una transacción. Port de Agregar_Ws (FrmRecibos):
/// recibo, imputación a comprobantes (cta. cte. + estados), cta. cte. del recibo, formas de pago
/// (caja, cheques, bancos, retenciones), detalle, movimientos de cta. cte. y numeración.
/// </summary>
public sealed class CreateReciboUseCase : ICreateReciboUseCase
{
    private const int IdEmpresa = 1;

    /// <summary>EntidadesRecibosDetalle.ID_Elemento: el ERP siempre graba 1.</summary>
    private const int IdElemento = 1;

    private const int MovimientoBancoDeposito = 1;
    private const int MovimientoBancoTarjeta = 4;

    /// <summary>Tolerancia al validar el importe de cada comprobante (el formulario redondea a 2 decimales).</summary>
    private const decimal Tolerancia = 0.01m;

    private readonly IReciboCobroRepository _repository;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public CreateReciboUseCase(
        IReciboCobroRepository repository,
        IReferenciasRepository referencias,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<EntidadReciboDisplay> ExecuteAsync(CreateReciboDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = ReciboContexto.RequireIdUsuario(_currentUser);
        ValidarEstructura(dto);

        // Mismas fórmulas que el formulario: el recargo de tarjeta suma en los dos totales.
        var totalRecibo = ImputacionRules.Redondear(dto.Elementos.Sum(e => e.Importe) + dto.RecargoTarjeta);
        var totalComprobantes = ImputacionRules.Redondear(dto.Imputaciones.Sum(i => i.ImporteComprobante) + dto.RecargoTarjeta);
        var fechaEmision = dto.FechaEmision!.Value;
        var idEntidad = dto.IdEntidad!.Value;
        var idSucursal = dto.IdSucursal!.Value;

        var recibo = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Primero el lock del cliente: un segundo recibo simultáneo espera y, al entrar, ve los comprobantes ya cancelados.
            await _repository.BloquearEntidadAsync(idEntidad, ct);

            var codigos = await CodigosCobranza.LoadAsync(_referencias, ct);
            var rec = codigos.Rec;

            var entidad = await _repository.GetEntidadAsync(idEntidad, ct)
                ?? throw new NotFoundException($"Entidad {idEntidad} no existe.");
            var planilla = await ReciboContexto.GetPlanillaAbiertaAsync(_repository, _referencias, idUsuario, rec, ct);
            var imputaciones = await ValidarImputacionesAsync(dto, idEntidad, codigos, ct);
            var (puntoVenta, numero) = await NumerarAsync(dto, planilla.PuntoVenta!, rec, ct);
            var ahora = await _clock.GetNowAsync(ct);
            var nroCompleto = $"{ReciboContexto.Letra}-{puntoVenta}-{numero}";

            // 1. Recibo
            var nuevo = new Db.EntidadesRecibos
            {
                IdComprobanteTipo = rec,
                IdEntidad = idEntidad,
                Letra = ReciboContexto.Letra,
                PuntoVenta = puntoVenta,
                Numero = numero,
                RazonSocial = Truncar(entidad.RazonSocial, 50), // el SP recibe varchar(50)
                IdCategoriaIva = entidad.IdCategoriaIva,
                NroDoc = Truncar(entidad.Cuit, 50),
                FechaEmision = fechaEmision,
                IdUsuario = idUsuario,
                IdEmpresa = IdEmpresa,
                IdPlanillaCaja = planilla.IdPlanillaCaja,
                Total = totalRecibo,
                Estado = ImputacionRules.EstadoReciboNuevo,
                Observaciones = dto.Observaciones ?? string.Empty,
                IdSucursal = idSucursal,
            };
            _repository.Add(nuevo);
            await _repository.SaveChangesAsync(ct);

            // 2. Imputación a comprobantes, en orden, consumiendo el importe del recibo
            var saldoRecibo = totalRecibo;
            decimal interesRecibo = 0;
            foreach (var (imputacion, tipo, concepto) in imputaciones)
            {
                var idComprobante = imputacion.IdComprobante!.Value;
                var idTipo = imputacion.IdComprobanteTipo!.Value;

                // Como en el ERP, la cta. cte. del recibo guarda el interés del último comprobante imputado.
                interesRecibo = imputacion.InteresAplicado;

                var resultado = ImputacionRules.Imputar(tipo, imputacion.ImporteComprobante, saldoRecibo);
                await _repository.ImputarCtaCteAsync(idComprobante, idTipo, resultado.SaldoCtaCte, fechaEmision, resultado.Cancelado, imputacion.InteresAplicado, ct);

                _repository.Add(new Db.EntidadRecibosDocumentosCliente
                {
                    IdEntidadRecibo = nuevo.IdEntidadRecibo,
                    IdEntidad = idEntidad,
                    NumeroRecibo = nroCompleto,
                    ImporteRecibo = resultado.ImporteImputado,
                    IdDocumentoCliente = idComprobante,
                    IdComprobanteTipo = idTipo,
                    NumeroComprobante = concepto,
                    ImporteComprobante = imputacion.ImporteComprobante,
                    Saldo = resultado.SaldoComprobante,
                });

                await ActualizarEstadoImputadoAsync(tipo, idComprobante, resultado.Parcial, ct);
                saldoRecibo = resultado.SaldoReciboRestante;
            }

            // 3. Cta. cte. del recibo
            var (saldoCtaCte, cancelado) = ImputacionRules.SaldoRecibo(totalComprobantes, totalRecibo);
            var ctaCte = new Db.EntidadesCtaCte
            {
                IdEntidad = idEntidad,
                IdComprobanteTipo = rec,
                IdComprobante = nuevo.IdEntidadRecibo,
                Concepto = $"REC-{nroCompleto}",
                NroCuota = 1,
                Total = totalRecibo,
                Saldo = saldoCtaCte,
                Cancelado = cancelado,
                Fecha = ahora,
                FechaVencimiento = fechaEmision,
                FechaAnulacion = fechaEmision,
                FechaPago = fechaEmision,
                InteresAplicado = interesRecibo,
                Estado = await _referencias.IdAsync(EstadosCobranza.CtaCteGenerado, ct),
                Total2 = -totalRecibo,
                IdEmpresa = IdEmpresa,
                IdSucursal = idSucursal,
                IdUsuario = idUsuario,
            };
            _repository.Add(ctaCte);
            await _repository.SaveChangesAsync(ct);

            // 4. Formas de pago
            foreach (var elemento in dto.Elementos)
            {
                await RegistrarElementoAsync(elemento, codigos, nuevo, planilla.IdPlanillaCaja, nroCompleto, fechaEmision, ahora, idUsuario, idSucursal, ct);

                _repository.Add(new Db.EntidadesRecibosDetalle
                {
                    IdEntidadRecibo = nuevo.IdEntidadRecibo,
                    IdElementoCobroPago = elemento.IdElementoCobro,
                    Descripcion = elemento.Descripcion ?? string.Empty,
                    Detalle = elemento.Descripcion ?? string.Empty,
                    IdBanco = elemento.IdBancoOrigen,
                    IdSucursal = elemento.IdSucursalOrigen,
                    Banco = elemento.Banco ?? string.Empty,
                    Sucursal = elemento.Sucursal ?? string.Empty,
                    Recepcion = elemento.FechaRecepcion ?? fechaEmision,
                    Emision = elemento.FechaEmision ?? fechaEmision,
                    Vto = elemento.FechaVencimiento ?? fechaEmision,
                    Nro = elemento.Numero ?? string.Empty,
                    IdElemento = IdElemento,
                    Total = elemento.Importe,
                });

                _repository.Add(new Db.EntidadesCtaCteMovimientos
                {
                    IdEntidadCtaCte = ctaCte.IdEntidadCtaCte,
                    Concepto = $"REC -{nroCompleto}",
                    AfavorEntidad = elemento.Importe,
                    EnContraEntidad = 0,
                    Fecha = ahora,
                    IdElementoCobroPago = elemento.IdElementoCobro,
                    IdElemento = IdElemento,
                    IdComprobanteTipo = rec,
                    IdComprobante = nuevo.IdEntidadRecibo,
                });
            }

            await _repository.SaveChangesAsync(ct);
            return nuevo;
        }, cancellationToken);

        return recibo.ToDisplay();
    }

    private static void ValidarEstructura(CreateReciboDto dto)
    {
        if (dto.Imputaciones.Count == 0 && dto.Elementos.Count == 0)
            throw new BusinessException("El recibo tiene que tener al menos un comprobante o una forma de pago.");

        var repetidos = dto.Imputaciones
            .GroupBy(i => (i.IdComprobante, i.IdComprobanteTipo))
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.IdComprobante)
            .ToList();
        if (repetidos.Count > 0)
            throw new BusinessException($"Comprobantes repetidos en el recibo: {string.Join(", ", repetidos)}.");
    }

    /// <summary>
    /// Cada comprobante tiene que ser del cliente, estar pendiente y el importe tiene que ser su saldo + interés
    /// (el ERP confiaba en el importe que mandaba el navegador).
    /// </summary>
    private async Task<List<(ImputacionDto Imputacion, TipoImputacion Tipo, string? Concepto)>> ValidarImputacionesAsync(
        CreateReciboDto dto, int idEntidad, CodigosCobranza codigos, CancellationToken ct)
    {
        var resultado = new List<(ImputacionDto, TipoImputacion, string?)>();
        foreach (var imputacion in dto.Imputaciones)
        {
            var idComprobante = imputacion.IdComprobante!.Value;
            var idTipo = imputacion.IdComprobanteTipo!.Value;

            var tipo = codigos.Clasificar(idTipo);
            if (tipo == TipoImputacion.NoSoportado)
                throw new BusinessException($"El tipo de comprobante {idTipo} no se puede imputar en un recibo.");

            var filas = await _repository.GetCtaCtePendienteAsync(idEntidad, idComprobante, idTipo, ct);
            if (filas.Count == 0)
                throw new BusinessException($"El comprobante {idComprobante} (tipo {idTipo}) no está pendiente en la cuenta corriente del cliente.");

            var coincide = filas.FirstOrDefault(f =>
            {
                var esperado = ImputacionRules.Redondear(ImputacionRules.SaldoVisible(idTipo, f.Saldo ?? 0)) + imputacion.InteresAplicado;
                return Math.Abs(esperado - imputacion.ImporteComprobante) <= Tolerancia;
            });
            if (coincide is null)
            {
                var saldo = ImputacionRules.Redondear(ImputacionRules.SaldoVisible(idTipo, filas[0].Saldo ?? 0));
                throw new BusinessException(
                    $"El importe del comprobante {filas[0].Concepto} ({Formato.Importe(imputacion.ImporteComprobante)}) no coincide con su saldo pendiente ({Formato.Importe(saldo)}) más el interés ({Formato.Importe(imputacion.InteresAplicado)}).");
            }

            resultado.Add((imputacion, tipo, coincide.Concepto));
        }

        return resultado;
    }

    private async Task<(string PuntoVenta, string Numero)> NumerarAsync(CreateReciboDto dto, string puntoVentaPlanilla, int rec, CancellationToken ct)
    {
        if (await ReciboContexto.NumeracionManualAsync(_referencias, ct))
        {
            if (string.IsNullOrWhiteSpace(dto.PuntoVenta) || string.IsNullOrWhiteSpace(dto.Numero))
                throw new BusinessException("La numeración de recibos es manual (NUMERACION/REC = 1): informar punto de venta y número.");

            // El ERP incrementa igual el contador del punto de venta, aunque el número sea manual.
            await _repository.ReservarNumeroAsync(dto.PuntoVenta, ReciboContexto.Letra, rec, ct);
            return (dto.PuntoVenta, dto.Numero);
        }

        var numero = await _repository.ReservarNumeroAsync(puntoVentaPlanilla, ReciboContexto.Letra, rec, ct)
            ?? throw new BusinessException($"No existe el punto de venta {puntoVentaPlanilla} para recibos (letra {ReciboContexto.Letra}).");
        return (puntoVentaPlanilla, ReciboContexto.FormatearNumero(numero));
    }

    private async Task ActualizarEstadoImputadoAsync(TipoImputacion tipo, int idComprobante, bool parcial, CancellationToken ct)
    {
        switch (tipo)
        {
            case TipoImputacion.Venta:
                await _repository.SetEstadoDocumentoClienteAsync(idComprobante,
                    await _referencias.IdAsync(parcial ? EstadosCobranza.DocumentoCobradoParcial : EstadosCobranza.DocumentoCobrado, ct), ct);
                break;
            case TipoImputacion.Compra:
                // El ERP actualiza DocumentosCliente también para FC/COM (no DocumentosProveedor).
                await _repository.SetEstadoDocumentoClienteAsync(idComprobante,
                    await _referencias.IdAsync(parcial ? EstadosCobranza.DocumentoPagadoParcial : EstadosCobranza.DocumentoPagado, ct), ct);
                break;
            case TipoImputacion.NotaCredito:
                await _repository.SetEstadoDocumentoClienteAsync(idComprobante, await _referencias.IdAsync(EstadosCobranza.DocumentoCobrado, ct), ct);
                break;
            case TipoImputacion.Recibo:
                await _repository.SetEstadoReciboAsync(idComprobante, await _referencias.IdAsync(EstadosCobranza.ReciboRelacionado, ct), ct);
                break;
            case TipoImputacion.OrdenPago:
                await _repository.SetEstadoOrdenPagoAsync(idComprobante, await _referencias.IdAsync(EstadosCobranza.OrdenPagoRelacionada, ct), ct);
                break;
        }
    }

    private async Task RegistrarElementoAsync(
        ElementoCobroDto e, CodigosCobranza codigos, Db.EntidadesRecibos recibo, int idPlanillaCaja, string nroCompleto,
        DateTime fechaEmision, DateTime ahora, int idUsuario, int idSucursal, CancellationToken ct)
    {
        var rec = codigos.Rec;
        var idRecibo = recibo.IdEntidadRecibo;
        var clase = codigos.ClasificarElemento(e.IdElementoCobro!.Value);

        switch (clase)
        {
            case ElementoCobro.Cheque:
                _repository.Add(new Db.EntidadesCheques
                {
                    IdEntidad = recibo.IdEntidad,
                    IdBanco = e.IdBancoOrigen,
                    IdSucursal = e.IdSucursalOrigen,
                    Nro = e.Numero ?? string.Empty,
                    Importe = e.Importe,
                    FechaRecepcion = e.FechaRecepcion ?? fechaEmision,
                    FechaEmision = e.FechaEmision ?? fechaEmision,
                    FechaVencimiento = e.FechaVencimiento ?? fechaEmision,
                    ValorToma = e.Importe,
                    IdEmpresa = IdEmpresa,
                    IdUsuario = idUsuario,
                    Tipo = await _referencias.GetIdCategoriaAsync("CLIENTECHEQUE", "AUTOMATICO", ct),
                    IdEntidadRecibo = idRecibo,
                    Estado = await _referencias.IdAsync(EstadosCobranza.ChequeEnCartera, ct),
                    IdProveedorRecibo = 0,
                    IdComprobanteTipo = 0,
                    Observaciones = string.Empty,
                    IdSucursalEmpresa = idSucursal,
                    IdEntidadReciboTipo = rec,
                });
                break;

            case ElementoCobro.DepositoBancario:
            case ElementoCobro.Tarjeta:
                if (e.IdBancoCuenta <= 0)
                    throw new BusinessException("Los depósitos bancarios y las tarjetas requieren la cuenta bancaria (IdBancoCuenta).");

                _repository.Add(new Db.BancosCuentasMovimientos
                {
                    IdBancoCuenta = e.IdBancoCuenta,
                    IdMovimientoTipo = clase == ElementoCobro.Tarjeta ? MovimientoBancoTarjeta : MovimientoBancoDeposito,
                    Debe = e.Importe,
                    Haber = 0,
                    Importe = e.Importe,
                    Fecha = ahora,
                    IdBancoOrigen = e.IdBancoOrigen,
                    IdBancoSucursalOrigen = e.IdSucursalOrigen,
                    CuentaTipoOrigen = e.IdTipoOrigen,
                    NroCuentaOrigen = e.Numero ?? string.Empty,
                    IdBancoDestino = e.IdBancoDestino,
                    IdBancoSucursalDestino = e.IdSucursalDestino,
                    CuentaTipoDestino = e.IdTipoDestino,
                    NroCuentaDestino = e.NroCuentaDestino ?? string.Empty,
                    IdComprobanteTipo = rec,
                    IdComprobante = idRecibo,
                    Estado = await _referencias.IdAsync(EstadosCobranza.MovimientoBancoActivo, ct),
                    Total = e.Importe,
                });
                break;

            case ElementoCobro.Retencion:
                if (e.IdRetencionTipo <= 0)
                    throw new BusinessException("Las retenciones requieren el tipo de retención (IdRetencionTipo).");

                _repository.Add(new Db.Retenciones
                {
                    IdRetencionTipo = e.IdRetencionTipo,
                    Descripcion = e.Descripcion ?? string.Empty,
                    IdComprobanteTipo = rec,
                    IdComprobante = idRecibo,
                    NroComprobante = e.Numero ?? string.Empty,
                    FechaEmision = e.FechaEmision ?? fechaEmision,
                    FechaRecepcion = e.FechaRecepcion ?? fechaEmision,
                    Total = e.Importe,
                    Estado = await _referencias.IdAsync(EstadosCobranza.RetencionGenerada, ct),
                    IdEntidad = recibo.IdEntidad,
                });
                break;
        }

        // Todo elemento conocido (efectivo, cheque, depósito, tarjeta, retención) entra en la planilla de caja.
        if (clase != ElementoCobro.Otro)
        {
            _repository.Add(new Db.CajasPlanillasDetalle
            {
                IdCajaPlanilla = idPlanillaCaja,
                IdElementoCobro = e.IdElementoCobro,
                IdComprobanteTipo = rec,
                IdComprobante = idRecibo,
                Reducida = "REC",
                Descripcion = nroCompleto,
                Obsevaciones = string.Empty,
                Automatico = true,
                Fecha = ahora,
                Total = e.Importe,
                Debe = e.Importe,
                Haber = 0,
                Total2 = e.Importe,
            });
        }
    }

    private static string? Truncar(string? valor, int largo) =>
        valor is { Length: > 0 } && valor.Length > largo ? valor[..largo] : valor;
}
