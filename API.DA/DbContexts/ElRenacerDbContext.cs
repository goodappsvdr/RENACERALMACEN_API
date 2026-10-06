using System;
using System.Collections.Generic;
using API.DA.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.DA.DbContexts;

public partial class ElRenacerDbContext : DbContext
{
    public ElRenacerDbContext(DbContextOptions<ElRenacerDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AreasDeContacto> AreasDeContacto { get; set; }

    public virtual DbSet<AspnetMembership> AspnetMembership { get; set; }

    public virtual DbSet<AspnetRoles> AspnetRoles { get; set; }

    public virtual DbSet<AspnetUsers> AspnetUsers { get; set; }

    public virtual DbSet<Bancos> Bancos { get; set; }

    public virtual DbSet<BancosChequeras> BancosChequeras { get; set; }

    public virtual DbSet<BancosCheques> BancosCheques { get; set; }

    public virtual DbSet<BancosCuentas> BancosCuentas { get; set; }

    public virtual DbSet<BancosCuentasMovimientos> BancosCuentasMovimientos { get; set; }

    public virtual DbSet<BancosSucursales> BancosSucursales { get; set; }

    public virtual DbSet<CajaPlanillas> CajaPlanillas { get; set; }

    public virtual DbSet<CajasPlanillasAjustes> CajasPlanillasAjustes { get; set; }

    public virtual DbSet<CajasPlanillasDetalle> CajasPlanillasDetalle { get; set; }

    public virtual DbSet<Categorias> Categorias { get; set; }

    public virtual DbSet<Choferes> Choferes { get; set; }

    public virtual DbSet<Combos> Combos { get; set; }

    public virtual DbSet<ComprobantesCarga> ComprobantesCarga { get; set; }

    public virtual DbSet<ComprobantesLetras> ComprobantesLetras { get; set; }

    public virtual DbSet<ComprobantesTipo> ComprobantesTipo { get; set; }

    public virtual DbSet<ConciliacionBancaria> ConciliacionBancaria { get; set; }

    public virtual DbSet<ConciliacionBancariaDetalle> ConciliacionBancariaDetalle { get; set; }

    public virtual DbSet<Consultas> Consultas { get; set; }

    public virtual DbSet<ConsultasDetalle> ConsultasDetalle { get; set; }

    public virtual DbSet<DatosEmpresa> DatosEmpresa { get; set; }

    public virtual DbSet<DocumentosCliente> DocumentosCliente { get; set; }

    public virtual DbSet<DocumentosClienteDetalle> DocumentosClienteDetalle { get; set; }

    public virtual DbSet<DocumentosClienteDetalleEnvases> DocumentosClienteDetalleEnvases { get; set; }

    public virtual DbSet<DocumentosClienteFotosApp> DocumentosClienteFotosApp { get; set; }

    public virtual DbSet<DocumentosClienteObservaciones> DocumentosClienteObservaciones { get; set; }

    public virtual DbSet<DocumentosClienteQr> DocumentosClienteQr { get; set; }

    public virtual DbSet<DocumentosClienteRelacion> DocumentosClienteRelacion { get; set; }

    public virtual DbSet<DocumentosClienteRemitos> DocumentosClienteRemitos { get; set; }

    public virtual DbSet<DocumentosClienteRemitosDetalle> DocumentosClienteRemitosDetalle { get; set; }

    public virtual DbSet<DocumentosClienteVencimientos> DocumentosClienteVencimientos { get; set; }

    public virtual DbSet<DocumentosClientesOtrosTributos> DocumentosClientesOtrosTributos { get; set; }

    public virtual DbSet<DocumentosProveedor> DocumentosProveedor { get; set; }

    public virtual DbSet<DocumentosProveedorDetalle> DocumentosProveedorDetalle { get; set; }

    public virtual DbSet<DocumentosProveedorObservaciones> DocumentosProveedorObservaciones { get; set; }

    public virtual DbSet<DocumentosProveedorOtrosTributos> DocumentosProveedorOtrosTributos { get; set; }

    public virtual DbSet<DocumentosProveedorRemitos> DocumentosProveedorRemitos { get; set; }

    public virtual DbSet<DocumentosProveedorRemitosDetalle> DocumentosProveedorRemitosDetalle { get; set; }

    public virtual DbSet<ElementosCobroPago> ElementosCobroPago { get; set; }

    public virtual DbSet<Empresas> Empresas { get; set; }

    public virtual DbSet<EntidadOrdenPagoDocumentosProveedores> EntidadOrdenPagoDocumentosProveedores { get; set; }

    public virtual DbSet<EntidadOrdenesDePagoDocumentosProveedor> EntidadOrdenesDePagoDocumentosProveedor { get; set; }

    public virtual DbSet<EntidadRecibosDocumentosCliente> EntidadRecibosDocumentosCliente { get; set; }

    public virtual DbSet<Entidades> Entidades { get; set; }

    public virtual DbSet<EntidadesAreasDeContacto> EntidadesAreasDeContacto { get; set; }

    public virtual DbSet<EntidadesCategorias> EntidadesCategorias { get; set; }

    public virtual DbSet<EntidadesCheques> EntidadesCheques { get; set; }

    public virtual DbSet<EntidadesContacto> EntidadesContacto { get; set; }

    public virtual DbSet<EntidadesCtaCte> EntidadesCtaCte { get; set; }

    public virtual DbSet<EntidadesCtaCteMovimientos> EntidadesCtaCteMovimientos { get; set; }

    public virtual DbSet<EntidadesCtaCteReporte> EntidadesCtaCteReporte { get; set; }

    public virtual DbSet<EntidadesCtaCteStockMovimientosDetalle> EntidadesCtaCteStockMovimientosDetalle { get; set; }

    public virtual DbSet<EntidadesDirecciones> EntidadesDirecciones { get; set; }

    public virtual DbSet<EntidadesEntidadesTipo> EntidadesEntidadesTipo { get; set; }

    public virtual DbSet<EntidadesFacturacion> EntidadesFacturacion { get; set; }

    public virtual DbSet<EntidadesRecibos> EntidadesRecibos { get; set; }

    public virtual DbSet<EntidadesRecibosDetalle> EntidadesRecibosDetalle { get; set; }

    public virtual DbSet<EntidadesTipo> EntidadesTipo { get; set; }

    public virtual DbSet<Estados> Estados { get; set; }

    public virtual DbSet<Impuestos> Impuestos { get; set; }

    public virtual DbSet<Items> Items { get; set; }

    public virtual DbSet<ItemsEnvanses> ItemsEnvanses { get; set; }

    public virtual DbSet<ItemsImagenes> ItemsImagenes { get; set; }

    public virtual DbSet<ItemsImpuestos> ItemsImpuestos { get; set; }

    public virtual DbSet<ItemsMovimientosDetalles> ItemsMovimientosDetalles { get; set; }

    public virtual DbSet<ItemsNroSeries> ItemsNroSeries { get; set; }

    public virtual DbSet<ItemsOfertas> ItemsOfertas { get; set; }

    public virtual DbSet<ItemsPreciosActualizacion> ItemsPreciosActualizacion { get; set; }

    public virtual DbSet<ItemsSucursales> ItemsSucursales { get; set; }

    public virtual DbSet<LibroIvaCompra> LibroIvaCompra { get; set; }

    public virtual DbSet<LibroIvaVenta> LibroIvaVenta { get; set; }

    public virtual DbSet<ListasPrecio> ListasPrecio { get; set; }

    public virtual DbSet<Localidades> Localidades { get; set; }

    public virtual DbSet<Marcas> Marcas { get; set; }

    public virtual DbSet<MarcasActualizacion> MarcasActualizacion { get; set; }

    public virtual DbSet<Modelos> Modelos { get; set; }

    public virtual DbSet<ModelosActualizacion> ModelosActualizacion { get; set; }

    public virtual DbSet<Motivos> Motivos { get; set; }

    public virtual DbSet<Ofertas> Ofertas { get; set; }

    public virtual DbSet<OfertasAgotamiento> OfertasAgotamiento { get; set; }

    public virtual DbSet<OfertasSucursales> OfertasSucursales { get; set; }

    public virtual DbSet<OrdenesDepositos> OrdenesDepositos { get; set; }

    public virtual DbSet<OrdenesDepositosDetalle> OrdenesDepositosDetalle { get; set; }

    public virtual DbSet<OrdenesExrtacciones> OrdenesExrtacciones { get; set; }

    public virtual DbSet<OrdenesExtraccionesDetalle> OrdenesExtraccionesDetalle { get; set; }

    public virtual DbSet<OtrosTributos> OtrosTributos { get; set; }

    public virtual DbSet<Paises> Paises { get; set; }

    public virtual DbSet<ParametroJson> ParametroJson { get; set; }

    public virtual DbSet<Parametros> Parametros { get; set; }

    public virtual DbSet<ProductosCombos> ProductosCombos { get; set; }

    public virtual DbSet<ProveedoresCheques> ProveedoresCheques { get; set; }

    public virtual DbSet<ProveedoresRecibos> ProveedoresRecibos { get; set; }

    public virtual DbSet<ProveedoresRecibosDetalle> ProveedoresRecibosDetalle { get; set; }

    public virtual DbSet<Provincias> Provincias { get; set; }

    public virtual DbSet<PuntosVenta> PuntosVenta { get; set; }

    public virtual DbSet<Retenciones> Retenciones { get; set; }

    public virtual DbSet<Rubros> Rubros { get; set; }

    public virtual DbSet<RubrosActualizacion> RubrosActualizacion { get; set; }

    public virtual DbSet<SubRubros> SubRubros { get; set; }

    public virtual DbSet<SubRubrosActualizacion> SubRubrosActualizacion { get; set; }

    public virtual DbSet<Sucursales> Sucursales { get; set; }

    public virtual DbSet<Tarjetas> Tarjetas { get; set; }

    public virtual DbSet<Transportes> Transportes { get; set; }

    public virtual DbSet<TxtComprasAlicuotas> TxtComprasAlicuotas { get; set; }

    public virtual DbSet<TxtVentasAlicuotas> TxtVentasAlicuotas { get; set; }

    public virtual DbSet<Ubicaciones> Ubicaciones { get; set; }

    public virtual DbSet<Unidades> Unidades { get; set; }

    public virtual DbSet<Usuarios> Usuarios { get; set; }

    public virtual DbSet<UsuariosSucursales> UsuariosSucursales { get; set; }

    public virtual DbSet<Vehiculos> Vehiculos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AreasDeContacto>(entity =>
        {
            entity.HasKey(e => e.IdAreaDeContacto).HasName("PK_EntidadesContactos");

            entity.Property(e => e.IdAreaDeContacto).HasColumnName("ID_AreaDeContacto");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IdEstado).HasColumnName("ID_Estado");
            entity.Property(e => e.Observaciones)
                .HasMaxLength(500)
                .IsUnicode(false);
        });

        modelBuilder.Entity<AspnetMembership>(entity =>
        {
            entity.HasKey(e => e.UserId)
                .HasName("PK__aspnet_M__1788CC4DC0EFF4D1")
                .IsClustered(false);

            entity.ToTable("aspnet_Membership");

            entity.HasIndex(e => new { e.ApplicationId, e.LoweredEmail }, "aspnet_Membership_index").IsClustered();

            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.Property(e => e.Comment).HasColumnType("ntext");
            entity.Property(e => e.CreateDate).HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FailedPasswordAnswerAttemptWindowStart).HasColumnType("datetime");
            entity.Property(e => e.FailedPasswordAttemptWindowStart).HasColumnType("datetime");
            entity.Property(e => e.LastLockoutDate).HasColumnType("datetime");
            entity.Property(e => e.LastLoginDate).HasColumnType("datetime");
            entity.Property(e => e.LastPasswordChangedDate).HasColumnType("datetime");
            entity.Property(e => e.LoweredEmail).HasMaxLength(256);
            entity.Property(e => e.MobilePin)
                .HasMaxLength(16)
                .HasColumnName("MobilePIN");
            entity.Property(e => e.Password).HasMaxLength(128);
            entity.Property(e => e.PasswordAnswer).HasMaxLength(128);
            entity.Property(e => e.PasswordQuestion).HasMaxLength(256);
            entity.Property(e => e.PasswordSalt).HasMaxLength(128);

            entity.HasOne(d => d.User).WithOne(p => p.AspnetMembership)
                .HasForeignKey<AspnetMembership>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__aspnet_Me__UserI__43D61337");
        });

        modelBuilder.Entity<AspnetRoles>(entity =>
        {
            entity.HasKey(e => e.RoleId)
                .HasName("PK__aspnet_R__8AFACE1BAD43E24D")
                .IsClustered(false);

            entity.ToTable("aspnet_Roles");

            entity.HasIndex(e => new { e.ApplicationId, e.LoweredRoleName }, "aspnet_Roles_index1")
                .IsUnique()
                .IsClustered();

            entity.Property(e => e.RoleId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Description).HasMaxLength(256);
            entity.Property(e => e.LoweredRoleName).HasMaxLength(256);
            entity.Property(e => e.RoleName).HasMaxLength(256);
        });

        modelBuilder.Entity<AspnetUsers>(entity =>
        {
            entity.HasKey(e => e.UserId)
                .HasName("PK__aspnet_U__1788CC4D421D9C62")
                .IsClustered(false);

            entity.ToTable("aspnet_Users");

            entity.HasIndex(e => new { e.ApplicationId, e.LoweredUserName }, "aspnet_Users_Index")
                .IsUnique()
                .IsClustered();

            entity.HasIndex(e => new { e.ApplicationId, e.LastActivityDate }, "aspnet_Users_Index2");

            entity.Property(e => e.UserId).HasDefaultValueSql("(newid())");
            entity.Property(e => e.LastActivityDate).HasColumnType("datetime");
            entity.Property(e => e.LoweredUserName).HasMaxLength(256);
            entity.Property(e => e.MobileAlias)
                .HasMaxLength(16)
                .HasDefaultValueSql("(NULL)");
            entity.Property(e => e.UserName).HasMaxLength(256);

            entity.HasMany(d => d.Role).WithMany(p => p.User)
                .UsingEntity<Dictionary<string, object>>(
                    "AspnetUsersInRoles",
                    r => r.HasOne<AspnetRoles>().WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__aspnet_Us__RoleI__662B2B3B"),
                    l => l.HasOne<AspnetUsers>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__aspnet_Us__UserI__65370702"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId").HasName("PK__aspnet_U__AF2760ADB3EBB99B");
                        j.ToTable("aspnet_UsersInRoles");
                        j.HasIndex(new[] { "RoleId" }, "aspnet_UsersInRoles_index");
                    });
        });

        modelBuilder.Entity<Bancos>(entity =>
        {
            entity.HasKey(e => e.IdBanco);

            entity.Property(e => e.IdBanco).HasColumnName("ID_Banco");
            entity.Property(e => e.IdEstado).HasColumnName("ID_Estado");
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(500)
                .IsUnicode(false);
        });

        modelBuilder.Entity<BancosChequeras>(entity =>
        {
            entity.HasKey(e => e.IdBancosChequeras);

            entity.Property(e => e.IdBancosChequeras).HasColumnName("ID_BancosChequeras");
            entity.Property(e => e.Cantidad).HasColumnType("money");
            entity.Property(e => e.IdBancoCuenta).HasColumnName("ID_BancoCuenta");
        });

        modelBuilder.Entity<BancosCheques>(entity =>
        {
            entity.HasKey(e => e.IdBancoCheque);

            entity.Property(e => e.IdBancoCheque).HasColumnName("ID_BancoCheque");
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.FechaImpresion).HasColumnType("datetime");
            entity.Property(e => e.FechaVencimiento).HasColumnType("datetime");
            entity.Property(e => e.IdBancoChequera).HasColumnName("ID_BancoChequera");
            entity.Property(e => e.IdBancoCuenta).HasColumnName("ID_BancoCuenta");
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.Importe).HasColumnType("money");
            entity.Property(e => e.NroCheque)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<BancosCuentas>(entity =>
        {
            entity.HasKey(e => e.IdBancoCuenta);

            entity.Property(e => e.IdBancoCuenta).HasColumnName("ID_BancoCuenta");
            entity.Property(e => e.IdBanco).HasColumnName("ID_Banco");
            entity.Property(e => e.IdBancoSucursal).HasColumnName("ID_BancoSucursal");
            entity.Property(e => e.IdCuentaTipo).HasColumnName("ID_CuentaTipo");
            entity.Property(e => e.NroCuenta)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<BancosCuentasMovimientos>(entity =>
        {
            entity.HasKey(e => e.IdBancoCuentaMovimiento);

            entity.Property(e => e.IdBancoCuentaMovimiento).HasColumnName("ID_BancoCuentaMovimiento");
            entity.Property(e => e.Debe).HasColumnType("money");
            entity.Property(e => e.Fecha).HasColumnType("datetime");
            entity.Property(e => e.Haber).HasColumnType("money");
            entity.Property(e => e.IdBancoCuenta).HasColumnName("ID_BancoCuenta");
            entity.Property(e => e.IdBancoDestino).HasColumnName("ID_BancoDestino");
            entity.Property(e => e.IdBancoOrigen).HasColumnName("ID_BancoOrigen");
            entity.Property(e => e.IdBancoSucursalDestino).HasColumnName("ID_BancoSucursalDestino");
            entity.Property(e => e.IdBancoSucursalOrigen).HasColumnName("ID_BancoSucursalOrigen");
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdMovimientoTipo).HasColumnName("ID_MovimientoTipo");
            entity.Property(e => e.Importe).HasColumnType("money");
            entity.Property(e => e.NroCuentaDestino)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NroCuentaOrigen)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<BancosSucursales>(entity =>
        {
            entity.HasKey(e => e.IdBancoSucursal);

            entity.Property(e => e.IdBancoSucursal).HasColumnName("ID_BancoSucursal");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IdBanco).HasColumnName("ID_Banco");
            entity.Property(e => e.IdEstado).HasColumnName("ID_Estado");
        });

        modelBuilder.Entity<CajaPlanillas>(entity =>
        {
            entity.HasKey(e => e.IdPlanillaCaja);

            entity.Property(e => e.IdPlanillaCaja).HasColumnName("ID_PlanillaCaja");
            entity.Property(e => e.Diferencia).HasColumnType("money");
            entity.Property(e => e.FechaApertura).HasColumnType("datetime");
            entity.Property(e => e.FechaCierre).HasColumnType("datetime");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SaldoInicial).HasColumnType("money");
            entity.Property(e => e.TotalEgresos).HasColumnType("money");
            entity.Property(e => e.TotalIngresos).HasColumnType("money");
            entity.Property(e => e.TotalRendido).HasColumnType("money");
        });

        modelBuilder.Entity<CajasPlanillasAjustes>(entity =>
        {
            entity.HasKey(e => e.IdCajaPlanillaAjuste);

            entity.Property(e => e.IdCajaPlanillaAjuste).HasColumnName("ID_CajaPlanillaAjuste");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.IdEstado).HasColumnName("ID_Estado");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.IdPlanillaCaja).HasColumnName("ID_PlanillaCaja");
            entity.Property(e => e.IdPlanillaCajaDetalle).HasColumnName("ID_PlanillaCajaDetalle");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.IdTipo).HasColumnName("ID_Tipo");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.Numero)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<CajasPlanillasDetalle>(entity =>
        {
            entity.HasKey(e => e.IdCajaPlanillaDetalle);

            entity.Property(e => e.IdCajaPlanillaDetalle).HasColumnName("ID_CajaPlanillaDetalle");
            entity.Property(e => e.Debe).HasColumnType("money");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Fecha).HasColumnType("datetime");
            entity.Property(e => e.Haber).HasColumnType("money");
            entity.Property(e => e.IdCajaPlanilla).HasColumnName("ID_CajaPlanilla");
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdElementoCobro).HasColumnName("ID_ElementoCobro");
            entity.Property(e => e.Obsevaciones)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Reducida)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
            entity.Property(e => e.Total2).HasColumnType("money");
        });

        modelBuilder.Entity<Categorias>(entity =>
        {
            entity.HasKey(e => e.IdCategoria);

            entity.Property(e => e.IdCategoria).HasColumnName("ID_Categoria");
            entity.Property(e => e.CategoriaTipo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Nombre)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Choferes>(entity =>
        {
            entity.HasKey(e => e.IdChofer);

            entity.Property(e => e.IdChofer).HasColumnName("ID_Chofer");
            entity.Property(e => e.Calle)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdCategoriaIva).HasColumnName("ID_CategoriaIVA");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdLocalidad).HasColumnName("ID_Localidad");
            entity.Property(e => e.IdProvincia).HasColumnName("ID_Provincia");
            entity.Property(e => e.IdTransporte).HasColumnName("ID_Transporte");
            entity.Property(e => e.Mail)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Nro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NroDoc)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Telefono)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Combos>(entity =>
        {
            entity.HasKey(e => e.IdCombo).HasName("PK__Combos__6E3C0EA292DA4B95");

            entity.Property(e => e.IdCombo)
                .ValueGeneratedNever()
                .HasColumnName("ID_Combo");
            entity.Property(e => e.FechaFin).HasColumnType("datetime");
            entity.Property(e => e.FechaInicio).HasColumnType("datetime");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.PrecioCombo).HasColumnType("money");

            entity.HasOne(d => d.IdSucursalNavigation).WithMany(p => p.Combos)
                .HasForeignKey(d => d.IdSucursal)
                .HasConstraintName("FK__Combos__ID_Sucur__2A0C1F93");
        });

        modelBuilder.Entity<ComprobantesCarga>(entity =>
        {
            entity.HasKey(e => e.IdComprobanteCarga).HasName("PK_ComoprobantesCarga");

            entity.Property(e => e.IdComprobanteCarga).HasColumnName("ID_ComprobanteCarga");
            entity.Property(e => e.FechaCarga).HasColumnType("datetime");
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
        });

        modelBuilder.Entity<ComprobantesLetras>(entity =>
        {
            entity.HasKey(e => e.IdComprobanteLetra);

            entity.Property(e => e.IdComprobanteLetra).HasColumnName("ID_ComprobanteLetra");
            entity.Property(e => e.IdCategoriaIvacliente).HasColumnName("ID_CategoriaIVACliente");
            entity.Property(e => e.IdCategoriaIvaproveedor).HasColumnName("ID_CategoriaIVAProveedor");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.Letra)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ComprobantesTipo>(entity =>
        {
            entity.HasKey(e => e.IdComprobanteTipo);

            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.LibroIva).HasColumnName("LibroIVA");
            entity.Property(e => e.Reducida)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ConciliacionBancaria>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.Cuenta)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.IdBanco).HasColumnName("ID_Banco");
            entity.Property(e => e.IdBancoCuenta).HasColumnName("ID_BancoCuenta");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdConciliacionBancaria)
                .ValueGeneratedOnAdd()
                .HasColumnName("ID_ConciliacionBancaria");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdPlanillaCaja).HasColumnName("ID_PlanillaCaja");
            entity.Property(e => e.IdPuntoVenta).HasColumnName("ID_PuntoVenta");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.IdTipo).HasColumnName("ID_Tipo");
            entity.Property(e => e.IdTipoAjuste).HasColumnName("ID_TipoAjuste");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.Letra)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Numero)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TotalGeneral).HasColumnType("money");
        });

        modelBuilder.Entity<ConciliacionBancariaDetalle>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.Cantidad).HasColumnType("money");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.EstadoLibroIva).HasColumnName("EstadoLibroIVA");
            entity.Property(e => e.IdConciliacionBancaria).HasColumnName("ID_ConciliacionBancaria");
            entity.Property(e => e.IdConciliacionBancariaDetalle)
                .ValueGeneratedOnAdd()
                .HasColumnName("ID_ConciliacionBancariaDetalle");
            entity.Property(e => e.IdImpuestoIva).HasColumnName("ID_ImpuestoIVA");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.IdListaPrecio).HasColumnName("ID_ListaPrecio");
            entity.Property(e => e.Iva)
                .HasColumnType("money")
                .HasColumnName("IVA");
            entity.Property(e => e.Ivaalic)
                .HasColumnType("money")
                .HasColumnName("IVAAlic");
            entity.Property(e => e.ListaPrecio)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Neto).HasColumnType("money");
            entity.Property(e => e.Observaciones)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Otros).HasColumnType("money");
            entity.Property(e => e.PrecioUnitario).HasColumnType("money");
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<Consultas>(entity =>
        {
            entity.HasKey(e => e.IdConsulta);

            entity.Property(e => e.IdConsulta).HasColumnName("ID_Consulta");
            entity.Property(e => e.Email)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Fecha).HasColumnType("datetime");
            entity.Property(e => e.IdLocalidad).HasColumnName("ID_Localidad");
            entity.Property(e => e.IdProvincia).HasColumnName("ID_Provincia");
            entity.Property(e => e.Nombre)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NombreEmpresa)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Observaciones).HasColumnType("text");
            entity.Property(e => e.RubroEmpresa)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Sucursales)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Telefono)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ConsultasDetalle>(entity =>
        {
            entity.HasKey(e => e.IdConsultaDetalle);

            entity.Property(e => e.IdConsultaDetalle).HasColumnName("ID_ConsultaDetalle");
            entity.Property(e => e.IdConsulta).HasColumnName("ID_Consulta");
            entity.Property(e => e.IdMotivo).HasColumnName("ID_Motivo");
        });

        modelBuilder.Entity<DatosEmpresa>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.ClaveFiscal)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("Clave_Fiscal");
            entity.Property(e => e.CondicionIva).HasColumnName("Condicion_IVA");
            entity.Property(e => e.CuitEmpresa)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CUIT_Empresa");
            entity.Property(e => e.CuitPersona)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CUIT_Persona");
            entity.Property(e => e.DireccionComercial)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("Direccion_Comercial");
            entity.Property(e => e.Email)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FechaInicio).HasColumnName("Fecha_Inicio");
            entity.Property(e => e.IdDatosEmpresa)
                .ValueGeneratedOnAdd()
                .HasColumnName("ID_DatosEmpresa");
            entity.Property(e => e.IdLocalidad).HasColumnName("ID_Localidad");
            entity.Property(e => e.IdProvincia).HasColumnName("ID_Provincia");
            entity.Property(e => e.Logo).IsUnicode(false);
            entity.Property(e => e.NombreFantasia)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("Nombre_Fantasia");
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("Razon_Social");
            entity.Property(e => e.Telefono)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<DocumentosCliente>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoCliente);

            entity.HasIndex(e => new { e.IdCliente, e.Estado }, "IX_DocumentosCliente_Cliente_Estado").HasFillFactor(90);

            entity.Property(e => e.IdDocumentoCliente).HasColumnName("ID_DocumentoCliente");
            entity.Property(e => e.BarCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Cae)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CAE");
            entity.Property(e => e.Calle)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Chofer)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.IdCategoriaIva).HasColumnName("ID_CategoriaIVA");
            entity.Property(e => e.IdChofer).HasColumnName("ID_Chofer");
            entity.Property(e => e.IdCliente).HasColumnName("ID_Cliente");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdCondicion).HasColumnName("ID_Condicion");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdLocalidad).HasColumnName("ID_Localidad");
            entity.Property(e => e.IdPlanillaCaja).HasColumnName("ID_PlanillaCaja");
            entity.Property(e => e.IdProvincia).HasColumnName("ID_Provincia");
            entity.Property(e => e.IdPuntoVenta).HasColumnName("ID_PuntoVenta");
            entity.Property(e => e.IdRemito).HasColumnName("ID_Remito");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.IdTransporte).HasColumnName("ID_Transporte");
            entity.Property(e => e.IdUnidad).HasColumnName("ID_Unidad");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.Letra)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Nro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NroDoc)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Numero)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Observaciones).IsUnicode(false);
            entity.Property(e => e.Porcentaje).HasColumnType("money");
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TotalDescuento).HasColumnType("money");
            entity.Property(e => e.TotalGeneral).HasColumnType("money");
            entity.Property(e => e.TotalIva)
                .HasColumnType("money")
                .HasColumnName("TotalIVA");
            entity.Property(e => e.TotalNeto).HasColumnType("money");
            entity.Property(e => e.TotalOtrosImpuestos).HasColumnType("money");
            entity.Property(e => e.TotalRecargo).HasColumnType("money");
            entity.Property(e => e.Transporte)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Unidad)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.VtoCae)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("VtoCAE");
        });

        modelBuilder.Entity<DocumentosClienteDetalle>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoClienteDetalle);

            entity.HasIndex(e => e.IdDocumentoCliente, "IX_DocumentosClienteDetalle_Documento");

            entity.Property(e => e.IdDocumentoClienteDetalle).HasColumnName("ID_DocumentoClienteDetalle");
            entity.Property(e => e.Cantidad).HasColumnType("money");
            entity.Property(e => e.Descripcion).IsUnicode(false);
            entity.Property(e => e.Descuento).HasColumnType("money");
            entity.Property(e => e.EstadoLibroIva).HasColumnName("EstadoLibroIVA");
            entity.Property(e => e.IdDocumentoCliente).HasColumnName("ID_DocumentoCliente");
            entity.Property(e => e.IdImpuestoIva).HasColumnName("ID_ImpuestoIVA");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.IdListaPrecio).HasColumnName("ID_ListaPrecio");
            entity.Property(e => e.Iva)
                .HasColumnType("money")
                .HasColumnName("IVA");
            entity.Property(e => e.Ivaalic)
                .HasColumnType("money")
                .HasColumnName("IVAAlic");
            entity.Property(e => e.ListaPrecio)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Metros).HasColumnType("money");
            entity.Property(e => e.Neto).HasColumnType("money");
            entity.Property(e => e.Observaciones)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Otros).HasColumnType("money");
            entity.Property(e => e.Porcentaje).HasColumnType("money");
            entity.Property(e => e.PrecioUnitario).HasColumnType("money");
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<DocumentosClienteDetalleEnvases>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoClienteDetalleEnvase);

            entity.Property(e => e.IdDocumentoClienteDetalleEnvase).HasColumnName("ID_DocumentoClienteDetalleEnvase");
            entity.Property(e => e.Cantidad).HasColumnType("money");
            entity.Property(e => e.IdDocumentoClienteDetalle).HasColumnName("ID_DocumentoClienteDetalle");
            entity.Property(e => e.IdEnvase).HasColumnName("ID_Envase");
            entity.Property(e => e.PesoEnvase).HasColumnType("money");
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<DocumentosClienteFotosApp>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoClienteFotoApp);

            entity.Property(e => e.IdDocumentoClienteFotoApp).HasColumnName("ID_DocumentoClienteFotoApp");
            entity.Property(e => e.FotoBalanza)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.FotoTransporte)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IdDocumentoCliente).HasColumnName("ID_DocumentoCliente");
        });

        modelBuilder.Entity<DocumentosClienteObservaciones>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoClienteObservacion);

            entity.Property(e => e.IdDocumentoClienteObservacion).HasColumnName("ID_DocumentoClienteObservacion");
            entity.Property(e => e.IdDocumentoCliente).HasColumnName("ID_DocumentoCliente");
            entity.Property(e => e.Observaciones).IsUnicode(false);
        });

        modelBuilder.Entity<DocumentosClienteQr>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoClienteQr);

            entity.ToTable("DocumentosClienteQR");

            entity.Property(e => e.IdDocumentoClienteQr).HasColumnName("ID_DocumentoClienteQR");
            entity.Property(e => e.CodigoQr)
                .IsUnicode(false)
                .HasColumnName("CodigoQR");
            entity.Property(e => e.IdDocumentoCliente).HasColumnName("ID_DocumentoCliente");
            entity.Property(e => e.Url)
                .HasMaxLength(500)
                .IsUnicode(false);
        });

        modelBuilder.Entity<DocumentosClienteRelacion>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoClienteRelacion);

            entity.Property(e => e.IdDocumentoClienteRelacion).HasColumnName("ID_DocumentoClienteRelacion");
            entity.Property(e => e.IdDocumentoCliente1).HasColumnName("ID_DocumentoCliente1");
            entity.Property(e => e.IdDocumentoCliente2).HasColumnName("ID_DocumentoCliente2");
        });

        modelBuilder.Entity<DocumentosClienteRemitos>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoClienteRemito);

            entity.Property(e => e.IdDocumentoClienteRemito).HasColumnName("ID_DocumentoClienteRemito");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdDocumentoCliente).HasColumnName("ID_DocumentoCliente");
            entity.Property(e => e.IdRemito).HasColumnName("ID_Remito");
        });

        modelBuilder.Entity<DocumentosClienteRemitosDetalle>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoClienteRemitoDetalle);

            entity.Property(e => e.IdDocumentoClienteRemitoDetalle).HasColumnName("ID_DocumentoClienteRemitoDetalle");
            entity.Property(e => e.CantidadFactura).HasColumnType("money");
            entity.Property(e => e.CantidadRemito).HasColumnType("money");
            entity.Property(e => e.IdDocumentoCliente).HasColumnName("ID_DocumentoCliente");
            entity.Property(e => e.IdDocumentoClienteRemito).HasColumnName("ID_DocumentoClienteRemito");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.IdRemito).HasColumnName("ID_Remito");
            entity.Property(e => e.Saldo).HasColumnType("money");
        });

        modelBuilder.Entity<DocumentosClienteVencimientos>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoClienteVencimiento);

            entity.Property(e => e.IdDocumentoClienteVencimiento).HasColumnName("ID_DocumentoClienteVencimiento");
            entity.Property(e => e.FechaVencimiento).HasColumnType("datetime");
            entity.Property(e => e.IdDocumentoCliente).HasColumnName("ID_DocumentoCliente");
        });

        modelBuilder.Entity<DocumentosClientesOtrosTributos>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoClienteOtroTributo);

            entity.Property(e => e.IdDocumentoClienteOtroTributo).HasColumnName("ID_DocumentoClienteOtroTributo");
            entity.Property(e => e.Alicuota).HasColumnType("money");
            entity.Property(e => e.BaseImponible).HasColumnType("money");
            entity.Property(e => e.Detalle)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdDocumentoCliente).HasColumnName("ID_DocumentoCliente");
            entity.Property(e => e.IdOtroTributo).HasColumnName("ID_OtroTributo");
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<DocumentosProveedor>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoProveedor);

            entity.Property(e => e.IdDocumentoProveedor).HasColumnName("ID_DocumentoProveedor");
            entity.Property(e => e.Cae)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CAE");
            entity.Property(e => e.Calle)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Chofer)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.IdCategoriaIva).HasColumnName("ID_CategoriaIVA");
            entity.Property(e => e.IdChofer).HasColumnName("ID_Chofer");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdCondicion).HasColumnName("ID_Condicion");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdLocalidad).HasColumnName("ID_Localidad");
            entity.Property(e => e.IdPlanillaCaja).HasColumnName("ID_PlanillaCaja");
            entity.Property(e => e.IdProveedor).HasColumnName("ID_Proveedor");
            entity.Property(e => e.IdProvincia).HasColumnName("ID_Provincia");
            entity.Property(e => e.IdPuntoVenta).HasColumnName("ID_PuntoVenta");
            entity.Property(e => e.IdRemito).HasColumnName("ID_Remito");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.IdTransporte).HasColumnName("ID_Transporte");
            entity.Property(e => e.IdUnidad).HasColumnName("ID_Unidad");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.Letra)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Nro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NroDoc)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Numero)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PorcentajeDescuento).HasColumnType("money");
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TotalDescuento).HasColumnType("money");
            entity.Property(e => e.TotalGeneral).HasColumnType("money");
            entity.Property(e => e.TotalIva)
                .HasColumnType("money")
                .HasColumnName("TotalIVA");
            entity.Property(e => e.TotalNeto).HasColumnType("money");
            entity.Property(e => e.TotalOtrosImpuestos).HasColumnType("money");
            entity.Property(e => e.Transporte)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Unidad)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.VtoCae)
                .HasColumnType("datetime")
                .HasColumnName("VtoCAE");
        });

        modelBuilder.Entity<DocumentosProveedorDetalle>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoProveedorDetalle);

            entity.Property(e => e.IdDocumentoProveedorDetalle).HasColumnName("ID_DocumentoProveedorDetalle");
            entity.Property(e => e.Bonificacion).HasColumnType("money");
            entity.Property(e => e.Cantidad).HasColumnType("money");
            entity.Property(e => e.Descripcion).IsUnicode(false);
            entity.Property(e => e.EstadoLibroIva).HasColumnName("EstadoLibroIVA");
            entity.Property(e => e.IdDocumentoProveedor).HasColumnName("ID_DocumentoProveedor");
            entity.Property(e => e.IdImpuestoIva).HasColumnName("ID_ImpuestoIVA");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.IdListaPrecio).HasColumnName("ID_ListaPrecio");
            entity.Property(e => e.Iva)
                .HasColumnType("money")
                .HasColumnName("IVA");
            entity.Property(e => e.Ivaalic)
                .HasColumnType("money")
                .HasColumnName("IVAAlic");
            entity.Property(e => e.ListaPrecio)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Metros).HasColumnType("money");
            entity.Property(e => e.Neto).HasColumnType("money");
            entity.Property(e => e.Observaciones)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Otros).HasColumnType("money");
            entity.Property(e => e.PrecioUnitario).HasColumnType("money");
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<DocumentosProveedorObservaciones>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoProveedorObservacion);

            entity.Property(e => e.IdDocumentoProveedorObservacion).HasColumnName("ID_DocumentoProveedorObservacion");
            entity.Property(e => e.IdDocumentoProveedor).HasColumnName("ID_DocumentoProveedor");
            entity.Property(e => e.Observaciones).IsUnicode(false);
        });

        modelBuilder.Entity<DocumentosProveedorOtrosTributos>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoProveedorOtroTributo);

            entity.Property(e => e.IdDocumentoProveedorOtroTributo).HasColumnName("ID_DocumentoProveedorOtroTributo");
            entity.Property(e => e.Alicuota).HasColumnType("money");
            entity.Property(e => e.BaseImponible).HasColumnType("money");
            entity.Property(e => e.Detalle)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdDocumentoProveedor).HasColumnName("ID_DocumentoProveedor");
            entity.Property(e => e.IdOtroTributo).HasColumnName("ID_OtroTributo");
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<DocumentosProveedorRemitos>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoProveedorRemito);

            entity.Property(e => e.IdDocumentoProveedorRemito).HasColumnName("ID_DocumentoProveedorRemito");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdDocumentoProveedor).HasColumnName("ID_DocumentoProveedor");
            entity.Property(e => e.IdRemito).HasColumnName("ID_Remito");
        });

        modelBuilder.Entity<DocumentosProveedorRemitosDetalle>(entity =>
        {
            entity.HasKey(e => e.IdDocumentoProveeorRemitoDetalle);

            entity.Property(e => e.IdDocumentoProveeorRemitoDetalle).HasColumnName("ID_DocumentoProveeorRemitoDetalle");
            entity.Property(e => e.CantidadFactura).HasColumnType("money");
            entity.Property(e => e.CantidadRemito).HasColumnType("money");
            entity.Property(e => e.IdDocumentoProveedor).HasColumnName("ID_DocumentoProveedor");
            entity.Property(e => e.IdDocumentoProveedorRemito).HasColumnName("ID_DocumentoProveedorRemito");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.IdRemito).HasColumnName("ID_Remito");
            entity.Property(e => e.Saldo).HasColumnType("money");
        });

        modelBuilder.Entity<ElementosCobroPago>(entity =>
        {
            entity.HasKey(e => e.IdElementroCobroPago);

            entity.Property(e => e.IdElementroCobroPago).HasColumnName("ID_ElementroCobroPago");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdTipo).HasColumnName("ID_Tipo");
            entity.Property(e => e.Reducida)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Empresas>(entity =>
        {
            entity.HasKey(e => e.IdEmpresa);

            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.Calle)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Email)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Fantasia)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FechaAlta).HasColumnType("datetime");
            entity.Property(e => e.IdCategoriaIva).HasColumnName("ID_CategoriaIVA");
            entity.Property(e => e.IdEmpresaTipo).HasColumnName("ID_EmpresaTipo");
            entity.Property(e => e.IdLocalidad).HasColumnName("ID_Localidad");
            entity.Property(e => e.IdProvincia).HasColumnName("ID_Provincia");
            entity.Property(e => e.Iibb)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("IIBB");
            entity.Property(e => e.Imagen)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Nro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NroCuit)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Telefono)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<EntidadOrdenPagoDocumentosProveedores>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdDocumentoProveedor).HasColumnName("ID_DocumentoProveedor");
            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.IdEntidadOrdenPago).HasColumnName("ID_EntidadOrdenPago");
            entity.Property(e => e.IdEntidadOrdenPagoDocumentoProveedor)
                .ValueGeneratedOnAdd()
                .HasColumnName("ID_EntidadOrdenPagoDocumentoProveedor");
            entity.Property(e => e.ImporteComprobante).HasColumnType("money");
            entity.Property(e => e.ImporteOrdenPago).HasColumnType("money");
            entity.Property(e => e.NumeroComprobante)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NumeroOrdenPago)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Saldo).HasColumnType("money");
        });

        modelBuilder.Entity<EntidadOrdenesDePagoDocumentosProveedor>(entity =>
        {
            entity.HasKey(e => e.IdEntidadOrdenDePagoDocumentoProveedor);

            entity.Property(e => e.IdEntidadOrdenDePagoDocumentoProveedor).HasColumnName("ID_EntidadOrdenDePagoDocumentoProveedor");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdDocumentoProveedor).HasColumnName("ID_DocumentoProveedor");
            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.IdOrdenDePago).HasColumnName("ID_OrdenDePago");
            entity.Property(e => e.ImporteComprobante).HasColumnType("money");
            entity.Property(e => e.ImporteOrdenDePago).HasColumnType("money");
            entity.Property(e => e.NumeroComprobante)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NumeroOrdenDePago)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<EntidadRecibosDocumentosCliente>(entity =>
        {
            entity.HasKey(e => e.IdEntidadReciboDocumentoCliente);

            entity.Property(e => e.IdEntidadReciboDocumentoCliente).HasColumnName("ID_EntidadReciboDocumentoCliente");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdDocumentoCliente).HasColumnName("ID_DocumentoCliente");
            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.IdEntidadRecibo).HasColumnName("ID_EntidadRecibo");
            entity.Property(e => e.ImporteComprobante).HasColumnType("money");
            entity.Property(e => e.ImporteRecibo).HasColumnType("money");
            entity.Property(e => e.NumeroComprobante)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NumeroRecibo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Saldo).HasColumnType("money");
        });

        modelBuilder.Entity<Entidades>(entity =>
        {
            entity.HasKey(e => e.IdEntidad);

            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.Cuit)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("CUIT");
            entity.Property(e => e.Direccion)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Email)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Fantasia)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IdCategoriaIva).HasColumnName("ID_CategoriaIVA");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdEntidadCategoria).HasColumnName("ID_EntidadCategoria");
            entity.Property(e => e.IdEstado).HasColumnName("ID_Estado");
            entity.Property(e => e.IdLocalidad).HasColumnName("ID_Localidad");
            entity.Property(e => e.IdPadre).HasColumnName("ID_Padre");
            entity.Property(e => e.IdProvincia).HasColumnName("ID_Provincia");
            entity.Property(e => e.Imagen).HasColumnType("text");
            entity.Property(e => e.Lat)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.LimiteCtaCte).HasColumnType("money");
            entity.Property(e => e.Lng)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.NroCalle)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Observaciones).IsUnicode(false);
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Telefono)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Url)
                .HasMaxLength(500)
                .IsUnicode(false);
        });

        modelBuilder.Entity<EntidadesAreasDeContacto>(entity =>
        {
            entity.HasKey(e => e.IdEntidadAreaDeContacto);

            entity.Property(e => e.IdEntidadAreaDeContacto).HasColumnName("ID_EntidadAreaDeContacto");
            entity.Property(e => e.EmailNumero)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IdAreaDeContacto).HasColumnName("ID_AreaDeContacto");
            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.IdMedioDeContacto).HasColumnName("ID_MedioDeContacto");
            entity.Property(e => e.NombreDeContacto)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<EntidadesCategorias>(entity =>
        {
            entity.HasKey(e => e.IdEntidadCategoria);

            entity.Property(e => e.IdEntidadCategoria).HasColumnName("ID_EntidadCategoria");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
        });

        modelBuilder.Entity<EntidadesCheques>(entity =>
        {
            entity.HasKey(e => e.IdEntidadCheque).HasName("PK_ClientesCheques");

            entity.Property(e => e.IdEntidadCheque).HasColumnName("ID_EntidadCheque");
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.FechaRecepcion).HasColumnType("datetime");
            entity.Property(e => e.FechaVencimiento).HasColumnType("datetime");
            entity.Property(e => e.IdBanco).HasColumnName("ID_Banco");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.IdEntidadRecibo).HasColumnName("ID_EntidadRecibo");
            entity.Property(e => e.IdEntidadReciboTipo).HasColumnName("ID_EntidadReciboTipo");
            entity.Property(e => e.IdProveedorRecibo).HasColumnName("ID_ProveedorRecibo");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.IdSucursalEmpresa).HasColumnName("ID_SucursalEmpresa");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.Importe).HasColumnType("money");
            entity.Property(e => e.Nro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Observaciones).HasColumnType("text");
            entity.Property(e => e.ValorToma).HasColumnType("money");
        });

        modelBuilder.Entity<EntidadesContacto>(entity =>
        {
            entity.HasKey(e => e.IdEntidadContacto);

            entity.Property(e => e.IdEntidadContacto).HasColumnName("ID_EntidadContacto");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.Valor)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<EntidadesCtaCte>(entity =>
        {
            entity.HasKey(e => e.IdEntidadCtaCte);

            entity.HasIndex(e => new { e.IdComprobante, e.IdComprobanteTipo }, "IX_EntidadesCtaCte_Comprobante_Tipo");

            entity.HasIndex(e => new { e.IdEntidad, e.Estado }, "IX_EntidadesCtaCte_Entidad_Estado");

            entity.Property(e => e.IdEntidadCtaCte).HasColumnName("ID_EntidadCtaCte");
            entity.Property(e => e.Concepto)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Fecha).HasColumnType("datetime");
            entity.Property(e => e.FechaAnulacion).HasColumnType("datetime");
            entity.Property(e => e.FechaPago).HasColumnType("datetime");
            entity.Property(e => e.FechaVencimiento).HasColumnType("datetime");
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.InteresAplicado).HasColumnType("money");
            entity.Property(e => e.Saldo).HasColumnType("money");
            entity.Property(e => e.Total).HasColumnType("money");
            entity.Property(e => e.Total2).HasColumnType("money");
        });

        modelBuilder.Entity<EntidadesCtaCteMovimientos>(entity =>
        {
            entity.HasKey(e => e.IdEntidadCtaCteMovimiento);

            entity.Property(e => e.IdEntidadCtaCteMovimiento).HasColumnName("ID_EntidadCtaCteMovimiento");
            entity.Property(e => e.AfavorEntidad)
                .HasColumnType("money")
                .HasColumnName("AFavorEntidad");
            entity.Property(e => e.Concepto)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.EnContraEntidad).HasColumnType("money");
            entity.Property(e => e.Fecha).HasColumnType("datetime");
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdElemento).HasColumnName("ID_Elemento");
            entity.Property(e => e.IdElementoCobroPago).HasColumnName("ID_ElementoCobroPago");
            entity.Property(e => e.IdEntidadCtaCte).HasColumnName("ID_EntidadCtaCte");
        });

        modelBuilder.Entity<EntidadesCtaCteReporte>(entity =>
        {
            entity.HasKey(e => e.IdEntidadCtaCteReporte);

            entity.Property(e => e.IdEntidadCtaCteReporte).HasColumnName("ID_EntidadCtaCteReporte");
            entity.Property(e => e.Concepto)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Debe).HasColumnType("money");
            entity.Property(e => e.Estado)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Fecha).HasColumnType("datetime");
            entity.Property(e => e.Haber).HasColumnType("money");
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.Nro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Observaciones).IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
            entity.Property(e => e.Vencimiento).HasColumnType("datetime");
        });

        modelBuilder.Entity<EntidadesCtaCteStockMovimientosDetalle>(entity =>
        {
            entity.HasKey(e => e.IdEntidadCtaCteStockMovimientoDetalle);

            entity.Property(e => e.IdEntidadCtaCteStockMovimientoDetalle).HasColumnName("ID_EntidadCtaCteStockMovimientoDetalle");
            entity.Property(e => e.Concepto)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Fecha).HasColumnType("datetime");
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteDetalle).HasColumnName("ID_ComprobanteDetalle");
            entity.Property(e => e.IdComprobanteRelacion).HasColumnName("ID_ComprobanteRelacion");
            entity.Property(e => e.IdComprobanteRelacionDetalle).HasColumnName("ID_ComprobanteRelacionDetalle");
            entity.Property(e => e.IdComprobanteRelacionTipo).HasColumnName("ID_ComprobanteRelacionTipo");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.Saldo).HasColumnType("money");
            entity.Property(e => e.Saldo2).HasColumnType("money");
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<EntidadesDirecciones>(entity =>
        {
            entity.HasKey(e => e.IdEntidadDireccion);

            entity.Property(e => e.IdEntidadDireccion).HasColumnName("ID_EntidadDireccion");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Domicilio)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.GeoLatitud)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.GeoLongitud)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.IdLocalidad).HasColumnName("ID_Localidad");
            entity.Property(e => e.IdProvincia).HasColumnName("ID_Provincia");
        });

        modelBuilder.Entity<EntidadesEntidadesTipo>(entity =>
        {
            entity.HasKey(e => e.IdEntidadEntidadTipo);

            entity.Property(e => e.IdEntidadEntidadTipo).HasColumnName("ID_EntidadEntidadTipo");
            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.IdEntidadTipo).HasColumnName("ID_EntidadTipo");
        });

        modelBuilder.Entity<EntidadesFacturacion>(entity =>
        {
            entity.HasKey(e => e.IdEntidadFacturacion);

            entity.Property(e => e.IdEntidadFacturacion).HasColumnName("ID_EntidadFacturacion");
            entity.Property(e => e.Cuit)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CUIT");
            entity.Property(e => e.Domicilio)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.DomicilioNro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Fantasia)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.GeoLatitud)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.GeoLongitud)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IdCategoriaIva).HasColumnName("ID_CategoriaIVA");
            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.IdLocalidad).HasColumnName("ID_Localidad");
            entity.Property(e => e.IdProvincia).HasColumnName("ID_Provincia");
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<EntidadesRecibos>(entity =>
        {
            entity.HasKey(e => e.IdEntidadRecibo).HasName("PK_ClientesRecibos");

            entity.Property(e => e.IdEntidadRecibo).HasColumnName("ID_EntidadRecibo");
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.IdCategoriaIva).HasColumnName("ID_CategoriaIVA");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.IdPlanillaCaja).HasColumnName("ID_PlanillaCaja");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.Letra)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NroDoc)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Numero)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Observaciones)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<EntidadesRecibosDetalle>(entity =>
        {
            entity.HasKey(e => e.IdEntidadReciboDetalle).HasName("PK_ClientesRecibosDetalle");

            entity.Property(e => e.IdEntidadReciboDetalle).HasColumnName("ID_EntidadReciboDetalle");
            entity.Property(e => e.Banco)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Detalle)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Emision).HasColumnType("datetime");
            entity.Property(e => e.IdBanco).HasColumnName("ID_Banco");
            entity.Property(e => e.IdElemento).HasColumnName("ID_Elemento");
            entity.Property(e => e.IdElementoCobroPago).HasColumnName("ID_ElementoCobroPago");
            entity.Property(e => e.IdEntidadRecibo).HasColumnName("ID_EntidadRecibo");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.Nro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Recepcion).HasColumnType("datetime");
            entity.Property(e => e.Sucursal)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
            entity.Property(e => e.Vto).HasColumnType("datetime");
        });

        modelBuilder.Entity<EntidadesTipo>(entity =>
        {
            entity.HasKey(e => e.IdEntidadTipo);

            entity.Property(e => e.IdEntidadTipo).HasColumnName("ID_EntidadTipo");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(500)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Estados>(entity =>
        {
            entity.HasKey(e => e.IdEstado);

            entity.Property(e => e.IdEstado).HasColumnName("ID_Estado");
            entity.Property(e => e.Categoria)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Imagen)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Nombre)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Impuestos>(entity =>
        {
            entity.HasKey(e => e.IdImpuesto);

            entity.Property(e => e.IdImpuesto).HasColumnName("ID_Impuesto");
            entity.Property(e => e.Alicuota).HasColumnType("money");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.EsIva).HasColumnName("EsIVA");
            entity.Property(e => e.Importe).HasColumnType("money");
            entity.Property(e => e.Reducida)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Items>(entity =>
        {
            entity.HasKey(e => e.IdItem);

            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.Barcode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CodFabrica)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Descripcion).IsUnicode(false);
            entity.Property(e => e.Detalle).IsUnicode(false);
            entity.Property(e => e.FechaCompra).HasColumnType("datetime");
            entity.Property(e => e.FechaVenta).HasColumnType("datetime");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdItemTipo).HasColumnName("ID_ItemTipo");
            entity.Property(e => e.IdMarca).HasColumnName("ID_Marca");
            entity.Property(e => e.IdModelo).HasColumnName("ID_Modelo");
            entity.Property(e => e.IdRubro).HasColumnName("ID_Rubro");
            entity.Property(e => e.IdSubRubro).HasColumnName("ID_SubRubro");
            entity.Property(e => e.IdUbicacion).HasColumnName("ID_Ubicacion");
            entity.Property(e => e.IdUnidadMedida).HasColumnName("ID_UnidadMedida");
            entity.Property(e => e.MtsKgs).HasColumnType("money");
            entity.Property(e => e.Neto).HasColumnType("money");
            entity.Property(e => e.Rentabilidad).HasColumnType("money");
            entity.Property(e => e.StockActual).HasColumnType("money");
            entity.Property(e => e.StockInicial).HasColumnType("money");
            entity.Property(e => e.StockMaximo).HasColumnType("money");
            entity.Property(e => e.StockMinimo).HasColumnType("money");
            entity.Property(e => e.UnidadesXbulto)
                .HasColumnType("money")
                .HasColumnName("UnidadesXBulto");
        });

        modelBuilder.Entity<ItemsEnvanses>(entity =>
        {
            entity.HasKey(e => e.IdItemEnvase);

            entity.Property(e => e.IdItemEnvase).HasColumnName("ID_ItemEnvase");
            entity.Property(e => e.CantidadEnvases).HasColumnType("money");
            entity.Property(e => e.CantidadKg).HasColumnType("money");
            entity.Property(e => e.IdEnvase).HasColumnName("ID_Envase");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.PesoEnvase).HasColumnType("money");
        });

        modelBuilder.Entity<ItemsImagenes>(entity =>
        {
            entity.HasKey(e => e.IdItemImagen);

            entity.Property(e => e.IdItemImagen).HasColumnName("ID_ItemImagen");
            entity.Property(e => e.CodFabrica)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.Imagen).IsUnicode(false);
        });

        modelBuilder.Entity<ItemsImpuestos>(entity =>
        {
            entity.HasKey(e => e.IdItemImpuesto);

            entity.Property(e => e.IdItemImpuesto).HasColumnName("ID_ItemImpuesto");
            entity.Property(e => e.IdImpuesto).HasColumnName("ID_Impuesto");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
        });

        modelBuilder.Entity<ItemsMovimientosDetalles>(entity =>
        {
            entity.HasKey(e => e.IdItemMovientoDetalle).HasName("PK_ItemMovimientoDetalle");

            entity.Property(e => e.IdItemMovientoDetalle).HasColumnName("ID_ItemMovientoDetalle");
            entity.Property(e => e.Concepto)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Debe).HasColumnType("money");
            entity.Property(e => e.FechaAlta).HasColumnType("datetime");
            entity.Property(e => e.Haber).HasColumnType("money");
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteDetalle).HasColumnName("ID_ComprobanteDetalle");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.Item)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
            entity.Property(e => e.Total2).HasColumnType("money");
        });

        modelBuilder.Entity<ItemsNroSeries>(entity =>
        {
            entity.HasKey(e => e.IdItemNroSerie);

            entity.Property(e => e.IdItemNroSerie).HasColumnName("ID_ItemNroSerie");
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdDocumentoClienteDetalle).HasColumnName("ID_DocumentoClienteDetalle");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.NroSerie)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ItemsOfertas>(entity =>
        {
            entity.HasKey(e => e.IdItemOferta);

            entity.Property(e => e.IdItemOferta).HasColumnName("ID_ItemOferta");
            entity.Property(e => e.IdItems).HasColumnName("ID_Items");
            entity.Property(e => e.Precio).HasColumnType("money");
        });

        modelBuilder.Entity<ItemsPreciosActualizacion>(entity =>
        {
            entity.HasKey(e => e.IdItemPrecioActulizacion);

            entity.Property(e => e.IdItemPrecioActulizacion).HasColumnName("ID_ItemPrecioActulizacion");
            entity.Property(e => e.FechaActualizacion).HasColumnType("datetime");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.IdUsario).HasColumnName("ID_Usario");
            entity.Property(e => e.PrecioActual).HasColumnType("money");
            entity.Property(e => e.PrecioNuevo).HasColumnType("money");
            entity.Property(e => e.Rentabilidad).HasColumnType("numeric(18, 0)");
        });

        modelBuilder.Entity<ItemsSucursales>(entity =>
        {
            entity.HasKey(e => e.IdItemSucursal);

            entity.Property(e => e.IdItemSucursal).HasColumnName("ID_ItemSucursal");
            entity.Property(e => e.Alicuota).HasColumnType("money");
            entity.Property(e => e.Barcode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CodFabrica)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Costo).HasColumnType("money");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdImpuesto).HasColumnName("ID_Impuesto");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.IdItemTipo).HasColumnName("ID_ItemTipo");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.IdUbicacion).HasColumnName("ID_Ubicacion");
            entity.Property(e => e.Neto).HasColumnType("money");
            entity.Property(e => e.PrecioOferta).HasColumnType("money");
            entity.Property(e => e.PrecioVenta).HasColumnType("money");
            entity.Property(e => e.Rentabilidad).HasColumnType("money");
            entity.Property(e => e.Stock).HasColumnType("money");
            entity.Property(e => e.StockInicial).HasColumnType("money");
            entity.Property(e => e.StockMaximo).HasColumnType("money");
            entity.Property(e => e.StockMinimo).HasColumnType("money");
        });

        modelBuilder.Entity<LibroIvaCompra>(entity =>
        {
            entity.HasKey(e => e.IdLibroIvaCompra).HasName("PK_LibroIvaCompras");

            entity.Property(e => e.IdLibroIvaCompra).HasColumnName("ID_LibroIvaCompra");
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.ImpInterno).HasColumnType("money");
            entity.Property(e => e.ImpMunicipal).HasColumnType("money");
            entity.Property(e => e.ImpNacional).HasColumnType("money");
            entity.Property(e => e.IngBruto).HasColumnType("money");
            entity.Property(e => e.Iva10).HasColumnType("money");
            entity.Property(e => e.Iva21).HasColumnType("money");
            entity.Property(e => e.Iva27).HasColumnType("money");
            entity.Property(e => e.IvaExcento).HasColumnType("money");
            entity.Property(e => e.Letra)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Neto10).HasColumnType("money");
            entity.Property(e => e.Neto21).HasColumnType("money");
            entity.Property(e => e.Neto27).HasColumnType("money");
            entity.Property(e => e.NetoExento).HasColumnType("money");
            entity.Property(e => e.NroDocumento)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Numero)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OtrosTributo).HasColumnType("money");
            entity.Property(e => e.Percepciones).HasColumnType("money");
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TipoComprobante)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TotalGeneral).HasColumnType("money");
            entity.Property(e => e.TotalIva)
                .HasColumnType("money")
                .HasColumnName("TotalIVA");
            entity.Property(e => e.TotalNeto).HasColumnType("money");
        });

        modelBuilder.Entity<LibroIvaVenta>(entity =>
        {
            entity.HasKey(e => e.IdLibroIvaVenta).HasName("PK_LibroIvaVentasnew");

            entity.Property(e => e.IdLibroIvaVenta).HasColumnName("ID_LibroIvaVenta");
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.ImpInterno).HasColumnType("money");
            entity.Property(e => e.ImpMunicipal).HasColumnType("money");
            entity.Property(e => e.ImpNacional).HasColumnType("money");
            entity.Property(e => e.IngBruto).HasColumnType("money");
            entity.Property(e => e.Iva10).HasColumnType("money");
            entity.Property(e => e.Iva21).HasColumnType("money");
            entity.Property(e => e.Iva27).HasColumnType("money");
            entity.Property(e => e.IvaExcento).HasColumnType("money");
            entity.Property(e => e.Letra)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Neto10).HasColumnType("money");
            entity.Property(e => e.Neto21).HasColumnType("money");
            entity.Property(e => e.Neto27).HasColumnType("money");
            entity.Property(e => e.NetoExento).HasColumnType("money");
            entity.Property(e => e.NroDocumento)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Numero)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.OtrosTributo).HasColumnType("money");
            entity.Property(e => e.Percepciones).HasColumnType("money");
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TipoComprobante)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TotalGeneral).HasColumnType("money");
            entity.Property(e => e.TotalIva)
                .HasColumnType("money")
                .HasColumnName("TotalIVA");
            entity.Property(e => e.TotalNeto).HasColumnType("money");
        });

        modelBuilder.Entity<ListasPrecio>(entity =>
        {
            entity.HasKey(e => e.IdListaPrecio);

            entity.Property(e => e.IdListaPrecio).HasColumnName("ID_ListaPrecio");
            entity.Property(e => e.Alicuota).HasColumnType("money");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Detalle)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
        });

        modelBuilder.Entity<Localidades>(entity =>
        {
            entity.HasKey(e => e.IdLocalidad);

            entity.Property(e => e.IdLocalidad).HasColumnName("ID_Localidad");
            entity.Property(e => e.Cp)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CP");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdProvincia).HasColumnName("Id_Provincia");

            entity.HasOne(d => d.EstadoNavigation).WithMany(p => p.Localidades)
                .HasForeignKey(d => d.Estado)
                .HasConstraintName("FK_Localidades_Estados");
        });

        modelBuilder.Entity<Marcas>(entity =>
        {
            entity.HasKey(e => e.IdMarca);

            entity.Property(e => e.IdMarca).HasColumnName("ID_Marca");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
        });

        modelBuilder.Entity<MarcasActualizacion>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.Alicuota).HasColumnType("money");
            entity.Property(e => e.FechaActualizacion).HasColumnType("datetime");
            entity.Property(e => e.IdMarca).HasColumnName("ID_Marca");
            entity.Property(e => e.IdMarcaActualizacion)
                .ValueGeneratedOnAdd()
                .HasColumnName("ID_MarcaActualizacion");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
        });

        modelBuilder.Entity<Modelos>(entity =>
        {
            entity.HasKey(e => e.IdModelo);

            entity.Property(e => e.IdModelo).HasColumnName("ID_Modelo");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdMarca).HasColumnName("ID_Marca");
        });

        modelBuilder.Entity<ModelosActualizacion>(entity =>
        {
            entity.HasKey(e => e.IdModeloActualizacion);

            entity.Property(e => e.IdModeloActualizacion).HasColumnName("ID_ModeloActualizacion");
            entity.Property(e => e.Alicuota).HasColumnType("money");
            entity.Property(e => e.FechaActualizacion).HasColumnType("datetime");
            entity.Property(e => e.IdModelo).HasColumnName("ID_Modelo");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
        });

        modelBuilder.Entity<Motivos>(entity =>
        {
            entity.HasKey(e => e.IdMotivo);

            entity.Property(e => e.IdMotivo).HasColumnName("ID_Motivo");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(60)
                .IsUnicode(false);
            entity.Property(e => e.IdEmpresa)
                .HasMaxLength(10)
                .IsFixedLength()
                .HasColumnName("ID_Empresa");
        });

        modelBuilder.Entity<Ofertas>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.CantidadStock).HasColumnType("money");
            entity.Property(e => e.Descuento).HasColumnType("money");
            entity.Property(e => e.FechaFin).HasColumnType("datetime");
            entity.Property(e => e.FechaInicio).HasColumnType("datetime");
            entity.Property(e => e.IdEstado).HasColumnName("ID_Estado");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.IdOferta).HasColumnName("ID_Oferta");
            entity.Property(e => e.Nombre).HasMaxLength(100);
            entity.Property(e => e.PrecioConDescuento).HasColumnType("money");
        });

        modelBuilder.Entity<OfertasAgotamiento>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.IdItem).HasColumnName("ID_Item");
            entity.Property(e => e.IdOferta).HasColumnName("ID_Oferta");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
        });

        modelBuilder.Entity<OfertasSucursales>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.IdOferta).HasColumnName("ID_Oferta");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
        });

        modelBuilder.Entity<OrdenesDepositos>(entity =>
        {
            entity.HasKey(e => e.IdOrdenDepostio);

            entity.Property(e => e.IdOrdenDepostio).HasColumnName("ID_OrdenDepostio");
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.IdBancoCuenta).HasColumnName("ID_BancoCuenta");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdPlanillaCaja).HasColumnName("ID_PlanillaCaja");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.Letra)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Numero)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Observaciones)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<OrdenesDepositosDetalle>(entity =>
        {
            entity.HasKey(e => e.IdOrdenDepositoDetalle);

            entity.Property(e => e.IdOrdenDepositoDetalle).HasColumnName("ID_OrdenDepositoDetalle");
            entity.Property(e => e.Banco)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Detalle)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Emision).HasColumnType("datetime");
            entity.Property(e => e.IdBanco).HasColumnName("ID_Banco");
            entity.Property(e => e.IdElemento).HasColumnName("ID_Elemento");
            entity.Property(e => e.IdElementoCobroPago).HasColumnName("ID_ElementoCobroPago");
            entity.Property(e => e.IdOrdendeposito).HasColumnName("ID_Ordendeposito");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.Nro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Recepcion).HasColumnType("datetime");
            entity.Property(e => e.Sucursal)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
            entity.Property(e => e.Vto).HasColumnType("datetime");
        });

        modelBuilder.Entity<OrdenesExrtacciones>(entity =>
        {
            entity.HasKey(e => e.IdOrdenExtraccion).HasName("PK_OrdenesExrtaccion");

            entity.Property(e => e.IdOrdenExtraccion).HasColumnName("ID_OrdenExtraccion");
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.IdBancoCuenta).HasColumnName("ID_BancoCuenta");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdPlanillaCaja).HasColumnName("ID_PlanillaCaja");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.Letra)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Numero)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Observaciones)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<OrdenesExtraccionesDetalle>(entity =>
        {
            entity.HasKey(e => e.IdOrdenExtraccionDetalle).HasName("PK_OrdenExtraccionsDetalle");

            entity.Property(e => e.IdOrdenExtraccionDetalle).HasColumnName("ID_OrdenExtraccionDetalle");
            entity.Property(e => e.Banco)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Detalle)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Emision).HasColumnType("datetime");
            entity.Property(e => e.IdBanco).HasColumnName("ID_Banco");
            entity.Property(e => e.IdElemento).HasColumnName("ID_Elemento");
            entity.Property(e => e.IdElementoCobroPago).HasColumnName("ID_ElementoCobroPago");
            entity.Property(e => e.IdOrdenExtraccion).HasColumnName("ID_OrdenExtraccion");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.Nro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Recepcion).HasColumnType("datetime");
            entity.Property(e => e.Sucursal)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
            entity.Property(e => e.Vto).HasColumnType("datetime");
        });

        modelBuilder.Entity<OtrosTributos>(entity =>
        {
            entity.HasKey(e => e.IdOtrosTributos);

            entity.Property(e => e.IdOtrosTributos).HasColumnName("ID_OtrosTributos");
            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Observaciones)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Paises>(entity =>
        {
            entity.HasKey(e => e.IdPais);

            entity.Property(e => e.IdPais).HasColumnName("ID_Pais");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.EstadoNavigation).WithMany(p => p.Paises)
                .HasForeignKey(d => d.Estado)
                .HasConstraintName("FK_Paises_Estados");
        });

        modelBuilder.Entity<ParametroJson>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Parametr__3214EC07CC042C0D");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Json)
                .IsUnicode(false)
                .HasColumnName("json");
        });

        modelBuilder.Entity<Parametros>(entity =>
        {
            entity.HasKey(e => e.IdParametro);

            entity.Property(e => e.IdParametro).HasColumnName("ID_Parametro");
            entity.Property(e => e.Categoria)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.Nombre)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Valor)
                .HasMaxLength(500)
                .IsUnicode(false);
        });

        modelBuilder.Entity<ProductosCombos>(entity =>
        {
            entity.HasKey(e => e.IdComboProducto).HasName("PK__Producto__134759604B3781C2");

            entity.Property(e => e.IdComboProducto)
                .ValueGeneratedNever()
                .HasColumnName("ID_ComboProducto");
            entity.Property(e => e.IdCombo).HasColumnName("ID_Combo");
            entity.Property(e => e.IdItem).HasColumnName("ID_Item");

            entity.HasOne(d => d.IdComboNavigation).WithMany(p => p.ProductosCombos)
                .HasForeignKey(d => d.IdCombo)
                .HasConstraintName("FK__Productos__ID_Co__2CE88C3E");

            entity.HasOne(d => d.IdItemNavigation).WithMany(p => p.ProductosCombos)
                .HasForeignKey(d => d.IdItem)
                .HasConstraintName("FK__Productos__ID_It__2DDCB077");
        });

        modelBuilder.Entity<ProveedoresCheques>(entity =>
        {
            entity.HasKey(e => e.IdProveedorCheque);

            entity.Property(e => e.IdProveedorCheque).HasColumnName("ID_ProveedorCheque");
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.FechaRecepcion).HasColumnType("datetime");
            entity.Property(e => e.FechaVencimiento).HasColumnType("datetime");
            entity.Property(e => e.IdBanco).HasColumnName("ID_Banco");
            entity.Property(e => e.IdClienteRecibo).HasColumnName("ID_ClienteRecibo");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdProveedor).HasColumnName("ID_Proveedor");
            entity.Property(e => e.IdProveedorRecibo).HasColumnName("ID_ProveedorRecibo");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.Importe).HasColumnType("money");
            entity.Property(e => e.Nro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ValorToma).HasColumnType("money");
        });

        modelBuilder.Entity<ProveedoresRecibos>(entity =>
        {
            entity.HasKey(e => e.IdProveedorRecibo);

            entity.Property(e => e.IdProveedorRecibo).HasColumnName("ID_ProveedorRecibo");
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.IdCategoriaIva).HasColumnName("ID_CategoriaIVA");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdOrdenPagoTipo).HasColumnName("ID_OrdenPagoTipo");
            entity.Property(e => e.IdPlanillaCaja).HasColumnName("ID_PlanillaCaja");
            entity.Property(e => e.IdProveedor).HasColumnName("ID_Proveedor");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.Letra)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NroDoc)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Numero)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Observaciones)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<ProveedoresRecibosDetalle>(entity =>
        {
            entity.HasKey(e => e.IdProveedorReciboDetalle);

            entity.Property(e => e.IdProveedorReciboDetalle).HasColumnName("ID_ProveedorReciboDetalle");
            entity.Property(e => e.Banco)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Detalle)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Emision).HasColumnType("datetime");
            entity.Property(e => e.IdBanco).HasColumnName("ID_Banco");
            entity.Property(e => e.IdElemento).HasColumnName("ID_Elemento");
            entity.Property(e => e.IdElementoCobroPago).HasColumnName("ID_ElementoCobroPago");
            entity.Property(e => e.IdProveedorRecibo).HasColumnName("ID_ProveedorRecibo");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.Nro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Recepcion).HasColumnType("datetime");
            entity.Property(e => e.Sucursal)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
            entity.Property(e => e.Vto).HasColumnType("datetime");
        });

        modelBuilder.Entity<Provincias>(entity =>
        {
            entity.HasKey(e => e.IdProvincia);

            entity.Property(e => e.IdProvincia).HasColumnName("ID_Provincia");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IdPais).HasColumnName("ID_Pais");

            entity.HasOne(d => d.EstadoNavigation).WithMany(p => p.Provincias)
                .HasForeignKey(d => d.Estado)
                .HasConstraintName("FK_Provincias_Estados");
        });

        modelBuilder.Entity<PuntosVenta>(entity =>
        {
            entity.HasKey(e => e.IdPuntoVenta);

            entity.Property(e => e.IdPuntoVenta).HasColumnName("ID_PuntoVenta");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.Letra)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Retenciones>(entity =>
        {
            entity.HasKey(e => e.IdRetencion);

            entity.Property(e => e.IdRetencion).HasColumnName("ID_Retencion");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FechaEmision).HasColumnType("datetime");
            entity.Property(e => e.FechaRecepcion).HasColumnType("datetime");
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.IdEntidad).HasColumnName("ID_Entidad");
            entity.Property(e => e.IdRetencionTipo).HasColumnName("ID_RetencionTipo");
            entity.Property(e => e.NroComprobante)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Total).HasColumnType("money");
        });

        modelBuilder.Entity<Rubros>(entity =>
        {
            entity.HasKey(e => e.IdRubro);

            entity.Property(e => e.IdRubro).HasColumnName("ID_Rubro");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
        });

        modelBuilder.Entity<RubrosActualizacion>(entity =>
        {
            entity.HasKey(e => e.IdRubroActualizacion);

            entity.Property(e => e.IdRubroActualizacion).HasColumnName("ID_RubroActualizacion");
            entity.Property(e => e.Alicuota).HasColumnType("money");
            entity.Property(e => e.FechaActualizacion).HasColumnType("datetime");
            entity.Property(e => e.IdRubro).HasColumnName("ID_Rubro");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
        });

        modelBuilder.Entity<SubRubros>(entity =>
        {
            entity.HasKey(e => e.IdSubRubro);

            entity.Property(e => e.IdSubRubro).HasColumnName("ID_SubRubro");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdRubro).HasColumnName("ID_Rubro");
        });

        modelBuilder.Entity<SubRubrosActualizacion>(entity =>
        {
            entity.HasKey(e => e.IdSubRubroActualizacion);

            entity.Property(e => e.IdSubRubroActualizacion).HasColumnName("ID_SubRubroActualizacion");
            entity.Property(e => e.Alicuota).HasColumnType("money");
            entity.Property(e => e.FechaActualizacion).HasColumnType("datetime");
            entity.Property(e => e.IdSubRubro).HasColumnName("ID_SubRubro");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
        });

        modelBuilder.Entity<Sucursales>(entity =>
        {
            entity.HasKey(e => e.IdSucursal);

            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.Cuit)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Direccion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Email)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Fantasia)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FechaAlta).HasColumnType("datetime");
            entity.Property(e => e.GeoLatitud)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.GeoLongitud)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IdCategoriaIva).HasColumnName("ID_CategoriaIVA");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdLocalidad).HasColumnName("ID_Localidad");
            entity.Property(e => e.IdProvincia).HasColumnName("ID_Provincia");
            entity.Property(e => e.Iibb)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("IIBB");
            entity.Property(e => e.Logo)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Nro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PuntoVentaAfip)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("PuntoVentaAFIP");
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Telefono)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Tarjetas>(entity =>
        {
            entity.HasKey(e => e.IdTarjeta);

            entity.Property(e => e.IdTarjeta).HasColumnName("ID_Tarjeta");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IdEstado).HasColumnName("ID_Estado");
        });

        modelBuilder.Entity<Transportes>(entity =>
        {
            entity.HasKey(e => e.IdTransporte);

            entity.Property(e => e.IdTransporte).HasColumnName("ID_Transporte");
            entity.Property(e => e.Calle)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdCategoriaIva).HasColumnName("ID_CategoriaIVA");
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdLocalidad).HasColumnName("ID_Localidad");
            entity.Property(e => e.IdProvincia).HasColumnName("ID_Provincia");
            entity.Property(e => e.Mail)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Nro)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NroDoc)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Telefono)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TxtComprasAlicuotas>(entity =>
        {
            entity.HasKey(e => e.TxtCompraAlicuota);

            entity.Property(e => e.Alicuota)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.CodVendedor)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.CuitVendedor)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.ImporteLiquidado).HasColumnType("money");
            entity.Property(e => e.NetoGravado).HasColumnType("money");
            entity.Property(e => e.NroComprobante)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.TipoComprobante)
                .HasMaxLength(3)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TxtVentasAlicuotas>(entity =>
        {
            entity.HasKey(e => e.TxtVentaAlicuota);

            entity.Property(e => e.Alicuota)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.IdComprobante).HasColumnName("ID_Comprobante");
            entity.Property(e => e.IdComprobanteTipo).HasColumnName("ID_ComprobanteTipo");
            entity.Property(e => e.ImporteLiquidado).HasColumnType("money");
            entity.Property(e => e.ImporteNeto).HasColumnType("money");
            entity.Property(e => e.NroComprobante)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PuntoVenta)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.TipoComprobante)
                .HasMaxLength(3)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Ubicaciones>(entity =>
        {
            entity.HasKey(e => e.IdUbicacion);

            entity.Property(e => e.IdUbicacion).HasColumnName("ID_Ubicacion");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
        });

        modelBuilder.Entity<Unidades>(entity =>
        {
            entity.HasKey(e => e.IdUnidad);

            entity.Property(e => e.IdUnidad).HasColumnName("ID_Unidad");
            entity.Property(e => e.Dominio)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdEmpresa).HasColumnName("ID_Empresa");
            entity.Property(e => e.IdTransporte).HasColumnName("ID_Transporte");
        });

        modelBuilder.Entity<Usuarios>(entity =>
        {
            entity.HasKey(e => e.IdUsuario);

            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
            entity.Property(e => e.Email)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IdEstado).HasColumnName("ID_Estado");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.Imagen)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Nombre)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Pass)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.Token).HasColumnType("text");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Usuario)
                .HasMaxLength(500)
                .IsUnicode(false);
        });

        modelBuilder.Entity<UsuariosSucursales>(entity =>
        {
            entity.HasKey(e => e.IdUsuarioSucursal);

            entity.Property(e => e.IdUsuarioSucursal).HasColumnName("ID_UsuarioSucursal");
            entity.Property(e => e.IdSucursal).HasColumnName("ID_Sucursal");
            entity.Property(e => e.IdUsuario).HasColumnName("ID_Usuario");
        });

        modelBuilder.Entity<Vehiculos>(entity =>
        {
            entity.HasKey(e => e.IdVehiculo);

            entity.Property(e => e.IdVehiculo).HasColumnName("ID_Vehiculo");
            entity.Property(e => e.Dominio)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FechaAlta).HasColumnType("datetime");
            entity.Property(e => e.FechaBaja).HasColumnType("datetime");
            entity.Property(e => e.FechaRuta).HasColumnType("datetime");
            entity.Property(e => e.IdCentro).HasColumnName("ID_Centro");
            entity.Property(e => e.IdSujeto).HasColumnName("ID_Sujeto");
            entity.Property(e => e.IdTransporte).HasColumnName("ID_Transporte");
            entity.Property(e => e.IdVehiculoPadre).HasColumnName("ID_VehiculoPadre");
            entity.Property(e => e.IdVehiculoTipo).HasColumnName("ID_VehiculoTipo");
            entity.Property(e => e.InspTecnica).HasColumnType("datetime");
            entity.Property(e => e.Observaciones)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.RespCivil).HasColumnType("datetime");
            entity.Property(e => e.SeguroCarga).HasColumnType("datetime");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
