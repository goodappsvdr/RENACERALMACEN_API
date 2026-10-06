using API.DA.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.DA.DbContexts;

/// <summary>
/// Ajustes al modelo scaffoldeado. Este archivo NO se regenera con `dotnet ef dbcontext scaffold`,
/// así que todo lo que corrija al modelo generado va acá.
/// </summary>
public partial class ElRenacerDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Tablas legacy sin PK física pero con columna IDENTITY: se declara la clave
        // en EF para poder leerlas por Id. No cambia el esquema de la base.
        modelBuilder.Entity<ConciliacionBancaria>().HasKey(e => e.IdConciliacionBancaria);
        modelBuilder.Entity<ConciliacionBancariaDetalle>().HasKey(e => e.IdConciliacionBancariaDetalle);
        modelBuilder.Entity<DatosEmpresa>().HasKey(e => e.IdDatosEmpresa);
        modelBuilder.Entity<EntidadOrdenPagoDocumentosProveedores>().HasKey(e => e.IdEntidadOrdenPagoDocumentoProveedor);
        modelBuilder.Entity<MarcasActualizacion>().HasKey(e => e.IdMarcaActualizacion);
    }
}
