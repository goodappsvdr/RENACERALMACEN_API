using API.SERVICE.Domain.Items;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Items;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Mappings.Items;
using API.SERVICE.Models.Items;
using Db = global::API.DA.Entities;

namespace API.SERVICE.UseCases.Items;

public interface ICreateItemUseCase
{
    Task<ItemDisplay> ExecuteAsync(CreateItemDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alta de ítem en una transacción: Items + ItemsImpuestos + una fila de ItemsSucursales por sucursal.
/// Replica Items_Agregar_Ws de FrmItemsABM (incluidos los valores fijos que graba el ERP).
/// </summary>
public sealed class CreateItemUseCase : ICreateItemUseCase
{
    private readonly IItemRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;

    public CreateItemUseCase(IItemRepository repository, IUnitOfWork unitOfWork, IServerClock clock)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<ItemDisplay> ExecuteAsync(CreateItemDto dto, CancellationToken cancellationToken = default)
    {
        ItemRules.ValidarSucursales(dto.Sucursales.Select(s => s.IdSucursal!.Value).ToList());
        var idImpuesto = dto.IdImpuesto!.Value;
        var alicuota = ItemRules.AlicuotaDe(idImpuesto);
        var codigoBarras = ItemRules.NormalizarCodigoBarras(dto.CodigoBarras);

        var item = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var ahora = await _clock.GetNowAsync(ct);

            var nuevo = new Db.Items
            {
                Descripcion = dto.Descripcion.ToUpperInvariant(),
                IdItemTipo = dto.IdItemTipo,
                IdEmpresa = ItemRules.IdEmpresa,
                IdRubro = dto.IdRubro,
                IdSubRubro = dto.IdSubRubro,
                IdMarca = dto.IdMarca,
                IdModelo = dto.IdModelo,
                FechaCompra = ahora,
                FechaVenta = ahora,
                IdUnidadMedida = dto.IdUnidadMedida,
                CodFabrica = dto.CodigoFabrica,
                TieneDetalle = true,
                MueveStock = dto.MueveStock,
                StockMaximo = dto.StockMaximo,
                StockMinimo = dto.StockMinimo,
                StockActual = dto.StockActual,
                IdUbicacion = dto.IdUbicacion,
                UnidadesXbulto = 1,
                Barcode = codigoBarras,
                Neto = dto.Costo, // el ERP guarda el costo en Items.Neto (Items no tiene columna Costo)
                CuentaDebe = 1,
                CuentaHaber = 1,
                MtsKgs = 1,
                Estado = dto.IdEstado,
                EsDolar = false,
                Rentabilidad = dto.Rentabilidad,
                StockInicial = dto.StockActual,
                MostrarWeb = dto.MostrarWeb,
                Detalle = dto.Detalle,
            };
            await _repository.AddAsync(nuevo, ct);

            await _repository.ReplaceImpuestoAsync(nuevo.IdItem, idImpuesto, ct);

            var fecha = DateOnly.FromDateTime(ahora);
            _repository.AddSucursales(dto.Sucursales.Select(s => new Db.ItemsSucursales
            {
                IdItem = nuevo.IdItem,
                CodFabrica = dto.CodigoFabrica,
                IdSucursal = s.IdSucursal,
                IdItemTipo = dto.IdItemTipo,
                Stock = s.Stock,
                StockMinimo = dto.StockMinimo,
                StockMaximo = dto.StockMaximo,
                MueveStock = dto.MueveStock,
                Costo = dto.Costo,
                Neto = dto.Neto,
                Rentabilidad = dto.Rentabilidad,
                IdImpuesto = idImpuesto,
                Alicuota = alicuota,
                PrecioVenta = dto.PrecioUnitario,
                Barcode = codigoBarras,
                FechaCompra = fecha,
                FechaVenta = fecha,
                Estado = s.Estado,
                EstaOferta = false,
                PrecioOferta = 0,
                IdUbicacion = dto.IdUbicacion,
                IdEmpresa = ItemRules.IdEmpresa,
                StockInicial = s.Stock,
            }));

            await _repository.SaveChangesAsync(ct);
            return nuevo;
        }, cancellationToken);

        return item.ToDisplay();
    }
}
