using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Items;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Models.Items;
using API.SERVICE.UseCases.Items;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace API.TESTS.UseCases;

/// <summary>ABM de ítems: paridad con Items_Agregar_Ws / Items_Modificar_Ws de FrmItemsABM.</summary>
public class ItemAbmUseCasesTests
{
    private static readonly DateTime Ahora = new(2026, 10, 6, 10, 30, 0);

    private readonly Mock<IItemRepository> _repository = new();
    private readonly Mock<IParametroRepository> _parametros = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IServerClock> _clock = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private readonly List<ItemsSucursales> _sucursalesAgregadas = [];
    private readonly List<ItemsPreciosActualizacion> _actualizaciones = [];

    public ItemAbmUseCasesTests()
    {
        // La transacción ejecuta el delegado tal cual.
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task<Items>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<Items>>, CancellationToken>((op, ct) => op(ct));
        _clock.Setup(c => c.GetNowAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Ahora);
        _currentUser.SetupGet(u => u.IdUsuario).Returns(3);

        _repository.Setup(r => r.AddAsync(It.IsAny<Items>(), It.IsAny<CancellationToken>()))
            .Callback<Items, CancellationToken>((i, _) => i.IdItem = 500);
        _repository.Setup(r => r.AddSucursales(It.IsAny<IEnumerable<ItemsSucursales>>()))
            .Callback<IEnumerable<ItemsSucursales>>(s => _sucursalesAgregadas.AddRange(s));
        _repository.Setup(r => r.AddActualizacionPrecio(It.IsAny<ItemsPreciosActualizacion>()))
            .Callback<ItemsPreciosActualizacion>(_actualizaciones.Add);
    }

    // ---------- Alta ----------

    [Fact]
    public async Task Create_ValidDto_GrabaItemConLosValoresDelErp()
    {
        Items? grabado = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<Items>(), It.IsAny<CancellationToken>()))
            .Callback<Items, CancellationToken>((i, _) => { i.IdItem = 500; grabado = i; });

        var result = await CreateSut().ExecuteAsync(AltaDto());

        result.IdItem.Should().Be(500);
        grabado!.Descripcion.Should().Be("YERBA MATE 1KG");
        grabado.Neto.Should().Be(800m, "el ERP guarda el costo en Items.Neto");
        grabado.IdEmpresa.Should().Be(1);
        grabado.StockInicial.Should().Be(10m);
        grabado.FechaCompra.Should().Be(Ahora);
        grabado.CuentaDebe.Should().Be(1);
        _repository.Verify(r => r.ReplaceImpuestoAsync(500, 2, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ValidDto_CreaUnaFilaPorSucursalConAlicuotaYEstadoPropio()
    {
        await CreateSut().ExecuteAsync(AltaDto());

        _sucursalesAgregadas.Should().HaveCount(2);
        _sucursalesAgregadas.Should().AllSatisfy(s =>
        {
            s.IdItem.Should().Be(500);
            s.Alicuota.Should().Be(10.5m);
            s.PrecioVenta.Should().Be(1500m);
            s.Neto.Should().Be(1000m);
            s.Costo.Should().Be(800m);
            s.FechaCompra.Should().Be(DateOnly.FromDateTime(Ahora));
        });
        _sucursalesAgregadas.Single(s => s.IdSucursal == 2).Should().Match<ItemsSucursales>(s => s.Stock == 4m && s.StockInicial == 4m && s.Estado == 0);
    }

    [Fact]
    public async Task Create_CodigoDeBalanza_GuardaLosPrimeros7Digitos()
    {
        var dto = AltaDto();
        dto.CodigoBarras = "2001234005678";

        await CreateSut().ExecuteAsync(dto);

        _sucursalesAgregadas.Should().AllSatisfy(s => s.Barcode.Should().Be("2001234"));
    }

    [Fact]
    public async Task Create_ImpuestoDesconocido_RechazaSinTocarLaBase()
    {
        var dto = AltaDto();
        dto.IdImpuesto = 9;

        var act = () => CreateSut().ExecuteAsync(dto);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("Impuesto 9 inválido*");
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_SucursalRepetida_Rechaza()
    {
        var dto = AltaDto();
        dto.Sucursales.Add(new ItemSucursalAltaDto { IdSucursal = 1, Stock = 1, Estado = 1 });

        var act = () => CreateSut().ExecuteAsync(dto);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("Sucursales repetidas: 1.");
    }

    // ---------- Modificación ----------

    [Fact]
    public async Task Update_PrimeraModificacion_RegistraHistorialAunqueElPrecioNoCambie()
    {
        SetupItemExistente(precioVentaActual: 1500m, ultimaActualizacion: null);

        await UpdateSut().ExecuteAsync(500, ModificacionDto(precio: 1500m));

        _actualizaciones.Should().ContainSingle().Which.Should().Match<ItemsPreciosActualizacion>(a =>
            a.PrecioActual == 1500m && a.PrecioNuevo == 1500m && a.IdUsario == 3 && a.FechaActualizacion == Ahora);
    }

    [Fact]
    public async Task Update_PrecioIgualA2Decimales_NoRegistraHistorial()
    {
        SetupItemExistente(precioVentaActual: 1500.004m, ultimaActualizacion: new ItemsPreciosActualizacion());

        await UpdateSut().ExecuteAsync(500, ModificacionDto(precio: 1500m));

        _actualizaciones.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_PrecioDistinto_RegistraHistorialConPrecioAnteriorDeLaPrimeraSucursal()
    {
        SetupItemExistente(precioVentaActual: 1500m, ultimaActualizacion: new ItemsPreciosActualizacion());

        await UpdateSut().ExecuteAsync(500, ModificacionDto(precio: 1650m));

        _actualizaciones.Should().ContainSingle().Which.Should().Match<ItemsPreciosActualizacion>(a => a.PrecioActual == 1500m && a.PrecioNuevo == 1650m);
    }

    [Fact]
    public async Task Update_SinParametroCambiaStock_NoTocaStock()
    {
        var (item, filas) = SetupItemExistente(precioVentaActual: 1500m, ultimaActualizacion: new ItemsPreciosActualizacion());

        await UpdateSut().ExecuteAsync(500, ModificacionDto(precio: 1500m));

        item.StockActual.Should().Be(10m);
        filas.Should().AllSatisfy(f => f.Stock.Should().Be(5m));
        item.Descripcion.Should().Be("YERBA MATE 1KG");
        filas.Should().AllSatisfy(f => f.PrecioVenta.Should().Be(1500m));
    }

    [Fact]
    public async Task Update_ConCambiaStockEn1_PisaStockDelItemYDeCadaSucursal()
    {
        var (item, filas) = SetupItemExistente(precioVentaActual: 1500m, ultimaActualizacion: new ItemsPreciosActualizacion());
        _parametros.Setup(p => p.GetValorAsync("CAMBIASTOCK", "CAMBIASTOCK", It.IsAny<CancellationToken>())).ReturnsAsync(" 1 ");

        await UpdateSut().ExecuteAsync(500, ModificacionDto(precio: 1500m));

        item.StockActual.Should().Be(30m);
        filas.Single(f => f.IdSucursal == 1).Stock.Should().Be(20m);
        filas.Single(f => f.IdSucursal == 2).Stock.Should().Be(10m);
    }

    [Fact]
    public async Task Update_SucursalSinFila_SeIgnoraComoEnElErp()
    {
        var (_, filas) = SetupItemExistente(precioVentaActual: 1500m, ultimaActualizacion: new ItemsPreciosActualizacion());
        var dto = ModificacionDto(precio: 1500m);
        dto.Sucursales.Add(new ItemSucursalStockDto { IdSucursal = 99, Stock = 1 });

        await UpdateSut().ExecuteAsync(500, dto);

        filas.Should().HaveCount(2);
        _repository.Verify(r => r.AddSucursales(It.IsAny<IEnumerable<ItemsSucursales>>()), Times.Never);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_UsuarioSinFilaEnUsuarios_Forbidden()
    {
        _currentUser.SetupGet(u => u.IdUsuario).Returns((int?)null);

        var act = () => UpdateSut().ExecuteAsync(500, ModificacionDto(precio: 1500m));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Update_ItemInexistente_NotFound()
    {
        var act = () => UpdateSut().ExecuteAsync(404, ModificacionDto(precio: 1500m));

        await act.Should().ThrowAsync<NotFoundException>();
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ItemSinSucursales_Rechaza()
    {
        _repository.Setup(r => r.GetByIdAsync(500, It.IsAny<CancellationToken>())).ReturnsAsync(new Items { IdItem = 500 });
        _repository.Setup(r => r.GetSucursalesAsync(500, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var act = () => UpdateSut().ExecuteAsync(500, ModificacionDto(precio: 1500m));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*ninguna sucursal*");
    }

    // ---------- helpers ----------

    private CreateItemUseCase CreateSut() => new(_repository.Object, _unitOfWork.Object, _clock.Object);

    private UpdateItemUseCase UpdateSut() => new(
        _repository.Object, _parametros.Object, _unitOfWork.Object, _clock.Object, _currentUser.Object, NullLogger<UpdateItemUseCase>.Instance);

    private (Items Item, List<ItemsSucursales> Filas) SetupItemExistente(decimal precioVentaActual, ItemsPreciosActualizacion? ultimaActualizacion)
    {
        var item = new Items { IdItem = 500, Descripcion = "viejo", StockActual = 10m };
        var filas = new List<ItemsSucursales>
        {
            new() { IdItemSucursal = 1, IdItem = 500, IdSucursal = 1, PrecioVenta = precioVentaActual, Stock = 5m },
            new() { IdItemSucursal = 2, IdItem = 500, IdSucursal = 2, PrecioVenta = 999m, Stock = 5m },
        };
        _repository.Setup(r => r.GetByIdAsync(500, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _repository.Setup(r => r.GetSucursalesAsync(500, It.IsAny<CancellationToken>())).ReturnsAsync(filas);
        _repository.Setup(r => r.GetUltimaActualizacionPrecioAsync(500, It.IsAny<CancellationToken>())).ReturnsAsync(ultimaActualizacion);
        return (item, filas);
    }

    private static CreateItemDto AltaDto() => new()
    {
        Descripcion = "Yerba mate 1kg",
        IdItemTipo = 1, IdRubro = 2, IdSubRubro = 3, IdMarca = 4, IdModelo = 5, IdUbicacion = 6, IdUnidadMedida = 7,
        IdImpuesto = 2, IdEstado = 1,
        CodigoFabrica = "YM1", CodigoBarras = "7790000000017",
        MueveStock = true, StockActual = 10m, StockMinimo = 1m, StockMaximo = 50m,
        Costo = 800m, Neto = 1000m, Rentabilidad = 25m, PrecioUnitario = 1500m,
        Sucursales =
        [
            new() { IdSucursal = 1, Stock = 6m, Estado = 1 },
            new() { IdSucursal = 2, Stock = 4m, Estado = 0 },
        ],
    };

    private static UpdateItemDto ModificacionDto(decimal precio) => new()
    {
        Descripcion = "Yerba mate 1kg",
        IdItemTipo = 1, IdRubro = 2, IdSubRubro = 3, IdMarca = 4, IdModelo = 5, IdUbicacion = 6, IdUnidadMedida = 7,
        IdImpuesto = 1, IdEstado = 1,
        MueveStock = true, StockActual = 30m, StockMinimo = 1m, StockMaximo = 50m,
        Costo = 800m, Neto = 1000m, Rentabilidad = 25m, PrecioUnitario = precio,
        Sucursales =
        [
            new() { IdSucursal = 1, Stock = 20m },
            new() { IdSucursal = 2, Stock = 10m },
        ],
    };
}
