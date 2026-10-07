using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Stock;
using API.SERVICE.Models.Stock;
using API.SERVICE.UseCases.Ventas;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Stock;

/// <summary>
/// Reglas de la transferencia entre sucursales. Estados de DOCUMENTOSCLIENTE: GENERADO = en tránsito (ya salió del origen),
/// CONFIRMADO = recibido en destino, RECHAZADO = el destino no la aceptó (vuelve al origen), ANULADO = el origen la canceló.
/// Origen en ID_Sucursal, destino en ID_Cliente (donde lo guardaba el ERP; DocumentosCliente no tiene otra columna) e ID_Usuario
/// es el usuario real (el ERP guardaba ahí la sucursal de origen).
/// </summary>
internal static class MovimientoStockContexto
{
    public const string Letra = "M";
    public const string RolAdministrador = "ADMINISTRADOR";
    public const int IdEmpresa = 1;

    public static int RequireIdUsuario(ICurrentUser user) =>
        user.IdUsuario ?? throw new ForbiddenException("El usuario no tiene un registro en Usuarios del ERP; no puede mover stock.");

    public static Task<int> TipoAsync(IReferenciasRepository referencias, CancellationToken ct) =>
        referencias.GetParametroEnteroAsync("COMPROBANTE", "MS", ct);

    public static Task<int> EstadoAsync(IReferenciasRepository referencias, string nombre, CancellationToken ct) =>
        referencias.GetIdEstadoAsync("DOCUMENTOSCLIENTE", nombre, ct);

    /// <summary>La sucursal es la del usuario, una asignada en UsuariosSucursales, o el usuario es administrador / CEO.</summary>
    public static async Task RequireOperaSucursalAsync(IMovimientoStockRepository movimientos, ICurrentUser user, int idUsuario, int idSucursal, string accion, CancellationToken ct)
    {
        if (user.IdSucursal == idSucursal || user.IsInRole(RolAdministrador) || user.IsInRole("CEO")
            || await movimientos.OperaSucursalAsync(idUsuario, idSucursal, ct))
            return;
        throw new ForbiddenException($"El usuario no opera la sucursal {idSucursal}: no puede {accion}.");
    }

    public static async Task<Db.DocumentosCliente> GetMovimientoAsync(IMovimientoStockRepository movimientos, int tipo, int id, CancellationToken ct) =>
        await movimientos.GetMovimientoAsync(id, tipo, ct) ?? throw new NotFoundException($"Movimiento de stock {id} no existe.");

    public static string Concepto(Db.DocumentosCliente doc) => $"{doc.Letra}-{doc.PuntoVenta}-{doc.Numero}";

    public static Db.ItemsMovimientosDetalles MovimientoItem(
        Db.DocumentosClienteDetalle linea, Db.DocumentosCliente doc, int idSucursal, bool entrada, int idUsuario, DateTime ahora)
    {
        var cantidad = linea.Cantidad ?? 0;
        return new Db.ItemsMovimientosDetalles
        {
            IdItem = linea.IdItem,
            IdComprobante = doc.IdDocumentoCliente,
            IdComprobanteTipo = doc.IdComprobanteTipo,
            IdComprobanteDetalle = (int)linea.IdDocumentoClienteDetalle,
            FechaAlta = ahora,
            IdUsuario = idUsuario,
            IdSucursal = idSucursal,
            Concepto = VentaRules.Truncar(Concepto(doc), 100),
            Item = VentaRules.Truncar(linea.Descripcion ?? string.Empty, 500),
            Total = cantidad,
            Debe = entrada ? cantidad : 0,
            Haber = entrada ? 0 : cantidad,
            Total2 = entrada ? cantidad : -cantidad,
            Automatico = true,
        };
    }

    /// <summary>Mueve todas las líneas a <paramref name="idSucursal"/> (entrada) o desde ella (salida) y deja el movimiento de stock del ítem.</summary>
    public static async Task MoverAsync(
        IMovimientoStockRepository movimientos, Db.DocumentosCliente doc, IEnumerable<Db.DocumentosClienteDetalle> lineas, int idSucursal, bool entrada,
        int idUsuario, DateTime ahora, CancellationToken ct)
    {
        foreach (var linea in lineas)
        {
            var cantidad = linea.Cantidad ?? 0;
            await movimientos.MoverStockSucursalAsync(linea.IdItem ?? 0, idSucursal, entrada ? cantidad : -cantidad, ahora, ct);
            movimientos.Add(MovimientoItem(linea, doc, idSucursal, entrada, idUsuario, ahora));
        }
        await movimientos.SaveChangesAsync(ct);
    }

    public static MovimientoStockDisplay ToDisplay(Db.DocumentosCliente doc, IEnumerable<Db.DocumentosClienteDetalle> lineas) => new(
        doc.IdDocumentoCliente,
        doc.Letra ?? Letra,
        doc.PuntoVenta ?? string.Empty,
        doc.Numero ?? string.Empty,
        doc.FechaEmision,
        doc.IdSucursal ?? 0,
        doc.IdCliente ?? 0,
        doc.RazonSocial ?? string.Empty,
        doc.IdUsuario ?? 0,
        doc.Estado ?? 0,
        doc.Observaciones,
        [.. lineas.Select(l => new MovimientoStockItemDisplay(l.IdItem ?? 0, l.Descripcion ?? string.Empty, l.Cantidad ?? 0))]);
}

public interface IIniciarMovimientoStockUseCase
{
    Task<NuevoMovimientoStockDisplay> ExecuteAsync(int? idSucursalOrigen, CancellationToken cancellationToken = default);
}

/// <summary>Punto de venta de la sucursal de origen y número sugerido (IniciarPuntoVenta_WS de FrmMovimientoStockABM).</summary>
public sealed class IniciarMovimientoStockUseCase : IIniciarMovimientoStockUseCase
{
    private readonly IMovimientoStockRepository _movimientos;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public IniciarMovimientoStockUseCase(IMovimientoStockRepository movimientos, IReciboCobroRepository comprobantes, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _movimientos = movimientos;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<NuevoMovimientoStockDisplay> ExecuteAsync(int? idSucursalOrigen, CancellationToken cancellationToken = default)
    {
        var idUsuario = MovimientoStockContexto.RequireIdUsuario(_currentUser);
        var origen = await SucursalOrigenAsync(_movimientos, _currentUser, idUsuario, idSucursalOrigen, cancellationToken);
        var tipo = await MovimientoStockContexto.TipoAsync(_referencias, cancellationToken);
        var manual = await VentaContexto.NumeracionManualAsync(_referencias, cancellationToken, "MS");
        string? sugerido = null;
        if (!manual)
            sugerido = (await _comprobantes.GetProximoNumeroAsync(origen.PuntoVenta!, MovimientoStockContexto.Letra, tipo, cancellationToken))?.ToString().PadLeft(8, '0');
        return new NuevoMovimientoStockDisplay(origen.IdSucursal, origen.PuntoVenta!, MovimientoStockContexto.Letra, manual, sugerido);
    }

    internal static async Task<Db.Sucursales> SucursalOrigenAsync(
        IMovimientoStockRepository movimientos, ICurrentUser user, int idUsuario, int? idSucursalOrigen, CancellationToken ct)
    {
        var id = idSucursalOrigen ?? user.IdSucursal ?? throw new BusinessException("El usuario no tiene sucursal: informar la sucursal de origen.");
        await MovimientoStockContexto.RequireOperaSucursalAsync(movimientos, user, idUsuario, id, "enviar mercadería desde ella", ct);
        var sucursal = await movimientos.GetSucursalAsync(id, ct) ?? throw new NotFoundException($"Sucursal {id} no existe.");
        if (string.IsNullOrWhiteSpace(sucursal.PuntoVenta))
            throw new BusinessException($"La sucursal {id} no tiene punto de venta.");
        return sucursal;
    }
}

public interface ICreateMovimientoStockUseCase
{
    Task<MovimientoStockDisplay> ExecuteAsync(CreateMovimientoStockDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Envío (Agregar_Ws de FrmMovimientoStockABM): comprobante MS en tránsito, la mercadería sale del stock de la sucursal de origen
/// (validando que alcance) y queda pendiente de recepción en el destino.
/// </summary>
public sealed class CreateMovimientoStockUseCase : ICreateMovimientoStockUseCase
{
    private readonly IMovimientoStockRepository _movimientos;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public CreateMovimientoStockUseCase(
        IMovimientoStockRepository movimientos, IReciboCobroRepository comprobantes, IReferenciasRepository referencias,
        IUnitOfWork unitOfWork, IServerClock clock, ICurrentUser currentUser)
    {
        _movimientos = movimientos;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<MovimientoStockDisplay> ExecuteAsync(CreateMovimientoStockDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = MovimientoStockContexto.RequireIdUsuario(_currentUser);
        var cantidades = dto.Items
            .GroupBy(i => i.IdItem!.Value)
            .Select(g => (IdItem: g.Key, Cantidad: g.Sum(i => i.Cantidad)))
            .ToList();
        if (cantidades.Count == 0)
            throw new BusinessException("Agregue al menos un ítem a transferir.");

        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var origen = await IniciarMovimientoStockUseCase.SucursalOrigenAsync(_movimientos, _currentUser, idUsuario, dto.IdSucursalOrigen, ct);
            var idDestino = dto.IdSucursalDestino!.Value;
            if (idDestino == origen.IdSucursal)
                throw new BusinessException("La sucursal de destino tiene que ser distinta de la de origen.");
            var destino = await _movimientos.GetSucursalAsync(idDestino, ct) ?? throw new NotFoundException($"Sucursal {idDestino} no existe.");

            await _movimientos.BloquearSucursalAsync(origen.IdSucursal, ct);
            var ids = cantidades.Select(c => c.IdItem).ToList();
            var enOrigen = (await _movimientos.GetItemsAsync(ids, origen.IdSucursal, ct)).ToDictionary(i => i.IdItem);
            var enDestino = (await _movimientos.GetItemsAsync(ids, idDestino, ct)).ToDictionary(i => i.IdItem);
            foreach (var (idItem, cantidad) in cantidades)
            {
                var item = enOrigen.GetValueOrDefault(idItem) ?? throw new NotFoundException($"Ítem {idItem} no existe.");
                if (!item.MueveStock)
                    throw new BusinessException($"El ítem {item.Descripcion} no mueve stock.");
                if (item.Stock is null || enDestino[idItem].Stock is null)
                    throw new BusinessException($"El ítem {item.Descripcion} no está habilitado en las dos sucursales.");
                if (item.Stock < cantidad)
                    throw new BusinessException($"Stock insuficiente de {item.Descripcion} en {origen.Descripcion?.Trim()}: hay {item.Stock:0.##}, se quieren enviar {cantidad:0.##}.");
            }

            var tipo = await MovimientoStockContexto.TipoAsync(_referencias, ct);
            var (puntoVenta, numero) = await ReservarNumeroAsync(origen.PuntoVenta!, tipo, dto, ct);
            var ahora = await _clock.GetNowAsync(ct);
            var fecha = dto.FechaEmision!.Value;

            var doc = new Db.DocumentosCliente
            {
                IdComprobanteTipo = tipo,
                IdCondicion = VentaRules.Condicion,
                Letra = MovimientoStockContexto.Letra,
                IdPuntoVenta = int.TryParse(puntoVenta, out var idPv) ? idPv : 0,
                PuntoVenta = puntoVenta,
                Numero = numero,
                IdCliente = destino.IdSucursal,
                RazonSocial = VentaRules.Truncar(destino.Descripcion?.Trim() ?? string.Empty, 50),
                IdCategoriaIva = 0,
                NroDoc = string.Empty,
                IdProvincia = destino.IdProvincia ?? 0,
                IdLocalidad = destino.IdLocalidad ?? 0,
                Calle = VentaRules.Truncar(destino.Direccion ?? string.Empty, 50),
                Nro = "0",
                TotalNeto = 0,
                TotalIva = 0,
                TotalOtrosImpuestos = 0,
                TotalGeneral = 0,
                FechaEmision = fecha,
                IdUsuario = idUsuario,
                IdEmpresa = MovimientoStockContexto.IdEmpresa,
                IdPlanillaCaja = 0,
                Estado = await MovimientoStockContexto.EstadoAsync(_referencias, "GENERADO", ct),
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
                Porcentaje = 0,
                TotalRecargo = 0,
                TotalDescuento = 0,
                IdSucursal = origen.IdSucursal,
                Remitar = false,
                Facturar = false,
                Pendiente = false,
            };
            _movimientos.Add(doc);
            await _movimientos.SaveChangesAsync(ct);

            var estadoLibro = await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "ACTIVO", ct);
            var lineas = new List<Db.DocumentosClienteDetalle>();
            foreach (var (idItem, cantidad) in cantidades)
            {
                var linea = new Db.DocumentosClienteDetalle
                {
                    IdDocumentoCliente = doc.IdDocumentoCliente,
                    IdItem = idItem,
                    Descripcion = VentaRules.Truncar(enOrigen[idItem].Descripcion.ToUpperInvariant(), 500),
                    Cantidad = cantidad,
                    ListaPrecio = string.Empty,
                    PrecioUnitario = 0,
                    Neto = 0,
                    Ivaalic = 0,
                    Iva = 0,
                    Otros = 0,
                    Total = 0,
                    IdImpuestoIva = 0,
                    IdListaPrecio = 0,
                    Metros = 0,
                    EstadoLibroIva = estadoLibro,
                    Observaciones = string.Empty,
                    Porcentaje = 0,
                    Descuento = 0,
                };
                _movimientos.Add(linea);
                lineas.Add(linea);
            }
            await _movimientos.SaveChangesAsync(ct);

            await MovimientoStockContexto.MoverAsync(_movimientos, doc, lineas, origen.IdSucursal, entrada: false, idUsuario, ahora, ct);
            return MovimientoStockContexto.ToDisplay(doc, lineas);
        }, cancellationToken);
    }

    private async Task<(string PuntoVenta, string Numero)> ReservarNumeroAsync(string puntoVentaOrigen, int tipo, CreateMovimientoStockDto dto, CancellationToken ct)
    {
        if (await VentaContexto.NumeracionManualAsync(_referencias, ct, "MS"))
        {
            if (string.IsNullOrWhiteSpace(dto.PuntoVenta) || string.IsNullOrWhiteSpace(dto.Numero))
                throw new BusinessException("La numeración de movimientos de stock es manual (NUMERACION/MS = 1): informar punto de venta y número.");
            await _comprobantes.ReservarNumeroAsync(dto.PuntoVenta, MovimientoStockContexto.Letra, tipo, ct);
            return (dto.PuntoVenta.PadLeft(4, '0'), dto.Numero.PadLeft(8, '0'));
        }

        var numero = await _comprobantes.ReservarNumeroAsync(puntoVentaOrigen, MovimientoStockContexto.Letra, tipo, ct)
            ?? throw new BusinessException($"No existe el punto de venta {puntoVentaOrigen} para movimientos de stock (letra {MovimientoStockContexto.Letra}).");
        return (puntoVentaOrigen, numero.ToString().PadLeft(8, '0'));
    }
}

public interface IResolverMovimientoStockUseCase
{
    /// <summary>El destino recibe la mercadería: suma a su stock y el movimiento queda CONFIRMADO.</summary>
    Task<MovimientoStockDisplay> RecibirAsync(int idMovimiento, CancellationToken cancellationToken = default);

    /// <summary>El destino no la acepta: vuelve al stock del origen y el movimiento queda RECHAZADO.</summary>
    Task<MovimientoStockDisplay> RechazarAsync(int idMovimiento, CancellationToken cancellationToken = default);

    /// <summary>El origen cancela el envío antes de que lo reciban: vuelve a su stock y el movimiento queda ANULADO.</summary>
    Task<MovimientoStockDisplay> AnularAsync(int idMovimiento, CancellationToken cancellationToken = default);
}

/// <summary>
/// Recepción / rechazo (FrmMovimientoStockRecibirABM.CambiarEstado) y anulación (Editar_Ws). Solo sobre movimientos en tránsito
/// (GENERADO): la actualización de estado es condicional, así que un movimiento no se recibe y anula a la vez (409).
/// </summary>
public sealed class ResolverMovimientoStockUseCase : IResolverMovimientoStockUseCase
{
    private readonly IMovimientoStockRepository _movimientos;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public ResolverMovimientoStockUseCase(
        IMovimientoStockRepository movimientos, IReferenciasRepository referencias, IUnitOfWork unitOfWork, IServerClock clock, ICurrentUser currentUser)
    {
        _movimientos = movimientos;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public Task<MovimientoStockDisplay> RecibirAsync(int idMovimiento, CancellationToken cancellationToken = default) =>
        ResolverAsync(idMovimiento, "CONFIRMADO", porDestino: true, cancellationToken);

    public Task<MovimientoStockDisplay> RechazarAsync(int idMovimiento, CancellationToken cancellationToken = default) =>
        ResolverAsync(idMovimiento, "RECHAZADO", porDestino: true, cancellationToken);

    public Task<MovimientoStockDisplay> AnularAsync(int idMovimiento, CancellationToken cancellationToken = default) =>
        ResolverAsync(idMovimiento, "ANULADO", porDestino: false, cancellationToken);

    private async Task<MovimientoStockDisplay> ResolverAsync(int idMovimiento, string estadoFinal, bool porDestino, CancellationToken cancellationToken)
    {
        var idUsuario = MovimientoStockContexto.RequireIdUsuario(_currentUser);
        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var tipo = await MovimientoStockContexto.TipoAsync(_referencias, ct);
            var doc = await MovimientoStockContexto.GetMovimientoAsync(_movimientos, tipo, idMovimiento, ct);
            var origen = doc.IdSucursal ?? 0;
            var destino = doc.IdCliente ?? 0;
            await MovimientoStockContexto.RequireOperaSucursalAsync(_movimientos, _currentUser, idUsuario, porDestino ? destino : origen,
                porDestino ? "recibir o rechazar mercadería en ella" : "anular envíos desde ella", ct);

            var ahora = await _clock.GetNowAsync(ct);
            var anulado = estadoFinal == "ANULADO";
            var estado = await MovimientoStockContexto.EstadoAsync(_referencias, estadoFinal, ct);
            if (!await _movimientos.CambiarEstadoAsync(idMovimiento, estado, await MovimientoStockContexto.EstadoAsync(_referencias, "GENERADO", ct),
                    anulado ? ahora : null, ct))
                throw new ConflictException($"El movimiento de stock {idMovimiento} ya no está en tránsito.");

            var lineas = await _movimientos.GetDetallesAsync(idMovimiento, ct);
            // Recibido: entra al destino. Rechazado o anulado: vuelve al origen.
            await MovimientoStockContexto.MoverAsync(_movimientos, doc, lineas, estadoFinal == "CONFIRMADO" ? destino : origen, entrada: true, idUsuario, ahora, ct);
            if (anulado)
                await _movimientos.AnularDetallesAsync(idMovimiento, await _referencias.GetIdEstadoAsync("LIBROIVAVENTAS", "BAJA", ct), ct);

            doc.Estado = estado;
            return MovimientoStockContexto.ToDisplay(doc, lineas);
        }, cancellationToken);
    }
}

public interface IGetMovimientosStockEnTransitoUseCase
{
    Task<IReadOnlyList<MovimientoStockDisplay>> ExecuteAsync(int? idSucursalDestino, CancellationToken cancellationToken = default);
}

/// <summary>Lo que falta recibir en una sucursal (grilla de FrmMovimientoStockRecibirABM).</summary>
public sealed class GetMovimientosStockEnTransitoUseCase : IGetMovimientosStockEnTransitoUseCase
{
    private readonly IMovimientoStockRepository _movimientos;
    private readonly IReferenciasRepository _referencias;
    private readonly ICurrentUser _currentUser;

    public GetMovimientosStockEnTransitoUseCase(IMovimientoStockRepository movimientos, IReferenciasRepository referencias, ICurrentUser currentUser)
    {
        _movimientos = movimientos;
        _referencias = referencias;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MovimientoStockDisplay>> ExecuteAsync(int? idSucursalDestino, CancellationToken cancellationToken = default)
    {
        var idUsuario = MovimientoStockContexto.RequireIdUsuario(_currentUser);
        var destino = idSucursalDestino ?? _currentUser.IdSucursal ?? throw new BusinessException("El usuario no tiene sucursal: informar la sucursal.");
        await MovimientoStockContexto.RequireOperaSucursalAsync(_movimientos, _currentUser, idUsuario, destino, "ver lo que le envían", cancellationToken);

        var tipo = await MovimientoStockContexto.TipoAsync(_referencias, cancellationToken);
        var docs = await _movimientos.GetEnTransitoAsync(destino, tipo, await MovimientoStockContexto.EstadoAsync(_referencias, "GENERADO", cancellationToken), cancellationToken);
        var resultado = new List<MovimientoStockDisplay>(docs.Count);
        foreach (var doc in docs)
            resultado.Add(MovimientoStockContexto.ToDisplay(doc, await _movimientos.GetDetallesAsync(doc.IdDocumentoCliente, cancellationToken)));
        return resultado;
    }
}

public interface IGetMovimientoStockUseCase
{
    Task<MovimientoStockDisplay> ExecuteAsync(int idMovimiento, CancellationToken cancellationToken = default);
}

public sealed class GetMovimientoStockUseCase : IGetMovimientoStockUseCase
{
    private readonly IMovimientoStockRepository _movimientos;
    private readonly IReferenciasRepository _referencias;

    public GetMovimientoStockUseCase(IMovimientoStockRepository movimientos, IReferenciasRepository referencias)
    {
        _movimientos = movimientos;
        _referencias = referencias;
    }

    public async Task<MovimientoStockDisplay> ExecuteAsync(int idMovimiento, CancellationToken cancellationToken = default)
    {
        var doc = await MovimientoStockContexto.GetMovimientoAsync(_movimientos, await MovimientoStockContexto.TipoAsync(_referencias, cancellationToken), idMovimiento, cancellationToken);
        return MovimientoStockContexto.ToDisplay(doc, await _movimientos.GetDetallesAsync(idMovimiento, cancellationToken));
    }
}
