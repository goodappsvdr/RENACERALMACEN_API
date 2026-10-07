using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Items;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Items;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Mappings.Items;
using API.SERVICE.Models.Items;
using Microsoft.Extensions.Logging;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Items;

public interface IUpdateItemUseCase
{
    Task<ItemDisplay> ExecuteAsync(int idItem, UpdateItemDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Modificación de ítem en una transacción. Replica Items_Modificar_Ws de FrmItemsABM:
/// historial de precio (ItemsPreciosActualizacion) si cambió el precio, datos del ítem,
/// impuesto, datos por sucursal y, si el parámetro CAMBIASTOCK vale 1, el stock.
/// </summary>
public sealed class UpdateItemUseCase : IUpdateItemUseCase
{
    private readonly IItemRepository _repository;
    private readonly IParametroRepository _parametros;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<UpdateItemUseCase> _logger;

    public UpdateItemUseCase(
        IItemRepository repository,
        IParametroRepository parametros,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser,
        ILogger<UpdateItemUseCase> logger)
    {
        _repository = repository;
        _parametros = parametros;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<ItemDisplay> ExecuteAsync(int idItem, UpdateItemDto dto, CancellationToken cancellationToken = default)
    {
        // El historial de precios registra quién hizo el cambio.
        var idUsuario = _currentUser.IdUsuario
            ?? throw new ForbiddenException("El usuario no tiene un registro en Usuarios del ERP; no puede modificar ítems.");

        ItemRules.ValidarSucursales(dto.Sucursales.Select(s => s.IdSucursal!.Value).ToList());
        var idImpuesto = dto.IdImpuesto!.Value;
        var alicuota = ItemRules.AlicuotaDe(idImpuesto);
        var codigoBarras = ItemRules.NormalizarCodigoBarras(dto.CodigoBarras);

        var item = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var actual = await _repository.GetByIdAsync(idItem, ct)
                ?? throw new NotFoundException($"Item {idItem} no existe.");

            var sucursales = await _repository.GetSucursalesAsync(idItem, ct);
            if (sucursales.Count == 0)
                throw new BusinessException($"El ítem {idItem} no está dado de alta en ninguna sucursal.");

            var ahora = await _clock.GetNowAsync(ct);
            var cambiaStock = await CambiaStockAsync(ct);

            // Historial de precio: siempre la primera vez; después, solo si el precio cambió.
            // Como en el ERP, el precio anterior es el de la primera fila de ItemsSucursales del ítem.
            var precioAnterior = sucursales[0].PrecioVenta;
            var ultimaActualizacion = await _repository.GetUltimaActualizacionPrecioAsync(idItem, ct);
            if (ultimaActualizacion is null || ItemRules.PrecioCambio(precioAnterior, dto.PrecioUnitario))
            {
                _repository.AddActualizacionPrecio(new Db.ItemsPreciosActualizacion
                {
                    IdItem = idItem,
                    PrecioActual = precioAnterior,
                    PrecioNuevo = dto.PrecioUnitario,
                    Rentabilidad = Math.Round(dto.Rentabilidad, 0, MidpointRounding.AwayFromZero), // columna numeric(18,0)
                    FechaActualizacion = ahora,
                    IdUsario = idUsuario,
                });
            }

            actual.Descripcion = dto.Descripcion.ToUpperInvariant();
            actual.IdItemTipo = dto.IdItemTipo;
            actual.IdEmpresa = ItemRules.IdEmpresa;
            actual.IdRubro = dto.IdRubro;
            actual.IdSubRubro = dto.IdSubRubro;
            actual.IdMarca = dto.IdMarca;
            actual.IdModelo = dto.IdModelo;
            actual.IdUnidadMedida = dto.IdUnidadMedida;
            actual.CodFabrica = dto.CodigoFabrica;
            actual.TieneDetalle = true;
            actual.MueveStock = dto.MueveStock;
            actual.StockMaximo = dto.StockMaximo;
            actual.StockMinimo = dto.StockMinimo;
            actual.IdUbicacion = dto.IdUbicacion;
            actual.UnidadesXbulto = 1;
            actual.Barcode = codigoBarras;
            actual.Neto = dto.Costo; // el ERP guarda el costo en Items.Neto
            actual.CuentaDebe = 0;
            actual.CuentaHaber = 0;
            actual.MtsKgs = 0;
            actual.Estado = dto.IdEstado;
            actual.EsDolar = false;
            actual.Rentabilidad = dto.Rentabilidad;
            actual.MostrarWeb = dto.MostrarWeb;
            actual.Detalle = dto.Detalle;
            if (cambiaStock)
                actual.StockActual = dto.StockActual;

            await _repository.ReplaceImpuestoAsync(idItem, idImpuesto, ct);

            foreach (var pedido in dto.Sucursales)
            {
                var filas = sucursales.Where(s => s.IdSucursal == pedido.IdSucursal).ToList();
                if (filas.Count == 0)
                {
                    // El ERP hace un UPDATE por ID_Item + ID_Sucursal: si la fila no existe, no pasa nada.
                    _logger.LogWarning("Item {IdItem}: la sucursal {IdSucursal} no tiene fila en ItemsSucursales; se ignora", idItem, pedido.IdSucursal);
                    continue;
                }

                foreach (var fila in filas)
                {
                    fila.CodFabrica = dto.CodigoFabrica;
                    fila.IdItemTipo = dto.IdItemTipo;
                    fila.StockMinimo = dto.StockMinimo;
                    fila.StockMaximo = dto.StockMaximo;
                    fila.MueveStock = dto.MueveStock;
                    fila.Costo = dto.Costo;
                    fila.Neto = dto.Neto;
                    fila.Rentabilidad = dto.Rentabilidad;
                    fila.IdImpuesto = idImpuesto;
                    fila.Alicuota = alicuota;
                    fila.PrecioVenta = dto.PrecioUnitario;
                    fila.Barcode = codigoBarras;
                    fila.IdUbicacion = dto.IdUbicacion;
                    fila.Estado = dto.IdEstado;
                    if (cambiaStock)
                        fila.Stock = pedido.Stock;
                }
            }

            await _repository.SaveChangesAsync(ct);
            return actual;
        }, cancellationToken);

        return item.ToDisplay();
    }

    private async Task<bool> CambiaStockAsync(CancellationToken cancellationToken)
    {
        var valor = await _parametros.GetValorAsync(ItemRules.CambiaStockCategoria, ItemRules.CambiaStockNombre, cancellationToken);
        return decimal.TryParse(valor?.Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var numero)
               && numero == 1;
    }
}
