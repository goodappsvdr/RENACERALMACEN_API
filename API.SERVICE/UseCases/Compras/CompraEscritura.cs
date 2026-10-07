using API.SERVICE.Domain.Afip;
using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces.Afip;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Compras;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;
using API.SERVICE.Models.Compras;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Compras;

/// <summary>Validaciones previas resueltas para grabar un comprobante de proveedor.</summary>
internal sealed record ComprobanteCompraPreparado(
    Db.Entidades Proveedor, Db.Sucursales Sucursal, int IdCategoriaIva, string Letra, string PuntoVenta, string Numero, int IdPlanillaCaja, bool SucursalInscripta);

/// <summary>Escritura común a la factura (FC) y la nota de crédito (NCP) de proveedor; corre dentro de la transacción del caso de uso.</summary>
internal sealed class CompraEscritura
{
    private const int IdEmpresa = 1;

    private readonly ICompraRepository _compras;
    private readonly IVentaRepository _stock;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;

    public CompraEscritura(ICompraRepository compras, IVentaRepository stock, IReciboCobroRepository comprobantes, IReferenciasRepository referencias)
    {
        _compras = compras;
        _stock = stock;
        _comprobantes = comprobantes;
        _referencias = referencias;
    }

    /// <summary>Proveedor, sucursal, letra válida, número no registrado y planilla abierta del usuario.</summary>
    /// <param name="validarLetra">False para el remito de compra: siempre R y sin filas en ComprobantesLetras.</param>
    public async Task<ComprobanteCompraPreparado> PrepararAsync(
        ComprobanteProveedorDtoBase dto, string letraPedida, int tipo, string nombre, int idUsuario, CancellationToken ct, bool validarLetra = true)
    {
        var idProveedor = dto.IdProveedor!.Value;
        var proveedor = await _comprobantes.GetEntidadAsync(idProveedor, ct) ?? throw new NotFoundException($"Proveedor {idProveedor} no existe.");
        var sucursal = await _stock.GetSucursalAsync(dto.IdSucursal!.Value, ct) ?? throw new NotFoundException($"Sucursal {dto.IdSucursal} no existe.");
        var idCategoriaIva = dto.IdCategoriaIva ?? proveedor.IdCategoriaIva ?? 1;
        var letra = letraPedida.ToUpperInvariant();
        var puntoVenta = dto.PuntoVenta.PadLeft(4, '0');
        var numero = dto.Numero.PadLeft(8, '0');

        var letras = validarLetra ? await _compras.GetLetrasAsync(tipo, sucursal.IdCategoriaIva ?? 0, idCategoriaIva, ct) : [];
        if (validarLetra && !letras.Contains(letra))
            throw new BusinessException($"La letra {letra} no corresponde a esta sucursal y un proveedor de esa categoría de IVA (válidas: {string.Join(", ", letras)}).");

        var anulado = await CompraContexto.EstadoAsync(_referencias, "ANULADO", ct);
        if (await _compras.ExisteDuplicadoAsync(idProveedor, tipo, puntoVenta, numero, anulado, ct))
            throw new ConflictException($"La {nombre} {letra}-{puntoVenta}-{numero} de este proveedor ya fue registrada.");

        var planilla = await _compras.GetPlanillaAbiertaAsync(idUsuario, await _referencias.IdAsync(EstadosCobranza.PlanillaAbierta, ct), ct)
            ?? throw new BusinessException("No posee una planilla de caja abierta.");

        var inscripta = sucursal.IdCategoriaIva == await _referencias.GetIdCategoriaAsync("CATIVA", "RESP. INSCRIPTO", ct);
        return new ComprobanteCompraPreparado(proveedor, sucursal, idCategoriaIva, letra, puntoVenta, numero, planilla.IdPlanillaCaja, inscripta);
    }

    /// <summary>Cabecera (DocumentosProveedor_Agregar) y observación.</summary>
    public async Task<Db.DocumentosProveedor> GrabarCabeceraAsync(
        ComprobanteCompraDtoBase dto, ComprobanteCompraPreparado p, int tipo, int idUsuario, CancellationToken ct, string? cae = null, DateTime? vtoCae = null)
    {
        var fecha = dto.FechaEmision!.Value;
        var doc = new Db.DocumentosProveedor
        {
            IdComprobanteTipo = tipo,
            IdCondicion = VentaRules.Condicion,
            Letra = p.Letra,
            IdPuntoVenta = int.Parse(p.PuntoVenta),
            PuntoVenta = p.PuntoVenta,
            Numero = p.Numero,
            IdProveedor = dto.IdProveedor,
            RazonSocial = VentaRules.Truncar(dto.RazonSocial ?? p.Proveedor.RazonSocial, 50),
            IdCategoriaIva = p.IdCategoriaIva,
            NroDoc = VentaRules.Truncar(dto.Cuit ?? p.Proveedor.Cuit, 50),
            IdProvincia = dto.IdProvincia ?? p.Proveedor.IdProvincia,
            IdLocalidad = dto.IdLocalidad ?? p.Proveedor.IdLocalidad,
            Calle = VentaRules.Truncar(dto.Calle ?? p.Proveedor.Direccion, 50),
            Nro = "0",
            TotalNeto = dto.Neto,
            TotalIva = dto.Iva,
            TotalOtrosImpuestos = dto.Otros,
            TotalGeneral = dto.Total,
            FechaEmision = fecha,
            IdUsuario = idUsuario,
            IdEmpresa = IdEmpresa,
            IdPlanillaCaja = p.IdPlanillaCaja,
            Estado = await CompraContexto.EstadoAsync(_referencias, "GENERADO", ct),
            Cae = cae ?? "0",
            VtoCae = vtoCae ?? fecha.Date,
            IdTransporte = dto.IdTransporte,
            Transporte = dto.Transporte ?? string.Empty,
            IdUnidad = dto.IdUnidad,
            Unidad = dto.Unidad ?? string.Empty,
            IdChofer = dto.IdChofer,
            Chofer = dto.Chofer ?? string.Empty,
            IdRemito = 0,
            IdSucursal = dto.IdSucursal,
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
        return doc;
    }

    /// <summary>Línea del comprobante (DocumentosProveedorDetalle_Agregar) y, si trae número de serie, su alta en ItemsNroSeries.</summary>
    public async Task<Db.DocumentosProveedorDetalle> GrabarDetalleAsync(FacturaCompraItemDto item, Db.DocumentosProveedor doc, int tipo, int estadoLibroActivo, int estadoSerie, CancellationToken ct)
    {
        var detalle = new Db.DocumentosProveedorDetalle
        {
            IdDocumentoProveedor = doc.IdDocumentoProveedor,
            IdItem = item.IdItem,
            Descripcion = item.Descripcion.ToUpperInvariant(),
            Cantidad = item.Cantidad,
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
                IdItem = item.IdItem,
                NroSerie = item.NroSerie.Trim(),
                IdComprobanteTipo = tipo,
                IdComprobante = doc.IdDocumentoProveedor,
                IdDocumentoClienteDetalle = (int)detalle.IdDocumentoProveedorDetalle,
                IdSucursal = doc.IdSucursal,
                Estado = estadoSerie,
            });
        return detalle;
    }

    public void GrabarTributos(ComprobanteCompraDtoBase dto, Db.DocumentosProveedor doc, int tipo)
    {
        foreach (var tributo in dto.OtrosTributos)
            _compras.Add(new Db.DocumentosProveedorOtrosTributos
            {
                IdDocumentoProveedor = doc.IdDocumentoProveedor,
                IdComprobanteTipo = tipo,
                IdOtroTributo = tributo.IdTributo,
                Detalle = VentaRules.Truncar(tributo.Detalle ?? string.Empty, 50),
                BaseImponible = tributo.BaseImponible,
                Alicuota = tributo.Alicuota,
                Total = tributo.Importe,
            });
    }

    public void AgregarStockDetalle(
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

    /// <summary>Suma (ingresa) o resta (egresa) stock y registra el movimiento (Items_*Stock + GrabarMovimientoStock).</summary>
    public async Task MoverStockAsync(
        bool ingresa, int idItem, int idSucursal, decimal cantidad, int idComprobante, int tipo, int idDetalle, int idUsuario,
        string concepto, string descripcion, DateTime ahora, CancellationToken ct)
    {
        if (ingresa)
            await _stock.SumarStockAsync(idItem, idSucursal, cantidad, ahora, ct);
        else
            await _stock.RestarStockAsync(idItem, idSucursal, cantidad, ahora, ct);

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
            Debe = ingresa ? cantidad : 0,
            Haber = ingresa ? 0 : cantidad,
            Total2 = ingresa ? cantidad : -cantidad,
            Automatico = true,
        });
    }

    /// <summary>Libro IVA compras + TxtComprasAlicuotas por alícuota, con la misma asignación de tributos que el ERP.</summary>
    /// <param name="claseAfip">"FACTURA" o "NC": arma el parámetro AFIP del tipo de comprobante ("FACTURA A", "NC B"…).</param>
    public async Task RegistrarLibroIvaAsync(Db.DocumentosProveedor doc, ComprobanteCompraDtoBase dto, int tipo, string claseAfip, CancellationToken ct)
    {
        var iva = AfipRules.AgruparIva(dto.Items.Select(i => (i.IdImpuestoIva, i.PrecioNeto, i.Iva)), porcentajeDescuento: 0);
        decimal Tributos(params int[] ids) => dto.OtrosTributos.Where(x => ids.Contains(x.IdTributo ?? 0)).Sum(x => x.Importe);

        var tipoComp = await _referencias.GetParametroEnteroAsync("AFIP", $"{claseAfip} {doc.Letra}", ct);
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
            IdComprobanteTipo = tipo,
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
                IdComprobanteTipo = tipo,
            });
    }

    /// <summary>
    /// Cta. cte. del proveedor (EntidadesCtaCte_Agregar + movimiento). Factura: deuda (saldo = total, Total2 negativo, movimiento en contra).
    /// Nota de crédito: a favor (saldo negativo, Total2 positivo, movimiento a favor).
    /// </summary>
    public async Task<Db.EntidadesCtaCte> RegistrarCtaCteAsync(Db.DocumentosProveedor doc, int tipo, string concepto, int idUsuario, bool credito, CancellationToken ct)
    {
        var fecha = doc.FechaEmision!.Value;
        var total = doc.TotalGeneral ?? 0m;
        var ctaCte = new Db.EntidadesCtaCte
        {
            IdEntidad = doc.IdProveedor,
            IdComprobanteTipo = tipo,
            IdComprobante = doc.IdDocumentoProveedor,
            Concepto = concepto,
            NroCuota = 1,
            Total = total,
            Saldo = credito ? -total : total,
            Cancelado = false,
            Fecha = fecha,
            FechaVencimiento = fecha.AddDays(30),
            FechaAnulacion = fecha,
            FechaPago = fecha,
            InteresAplicado = 0,
            Estado = await _referencias.IdAsync(EstadosCobranza.CtaCteGenerado, ct),
            Total2 = credito ? total : -total,
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
            AfavorEntidad = credito ? total : 0,
            EnContraEntidad = credito ? 0 : total,
            Fecha = fecha,
            IdElementoCobroPago = await _referencias.GetParametroEnteroAsync("ELEMENTO", "CTACTE", ct),
            IdElemento = 1,
            IdComprobanteTipo = tipo,
            IdComprobante = doc.IdDocumentoProveedor,
        });
        return ctaCte;
    }
}
