using Microsoft.EntityFrameworkCore;
using Db = global::API.DA.Entities;

namespace API.SERVICE.Repositories.Items;

public sealed partial class ItemRepository
{
    public async Task AddAsync(Db.Items item, CancellationToken cancellationToken = default)
    {
        // No hay FK/navegación entre Items y sus tablas hijas: se guarda primero para obtener el ID_Item.
        Set.Add(item);
        await Context.SaveChangesAsync(cancellationToken);
    }

    public Task<List<Db.ItemsSucursales>> GetSucursalesAsync(int idItem, CancellationToken cancellationToken = default) =>
        Context.ItemsSucursales
            .Where(s => s.IdItem == idItem)
            .OrderBy(s => s.IdItemSucursal)
            .ToListAsync(cancellationToken);

    public void AddSucursales(IEnumerable<Db.ItemsSucursales> sucursales) =>
        Context.ItemsSucursales.AddRange(sucursales);

    public async Task ReplaceImpuestoAsync(int idItem, int idImpuesto, CancellationToken cancellationToken = default)
    {
        var actuales = await Context.ItemsImpuestos.Where(i => i.IdItem == idItem).ToListAsync(cancellationToken);
        Context.ItemsImpuestos.RemoveRange(actuales);
        Context.ItemsImpuestos.Add(new Db.ItemsImpuestos { IdItem = idItem, IdImpuesto = idImpuesto });
    }

    public Task<Db.ItemsPreciosActualizacion?> GetUltimaActualizacionPrecioAsync(int idItem, CancellationToken cancellationToken = default) =>
        Context.ItemsPreciosActualizacion
            .AsNoTracking()
            .Where(a => a.IdItem == idItem)
            .OrderByDescending(a => a.IdItemPrecioActulizacion)
            .FirstOrDefaultAsync(cancellationToken);

    public void AddActualizacionPrecio(Db.ItemsPreciosActualizacion actualizacion) =>
        Context.ItemsPreciosActualizacion.Add(actualizacion);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Context.SaveChangesAsync(cancellationToken);
}
