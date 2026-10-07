using Db = global::API.DA.Entities;

namespace API.SERVICE.Interfaces.Items;

/// <summary>
/// Operaciones del ABM de ítems (Items + ItemsSucursales + ItemsImpuestos + ItemsPreciosActualizacion).
/// Pensadas para usarse dentro de <see cref="IUnitOfWork"/>: nada se confirma hasta el commit.
/// </summary>
public partial interface IItemRepository
{
    /// <summary>Inserta el ítem; al volver, la entidad ya tiene el ID_Item asignado.</summary>
    Task AddAsync(Db.Items item, CancellationToken cancellationToken = default);

    /// <summary>Filas de ItemsSucursales del ítem, trackeadas, en orden de alta.</summary>
    Task<List<Db.ItemsSucursales>> GetSucursalesAsync(int idItem, CancellationToken cancellationToken = default);

    void AddSucursales(IEnumerable<Db.ItemsSucursales> sucursales);

    /// <summary>Deja un único impuesto para el ítem (borra los anteriores).</summary>
    Task ReplaceImpuestoAsync(int idItem, int idImpuesto, CancellationToken cancellationToken = default);

    Task<Db.ItemsPreciosActualizacion?> GetUltimaActualizacionPrecioAsync(int idItem, CancellationToken cancellationToken = default);

    void AddActualizacionPrecio(Db.ItemsPreciosActualizacion actualizacion);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
