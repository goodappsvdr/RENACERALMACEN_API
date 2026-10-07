using API.SERVICE.Domain.Afip;
using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Afip;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Compras;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;
using API.SERVICE.Mappings.Compras;
using API.SERVICE.Models.Compras;
using API.SERVICE.Models.Ventas;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Compras;

/// <summary>Tipos y reglas comunes de los comprobantes de compra.</summary>
internal static class CompraContexto
{
    public static int RequireIdUsuario(ICurrentUser user) =>
        user.IdUsuario ?? throw new ForbiddenException("El usuario no tiene un registro en Usuarios del ERP; no puede operar comprobantes de compra.");

    public static bool EsGlobal(ICurrentUser user) => user.IsInRole("CEO") || user.IsInRole("CTO");

    public sealed record Tipos(int Fc, int Com, int Rc, int Oc)
    {
        /// <summary>Tipos cuyo saldo de stock mira el SP ..._Pendiente_RemitarNuevo (4, 12, 6, 22).</summary>
        public int[] ConStockPendiente => [Fc, Com, Rc, Oc];
    }

    public static async Task<Tipos> TiposAsync(IReferenciasRepository referencias, CancellationToken ct) => new(
        await referencias.GetParametroEnteroAsync("COMPROBANTE", "FC", ct),
        await referencias.GetParametroEnteroAsync("COMPROBANTE", "COM", ct),
        await referencias.GetParametroEnteroAsync("COMPROBANTE", "RC", ct),
        await referencias.GetParametroEnteroAsync("COMPROBANTE", "OC", ct));

    public static Task<int> EstadoAsync(IReferenciasRepository referencias, string nombre, CancellationToken ct) =>
        referencias.GetIdEstadoAsync("DOCUMENTOSPROVEEDOR", nombre, ct);
}

public interface IIniciarFacturaCompraUseCase
{
    Task<NuevaFacturaCompraDisplay> ExecuteAsync(int idProveedor, int idSucursal, CancellationToken cancellationToken = default);
}

/// <summary>Planilla abierta y letras posibles según la sucursal y el proveedor (IniciarPuntoVenta_WS + CargarCboLetra_WS).</summary>
public sealed class IniciarFacturaCompraUseCase : IIniciarFacturaCompraUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public IniciarFacturaCompraUseCase(
        ICompraRepository compras, IVentaRepository ventas, IReciboCobroRepository comprobantes, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _compras = compras;
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<NuevaFacturaCompraDisplay> ExecuteAsync(int idProveedor, int idSucursal, CancellationToken cancellationToken = default)
    {
        var idUsuario = CompraContexto.RequireIdUsuario(_currentUser);
        var planilla = await _compras.GetPlanillaAbiertaAsync(idUsuario, await _referencias.IdAsync(EstadosCobranza.PlanillaAbierta, cancellationToken), cancellationToken)
            ?? throw new BusinessException("No se pueden registrar facturas de compra: el usuario no tiene una planilla de caja abierta.");
        var proveedor = await _comprobantes.GetEntidadAsync(idProveedor, cancellationToken)
            ?? throw new NotFoundException($"Proveedor {idProveedor} no existe.");
        var sucursal = await _ventas.GetSucursalAsync(idSucursal, cancellationToken)
            ?? throw new NotFoundException($"Sucursal {idSucursal} no existe.");
        var fc = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "FC", cancellationToken);
        var letras = await _compras.GetLetrasAsync(fc, sucursal.IdCategoriaIva ?? 0, proveedor.IdCategoriaIva ?? 1, cancellationToken);
        return new NuevaFacturaCompraDisplay(planilla.IdPlanillaCaja, letras);
    }
}

public interface ICreateFacturaCompraUseCase
{
    Task<DocumentoProveedorDisplay> ExecuteAsync(CreateFacturaCompraDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alta de factura de compra en una transacción (Agregar_Ws de FrmFacturasCompras). Por línea, como el ERP:
/// directa → suma stock; de un remito de compra → solo consume su saldo (el stock lo sumó el remito);
/// de una orden de compra → consume su saldo y suma stock.
/// </summary>
public sealed class CreateFacturaCompraUseCase : ICreateFacturaCompraUseCase
{
    private const int IdEmpresa = 1;

    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _stock;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public CreateFacturaCompraUseCase(
        ICompraRepository compras,
        IVentaRepository stock,
        IReciboCobroRepository comprobantes,
        IReferenciasRepository referencias,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser)
    {
        _compras = compras;
        _stock = stock;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<DocumentoProveedorDisplay> ExecuteAsync(CreateFacturaCompraDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = CompraContexto.RequireIdUsuario(_currentUser);
        if (dto.Items.Count == 0)
            throw new BusinessException("Agregue al menos un ítem a la factura de compra.");

        var idProveedor = dto.IdProveedor!.Value;
        var idSucursal = dto.IdSucursal!.Value;
        var puntoVenta = dto.PuntoVenta.PadLeft(4, '0');
        var numero = dto.Numero.PadLeft(8, '0');
        var letra = dto.Letra.ToUpperInvariant();

        var factura = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _comprobantes.BloquearEntidadAsync(idProveedor, ct);

            var t = await CompraContexto.TiposAsync(_referencias, ct);
            var proveedor = await _comprobantes.GetEntidadAsync(idProveedor, ct)
                ?? throw new NotFoundException($"Proveedor {idProveedor} no existe.");
            var sucursal = await _stock.GetSucursalAsync(idSucursal, ct)
                ?? throw new NotFoundException($"Sucursal {idSucursal} no existe.");
            var idCategoriaIva = dto.IdCategoriaIva ?? proveedor.IdCategoriaIva ?? 1;

            var letras = await _compras.GetLetrasAsync(t.Fc, sucursal.IdCategoriaIva ?? 0, idCategoriaIva, ct);
            if (!letras.Contains(letra))
                throw new BusinessException($"La letra {letra} no corresponde a una compra de esta sucursal a un proveedor de esa categoría de IVA (válidas: {string.Join(", ", letras)}).");

            var anulado = await CompraContexto.EstadoAsync(_referencias, "ANULADO", ct);
            if (await _compras.ExisteDuplicadoAsync(idProveedor, t.Fc, puntoVenta, numero, anulado, ct))
                throw new ConflictException($"La factura {letra}-{puntoVenta}-{numero} de este proveedor ya fue registrada.");

            var planilla = await _compras.GetPlanillaAbiertaAsync(idUsuario, await _referencias.IdAsync(EstadosCobranza.PlanillaAbierta, ct), ct)
                ?? throw new BusinessException("No posee una planilla de caja abierta.");

            var origenes = await ValidarOrigenesAsync(dto, idProveedor, t, anulado, ct);

            var ahora = await _clock.GetNowAsync(ct);
            var fecha = dto.FechaEmision!.Value;
            var concepto = VentaRules.Concepto("FC", letra, puntoVenta, numero);

            var doc = new Db.DocumentosProveedor
            {
                IdComprobanteTipo = t.Fc,
                IdCondicion = VentaRules.Condicion,
                Letra = letra,
                IdPuntoVenta = int.Parse(puntoVenta),
                PuntoVenta = puntoVenta,
                Numero = numero,
                IdProveedor = idProveedor,
                RazonSocial = VentaRules.Truncar(dto.RazonSocial ?? proveedor.RazonSocial, 50),
                IdCategoriaIva = idCategoriaIva,
                NroDoc = VentaRules.Truncar(dto.Cuit ?? proveedor.Cuit, 50),
                IdProvincia = dto.IdProvincia ?? proveedor.IdProvincia,
                IdLocalidad = dto.IdLocalidad ?? proveedor.IdLocalidad,
                Calle = VentaRules.Truncar(dto.Calle ?? proveedor.Direccion, 50),
                Nro = "0",
                TotalNeto = dto.Neto,
                TotalIva = dto.Iva,
                TotalOtrosImpuestos = dto.Otros,
                TotalGeneral = dto.Total,
                FechaEmision = fecha,
                IdUsuario = idUsuario,
                IdEmpresa = IdEmpresa,
                IdPlanillaCaja = planilla.IdPlanillaCaja,
                Estado = await CompraContexto.EstadoAsync(_referencias, "GENERADO", ct),
                Cae = "0",
                VtoCae = fecha.Date,
                IdTransporte = dto.IdTransporte,
                Transporte = dto.Transporte ?? string.Empty,
                IdUnidad = dto.IdUnidad,
                Unidad = dto.Unidad ?? string.Empty,
                IdChofer = dto.IdChofer,
                Chofer = dto.Chofer ?? string.Empty,
                IdRemito = 0,
                IdSucursal = idSucursal,
                TotalDescuento = dto.TotalDescuento,
                PorcentajeDescuento = dto.PorcentajeDescuento,
                Remitar = false,
                Facturar = false,
                Pendiente = false,
            };
            _compras.Add(doc);
            await _compras.SaveChangesAsync(ct);

            if (!string.IsNullOrEmpty(dto.Observaciones))
                _compras.Add(new Db.DocumentosProveedorObservaciones { IdDocumentoProveedor = doc.IdDocumentoProveedor, Observaciones = dto.Observaciones });

            var estadoLibroActivo = await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "ACTIVO", ct);
            var serieDisponible = await _referencias.GetIdEstadoAsync("ITEMSNROSERIE", "DISPONIBLE", ct);
            foreach (var item in dto.Items)
                await GrabarItemAsync(item, doc, origenes, t, idUsuario, concepto, ahora, estadoLibroActivo, serieDisponible, ct);

            foreach (var tributo in dto.OtrosTributos)
                _compras.Add(new Db.DocumentosProveedorOtrosTributos
                {
                    IdDocumentoProveedor = doc.IdDocumentoProveedor,
                    IdComprobanteTipo = t.Fc,
                    IdOtroTributo = tributo.IdTributo,
                    Detalle = VentaRules.Truncar(tributo.Detalle ?? string.Empty, 50),
                    BaseImponible = tributo.BaseImponible,
                    Alicuota = tributo.Alicuota,
                    Total = tributo.Importe,
                });

            await ActualizarOrigenesAsync(doc.IdDocumentoProveedor, origenes.Values, t, ct);

            if (sucursal.IdCategoriaIva == await _referencias.GetIdCategoriaAsync("CATIVA", "RESP. INSCRIPTO", ct))
                await RegistrarLibroIvaAsync(doc, dto, t.Fc, ct);

            await RegistrarCtaCteAsync(doc, t.Fc, concepto, idUsuario, ct);

            await _compras.SaveChangesAsync(ct);
            return doc;
        }, cancellationToken);

        return factura.ToDisplay();
    }

    /// <summary>Remitos / órdenes de compra del proveedor, no anulados; cada línea relacionada sin superar su saldo. El ERP no validaba nada.</summary>
    private async Task<Dictionary<int, Db.DocumentosProveedor>> ValidarOrigenesAsync(
        CreateFacturaCompraDto dto, int idProveedor, CompraContexto.Tipos t, int anulado, CancellationToken ct)
    {
        var origenes = new Dictionary<int, Db.DocumentosProveedor>();
        foreach (var id in dto.Comprobantes.Select(c => c.IdDocumentoCliente!.Value).Distinct())
        {
            var origen = await _compras.GetDocumentoAsync(id, ct) ?? throw new NotFoundException($"Comprobante de proveedor {id} no existe.");
            if (origen.IdProveedor != idProveedor)
                throw new BusinessException($"El comprobante {id} no es del proveedor {idProveedor}.");
            if (origen.IdComprobanteTipo != t.Rc && origen.IdComprobanteTipo != t.Oc)
                throw new BusinessException($"El comprobante {id} no es un remito ni una orden de compra.");
            if (origen.Estado == anulado)
                throw new ConflictException($"El comprobante {id} está anulado.");
            origenes[id] = origen;
        }

        var lineasPorOrigen = new Dictionary<int, List<LineaPendienteCompraRow>>();
        foreach (var grupo in dto.Items.Where(i => i.Relacion is { IdDocumentoCliente: > 0 })
                     .GroupBy(i => (Doc: i.Relacion!.IdDocumentoCliente!.Value, Det: i.Relacion.IdDocumentoClienteDetalle!.Value)))
        {
            if (!origenes.ContainsKey(grupo.Key.Doc))
                throw new BusinessException($"La línea relacionada al comprobante {grupo.Key.Doc} requiere incluirlo en Comprobantes.");
            if (!lineasPorOrigen.TryGetValue(grupo.Key.Doc, out var lineas))
                lineasPorOrigen[grupo.Key.Doc] = lineas = await _compras.GetLineasPendientesAsync(grupo.Key.Doc, t.ConStockPendiente, ct);

            var linea = lineas.FirstOrDefault(l => l.Detalle.IdDocumentoProveedorDetalle == grupo.Key.Det)
                ?? throw new BusinessException($"La línea {grupo.Key.Det} del comprobante {grupo.Key.Doc} no tiene saldo pendiente.");
            if (grupo.Any(i => i.IdItem != linea.Detalle.IdItem))
                throw new BusinessException($"La línea {grupo.Key.Det} del comprobante {grupo.Key.Doc} es de otro ítem.");
            var cantidad = grupo.Sum(i => i.Cantidad);
            if (cantidad > linea.Saldo)
                throw new BusinessException($"Se facturan {cantidad} de {linea.Detalle.Descripcion} y el pendiente es {linea.Saldo}.");
        }

        var sinLineas = origenes.Keys.Except(lineasPorOrigen.Keys).ToList();
        if (sinLineas.Count > 0)
            throw new BusinessException($"La factura no incluye ninguna línea de los comprobantes {string.Join(", ", sinLineas)}.");
        return origenes;
    }

    private async Task GrabarItemAsync(
        FacturaCompraItemDto item, Db.DocumentosProveedor doc, IReadOnlyDictionary<int, Db.DocumentosProveedor> origenes, CompraContexto.Tipos t,
        int idUsuario, string concepto, DateTime ahora, int estadoLibroActivo, int serieDisponible, CancellationToken ct)
    {
        var idItem = item.IdItem!.Value;
        var descripcion = item.Descripcion.ToUpperInvariant();
        var cantidad = item.Cantidad;
        var idSucursal = doc.IdSucursal ?? 0;

        var detalle = new Db.DocumentosProveedorDetalle
        {
            IdDocumentoProveedor = doc.IdDocumentoProveedor,
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
            IdListaPrecio = 1,
            Metros = 0,
            EstadoLibroIva = estadoLibroActivo,
            Observaciones = string.Empty,
            Bonificacion = item.Bonificacion,
        };
        _compras.Add(detalle);
        await _compras.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(item.NroSerie))
            _compras.Add(new Db.ItemsNroSeries
            {
                IdItem = idItem,
                NroSerie = item.NroSerie.Trim(),
                IdComprobanteTipo = t.Fc,
                IdComprobante = doc.IdDocumentoProveedor,
                IdDocumentoClienteDetalle = (int)detalle.IdDocumentoProveedorDetalle,
                IdSucursal = idSucursal,
                Estado = serieDisponible,
            });

        var relacion = item.Relacion is { IdDocumentoCliente: > 0 } r ? r : null;
        var idDetalle = (int)detalle.IdDocumentoProveedorDetalle;
        if (relacion is null)
        {
            AgregarStockDetalle(doc, t.Fc, concepto, idItem, cantidad, cantidad, cantidad, idDetalle, 0, 0, 0);
            await SumarStockAsync(idItem, idSucursal, cantidad, doc.IdDocumentoProveedor, t.Fc, idDetalle, idUsuario, concepto, descripcion, ahora, ct);
            return;
        }

        var idRel = relacion.IdDocumentoCliente!.Value;
        var tipoRel = origenes[idRel].IdComprobanteTipo ?? 0;
        var detRel = relacion.IdDocumentoClienteDetalle!.Value;
        await _stock.AjustarSaldoStockAsync(idRel, detRel, tipoRel, idItem, -cantidad, ct);

        if (tipoRel != t.Oc)
        {
            // Remito de compra: el stock ya ingresó con el remito.
            AgregarStockDetalle(doc, t.Fc, concepto, idItem, cantidad, 0, -cantidad, idDetalle, idRel, tipoRel, detRel);
            return;
        }

        AgregarStockDetalle(doc, t.Fc, concepto, idItem, cantidad, cantidad, cantidad, idDetalle, idRel, tipoRel, detRel);
        await SumarStockAsync(idItem, idSucursal, cantidad, doc.IdDocumentoProveedor, t.Fc, idDetalle, idUsuario, concepto, descripcion, ahora, ct);
    }

    /// <summary>Estado de cada remito / orden facturada y relación; la factura queda para remitir si no viene de un remito.</summary>
    private async Task ActualizarOrigenesAsync(int idFactura, IEnumerable<Db.DocumentosProveedor> origenes, CompraContexto.Tipos t, CancellationToken ct)
    {
        var lista = origenes.ToList();
        if (lista.Count == 0)
        {
            await _compras.DeterminarRemitarFacturarAsync(idFactura, remitar: true, facturar: false, pendiente: true, ct);
            return;
        }

        await _compras.SaveChangesAsync(ct);
        foreach (var origen in lista)
        {
            var pendiente = (await _compras.GetLineasPendientesAsync(origen.IdDocumentoProveedor, t.ConStockPendiente, ct)).Count > 0;
            _compras.Add(new Db.DocumentosProveedorRemitos { IdDocumentoProveedor = origen.IdDocumentoProveedor, IdRemito = idFactura, IdComprobanteTipo = origen.IdComprobanteTipo });

            var estado = await CompraContexto.EstadoAsync(_referencias, pendiente ? "FACTURADO PARCIAL" : "FACTURADO", ct);
            await _compras.SetEstadoPendienteAsync(origen.IdDocumentoProveedor, estado, pendiente, ct);

            if (origen.IdComprobanteTipo == t.Oc)
                await _compras.DeterminarRemitarFacturarAsync(idFactura, remitar: true, facturar: false, pendiente: true, ct);
        }
    }

    /// <summary>Libro IVA compras + TxtComprasAlicuotas por alícuota, con la misma asignación de tributos que el ERP.</summary>
    private async Task RegistrarLibroIvaAsync(Db.DocumentosProveedor doc, CreateFacturaCompraDto dto, int fc, CancellationToken ct)
    {
        var iva = AfipRules.AgruparIva(dto.Items.Select(i => (i.IdImpuestoIva, i.PrecioNeto, i.Iva)), porcentajeDescuento: 0);
        decimal Tributos(params int[] ids) => dto.OtrosTributos.Where(x => ids.Contains(x.IdTributo ?? 0)).Sum(x => x.Importe);

        var tipoComp = await _referencias.GetParametroEnteroAsync("AFIP", $"FACTURA {doc.Letra}", ct);
        var fecha = doc.FechaEmision!.Value;
        _compras.Add(new Db.LibroIvaCompra
        {
            FechaEmision = DateOnly.FromDateTime(fecha),
            TipoComprobante = tipoComp.ToString(),
            Letra = doc.Letra,
            PuntoVenta = doc.PuntoVenta,
            Numero = doc.Numero,
            RazonSocial = doc.RazonSocial,
            NroDocumento = doc.NroDoc,
            TotalGeneral = dto.Total,
            TotalNeto = dto.Neto,
            TotalIva = dto.Iva,
            Neto21 = iva.Neto21,
            Neto10 = iva.Neto105,
            Neto27 = iva.Neto27,
            NetoExento = iva.NetoExento,
            Iva21 = iva.Iva21,
            Iva10 = iva.Iva105,
            Iva27 = iva.Iva27,
            IvaExcento = 0,
            IngBruto = Tributos(5),
            Percepciones = Tributos(7, 8, 9),
            ImpNacional = Tributos(1),
            ImpMunicipal = Tributos(3),
            ImpInterno = dto.Items.Sum(i => i.Otros) + Tributos(4),
            OtrosTributo = Tributos(18),
            Mes = fecha.Month,
            Anio = fecha.Year,
            IdComprobante = doc.IdDocumentoProveedor,
            IdComprobanteTipo = fc,
        });

        var cuit = doc.NroDoc ?? string.Empty;
        var (docTipoNombre, _) = AfipRules.Documento(cuit);
        var docTipo = await _referencias.GetParametroEnteroAsync("DOCTIPO", docTipoNombre == "SIN IDENTIFICAR" ? "CUIT" : docTipoNombre, ct);
        foreach (var a in iva.ParaAfip().Concat(iva.NetoExento > 0 ? [new AfipAlicuota(3, iva.NetoExento, 0)] : []))
            _compras.Add(new Db.TxtComprasAlicuotas
            {
                TipoComprobante = tipoComp.ToString().PadLeft(3, '0'),
                PuntoVenta = (doc.PuntoVenta ?? string.Empty).PadLeft(5, '0'),
                NroComprobante = (doc.Numero ?? string.Empty).PadLeft(20, '0'),
                CodVendedor = docTipo.ToString(),
                CuitVendedor = cuit.PadLeft(20, '0'),
                NetoGravado = a.BaseImponible,
                Alicuota = a.Id.ToString().PadLeft(4, '0'),
                ImporteLiquidado = a.Importe,
                Mes = fecha.Month,
                Anio = fecha.Year,
                FechaAlta = DateOnly.FromDateTime(fecha),
                IdComprobante = doc.IdDocumentoProveedor,
                IdComprobanteTipo = fc,
            });
    }

    /// <summary>Deuda con el proveedor: saldo = total, Total2 negativo (EntidadesCtaCte_Agregar + movimiento).</summary>
    private async Task RegistrarCtaCteAsync(Db.DocumentosProveedor doc, int fc, string concepto, int idUsuario, CancellationToken ct)
    {
        var fecha = doc.FechaEmision!.Value;
        var total = doc.TotalGeneral ?? 0m;
        var ctaCte = new Db.EntidadesCtaCte
        {
            IdEntidad = doc.IdProveedor,
            IdComprobanteTipo = fc,
            IdComprobante = doc.IdDocumentoProveedor,
            Concepto = concepto,
            NroCuota = 1,
            Total = total,
            Saldo = total,
            Cancelado = false,
            Fecha = fecha,
            FechaVencimiento = fecha.AddDays(30),
            FechaAnulacion = fecha,
            FechaPago = fecha,
            InteresAplicado = 0,
            Estado = await _referencias.IdAsync(EstadosCobranza.CtaCteGenerado, ct),
            Total2 = -total,
            IdEmpresa = IdEmpresa,
            IdSucursal = doc.IdSucursal,
            IdUsuario = idUsuario,
        };
        _compras.Add(ctaCte);
        await _compras.SaveChangesAsync(ct);

        _compras.Add(new Db.EntidadesCtaCteMovimientos
        {
            IdEntidadCtaCte = ctaCte.IdEntidadCtaCte,
            Concepto = concepto,
            AfavorEntidad = 0,
            EnContraEntidad = total,
            Fecha = fecha,
            IdElementoCobroPago = await _referencias.GetParametroEnteroAsync("ELEMENTO", "CTACTE", ct),
            IdElemento = 1,
            IdComprobanteTipo = fc,
            IdComprobante = doc.IdDocumentoProveedor,
        });
    }

    private void AgregarStockDetalle(
        Db.DocumentosProveedor doc, int tipo, string concepto, int idItem, decimal total, decimal saldo, decimal saldo2,
        int idDetalle, int idRelacion, int idRelacionTipo, int idRelacionDetalle) =>
        _compras.Add(new Db.EntidadesCtaCteStockMovimientosDetalle
        {
            IdEntidad = doc.IdProveedor,
            IdComprobante = doc.IdDocumentoProveedor,
            IdComprobanteTipo = tipo,
            Concepto = VentaRules.Truncar(concepto, 50),
            IdItem = idItem,
            Total = total,
            Saldo = saldo,
            Saldo2 = saldo2,
            Fecha = doc.FechaEmision,
            IdSucursal = doc.IdSucursal,
            IdComprobanteDetalle = idDetalle,
            IdComprobanteRelacion = idRelacion,
            IdComprobanteRelacionTipo = idRelacionTipo,
            IdComprobanteRelacionDetalle = idRelacionDetalle,
        });

    private async Task SumarStockAsync(
        int idItem, int idSucursal, decimal cantidad, int idComprobante, int tipo, int idDetalle, int idUsuario,
        string concepto, string descripcion, DateTime ahora, CancellationToken ct)
    {
        await _stock.SumarStockAsync(idItem, idSucursal, cantidad, ahora, ct);
        _compras.Add(new Db.ItemsMovimientosDetalles
        {
            IdItem = idItem,
            IdComprobante = idComprobante,
            IdComprobanteTipo = tipo,
            IdComprobanteDetalle = idDetalle,
            FechaAlta = ahora,
            IdUsuario = idUsuario,
            IdSucursal = idSucursal,
            Concepto = VentaRules.Truncar(concepto, 100),
            Item = VentaRules.Truncar(descripcion, 500),
            Total = cantidad,
            Debe = cantidad,
            Haber = 0,
            Total2 = cantidad,
            Automatico = true,
        });
    }
}

public interface IAnularFacturaCompraUseCase
{
    Task ExecuteAsync(int idFactura, CancellationToken cancellationToken = default);
}

/// <summary>
/// Anulación de factura de compra (Editar_Ws de FrmFacturasCompras): resta el stock que había sumado, devuelve el saldo a
/// remitos / órdenes de compra, borra libro IVA y otros tributos, anula la cta. cte. y el comprobante.
/// </summary>
public sealed class AnularFacturaCompraUseCase : IAnularFacturaCompraUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _stock;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public AnularFacturaCompraUseCase(
        ICompraRepository compras,
        IVentaRepository stock,
        IReciboCobroRepository comprobantes,
        IReferenciasRepository referencias,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser)
    {
        _compras = compras;
        _stock = stock;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task ExecuteAsync(int idFactura, CancellationToken cancellationToken = default)
    {
        var idUsuario = CompraContexto.RequireIdUsuario(_currentUser);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var t = await CompraContexto.TiposAsync(_referencias, ct);
            var doc = await _compras.GetDocumentoAsync(idFactura, ct) ?? throw new NotFoundException($"Comprobante de proveedor {idFactura} no existe.");
            if (doc.IdComprobanteTipo != t.Fc)
                throw new BusinessException($"El comprobante {idFactura} no es una factura de compra.");

            await _comprobantes.BloquearEntidadAsync(doc.IdProveedor ?? 0, ct);

            // El ERP permite anular en GENERADO y LIQUIDADO (69 / 102).
            var anulables = new[] { await CompraContexto.EstadoAsync(_referencias, "GENERADO", ct), await CompraContexto.EstadoAsync(_referencias, "LIQUIDADO", ct) };
            if (!anulables.Contains(doc.Estado ?? 0))
                throw new ConflictException($"La factura de compra {idFactura} no se puede anular en su estado actual.");

            var ctaCte = await _compras.GetCtaCteAsync(t.Fc, idFactura, ct);
            if (ctaCte is not null && ctaCte.Saldo != ctaCte.Total)
                throw new ConflictException($"La factura de compra {idFactura} tiene pagos imputados: anule primero las órdenes de pago.");

            var ahora = await _clock.GetNowAsync(ct);
            var detalles = (await _compras.GetDetallesAsync(idFactura, ct)).ToDictionary(d => d.IdDocumentoProveedorDetalle);
            var concepto = VentaRules.Concepto("FC", doc.Letra ?? string.Empty, doc.PuntoVenta ?? string.Empty, doc.Numero ?? string.Empty);

            foreach (var mov in await _stock.GetMovimientosStockAsync(idFactura, t.Fc, ct))
            {
                var cantidad = mov.Total ?? 0m;
                var idItem = mov.IdItem ?? 0;
                var tipoRel = mov.IdComprobanteRelacionTipo ?? 0;
                var directa = (mov.IdComprobanteRelacion ?? 0) == 0;

                if (!directa)
                    await _stock.AjustarSaldoStockAsync(mov.IdComprobanteRelacion ?? 0, mov.IdComprobanteRelacionDetalle ?? 0, tipoRel, idItem, cantidad, ct);

                // Resta el stock que la factura había sumado (líneas directas y de orden de compra).
                if (directa || tipoRel == t.Oc)
                {
                    await _stock.RestarStockAsync(idItem, mov.IdSucursal ?? doc.IdSucursal ?? 0, cantidad, ahora, ct);
                    _compras.Add(new Db.ItemsMovimientosDetalles
                    {
                        IdItem = idItem,
                        IdComprobante = idFactura,
                        IdComprobanteTipo = t.Fc,
                        IdComprobanteDetalle = mov.IdComprobanteDetalle ?? 0,
                        FechaAlta = ahora,
                        IdUsuario = idUsuario,
                        IdSucursal = mov.IdSucursal ?? doc.IdSucursal,
                        Concepto = VentaRules.Truncar(directa ? concepto : mov.Concepto, 100),
                        Item = VentaRules.Truncar(directa
                            ? (detalles.GetValueOrDefault(mov.IdComprobanteDetalle ?? 0)?.Descripcion ?? string.Empty).ToUpperInvariant()
                            : "DEVOLUCION POR ANULACION", 500),
                        Total = cantidad,
                        Debe = 0,
                        Haber = cantidad,
                        Total2 = -cantidad,
                        Automatico = true,
                    });
                }
            }
            await _compras.SaveChangesAsync(ct);

            foreach (var relacion in await _compras.GetRelacionesComoDestinoAsync(idFactura, ct))
            {
                var idOrigen = relacion.IdDocumentoProveedor ?? 0;
                var consumido = await _stock.GetSaldoStockAsync(idOrigen, relacion.IdComprobanteTipo ?? 0, ct);
                var estado = await CompraContexto.EstadoAsync(_referencias, consumido == 0 ? "GENERADO" : "FACTURADO PARCIAL", ct);
                await _compras.SetEstadoPendienteAsync(idOrigen, estado, pendiente: true, ct);
                await _compras.BorrarRelacionAsync(relacion.IdDocumentoProveedorRemito, ct);
            }

            await _stock.AnularMovimientosStockAsync(idFactura, t.Fc, ct);
            await _compras.BorrarLibroIvaAsync(idFactura, t.Fc, ct);
            await _compras.BorrarOtrosTributosAsync(idFactura, t.Fc, ct);
            if (ctaCte is not null)
                await _comprobantes.AnularCtaCteAsync(ctaCte.IdEntidadCtaCte, await _referencias.IdAsync(EstadosCobranza.CtaCteAnulado, ct), ahora, ct);
            await _stock.LiberarNrosSerieAsync(t.Fc, idFactura, await _referencias.GetIdEstadoAsync("ITEMSNROSERIE", "NO DISPONIBLE", ct), ct);
            await _compras.AnularDocumentoAsync(idFactura, await CompraContexto.EstadoAsync(_referencias, "ANULADO", ct), ahora, ct);
            return true;
        }, cancellationToken);
    }
}

public interface IGetComprobantesCompraParaFacturarUseCase
{
    Task<IReadOnlyList<ComprobanteCompraPendienteDisplay>> ExecuteAsync(int idProveedor, CancellationToken cancellationToken = default);
}

/// <summary>
/// Remitos de compra y órdenes de compra del proveedor con líneas pendientes (BuscarComprobantes_WS). Sin rol CEO / CTO, solo de la
/// sucursal del usuario, como el ERP.
/// </summary>
public sealed class GetComprobantesCompraParaFacturarUseCase : IGetComprobantesCompraParaFacturarUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public GetComprobantesCompraParaFacturarUseCase(ICompraRepository compras, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _compras = compras;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ComprobanteCompraPendienteDisplay>> ExecuteAsync(int idProveedor, CancellationToken cancellationToken = default)
    {
        CompraContexto.RequireIdUsuario(_currentUser);
        var t = await CompraContexto.TiposAsync(_referencias, cancellationToken);
        var excluidos = new[] { await CompraContexto.EstadoAsync(_referencias, "ANULADO", cancellationToken), await CompraContexto.EstadoAsync(_referencias, "CANCELADO", cancellationToken) };
        var sucursal = CompraContexto.EsGlobal(_currentUser) ? null : _currentUser.IdSucursal;

        return (await _compras.GetComprobantesConPendienteAsync(idProveedor, [t.Rc, t.Oc], excluidos, sucursal, cancellationToken))
            .Select(d => new ComprobanteCompraPendienteDisplay(
                d.IdDocumentoProveedor, d.IdComprobanteTipo ?? 0, $"{d.Letra}-{d.PuntoVenta}-{d.Numero}", d.RazonSocial, d.FechaEmision, d.Estado, d.TotalGeneral))
            .ToList();
    }
}

public interface IGetLineasPendientesCompraUseCase
{
    Task<IReadOnlyList<LineaPendienteCompraDisplay>> ExecuteAsync(int idDocumentoProveedor, CancellationToken cancellationToken = default);
}

/// <summary>Líneas de un remito / orden de compra con saldo pendiente (BuscarRemito_PorID_Remito_Seleccionar_Ws).</summary>
public sealed class GetLineasPendientesCompraUseCase : IGetLineasPendientesCompraUseCase
{
    private readonly ICompraRepository _compras;
    private readonly IReferenciasRepository _referencias;

    public GetLineasPendientesCompraUseCase(ICompraRepository compras, IReferenciasRepository referencias)
    {
        _compras = compras;
        _referencias = referencias;
    }

    public async Task<IReadOnlyList<LineaPendienteCompraDisplay>> ExecuteAsync(int idDocumentoProveedor, CancellationToken cancellationToken = default)
    {
        _ = await _compras.GetDocumentoAsync(idDocumentoProveedor, cancellationToken)
            ?? throw new NotFoundException($"Comprobante de proveedor {idDocumentoProveedor} no existe.");
        var t = await CompraContexto.TiposAsync(_referencias, cancellationToken);

        return (await _compras.GetLineasPendientesAsync(idDocumentoProveedor, t.ConStockPendiente, cancellationToken))
            .Select(l => new LineaPendienteCompraDisplay(
                l.Detalle.IdItem, l.Detalle.Descripcion, l.Saldo, l.Detalle.ListaPrecio, l.Detalle.PrecioUnitario, l.Detalle.Neto, l.Detalle.Ivaalic,
                l.Detalle.Iva, l.Detalle.Otros, l.Detalle.Total, l.Detalle.IdImpuestoIva, l.Detalle.Bonificacion,
                new LineaRelacionDisplay(idDocumentoProveedor, l.IdComprobanteTipo, (int)l.Detalle.IdDocumentoProveedorDetalle)))
            .ToList();
    }
}
