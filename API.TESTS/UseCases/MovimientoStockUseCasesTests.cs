using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Models.Stock;
using API.SERVICE.UseCases.Stock;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Moq;

namespace API.TESTS.UseCases;

/// <summary>Transferencias de stock entre sucursales: envío → en tránsito → recepción / rechazo / anulación.</summary>
public class MovimientoStockUseCasesTests
{
    private const int Ms = 17, Local = 1, Sucursal = 2, Arroz = 10, Fideos = 11, Servicio = 12, IdUsuario = 3, IdMovimiento = 450;
    private static readonly DateTime Ahora = new(2026, 10, 8, 10, 0, 0);
    private static readonly DateTime Fecha = new(2026, 10, 8);

    private readonly FakeMovimientoStockRepository _repo = new();
    private readonly FakeReciboCobroRepository _comprobantes = new();
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    public MovimientoStockUseCasesTests()
    {
        _ref.Parametros[("COMPROBANTE", "MS")] = Ms.ToString();
        _ref.Parametros[("NUMERACION", "MS")] = "0";
        _repo.Sucursales[Local] = new Sucursales { IdSucursal = Local, Descripcion = "LOCAL", PuntoVenta = "0003" };
        _repo.Sucursales[Sucursal] = new Sucursales { IdSucursal = Sucursal, Descripcion = "SUCURSAL ", PuntoVenta = "0002" };
        _repo.Items[Arroz] = ("arroz", true);
        _repo.Items[Fideos] = ("fideos", true);
        _repo.Items[Servicio] = ("flete", false);
        _repo.Stock[(Arroz, Local)] = 10;
        _repo.Stock[(Arroz, Sucursal)] = 1;
        _repo.Stock[(Fideos, Local)] = 5;
        _repo.Stock[(Fideos, Sucursal)] = 0;
        _repo.Stock[(Servicio, Local)] = 0;
        _repo.Stock[(Servicio, Sucursal)] = 0;
        Usuario(sucursal: Local);
    }

    [Fact]
    public async Task Iniciar_UsaElPuntoDeVentaDeLaSucursalDelUsuario()
    {
        var r = await new IniciarMovimientoStockUseCase(_repo, _comprobantes, _ref, _user.Object).ExecuteAsync(null);

        r.Should().Be(new NuevoMovimientoStockDisplay(Local, "0003", "M", false, "00000042"));
    }

    [Fact]
    public async Task Enviar_DescuentaDelOrigenYQuedaEnTransito()
    {
        var r = await CreateSut().ExecuteAsync(Dto((Arroz, 4), (Fideos, 2), (Arroz, 1)));

        var doc = _repo.Single<DocumentosCliente>();
        doc.Should().Match<DocumentosCliente>(d => d.IdComprobanteTipo == Ms && d.Letra == "M" && d.PuntoVenta == "0003" && d.Numero == "00000042"
            && d.IdSucursal == Local && d.IdCliente == Sucursal && d.RazonSocial == "SUCURSAL" && d.IdUsuario == IdUsuario
            && d.IdPlanillaCaja == 0 && d.Estado == _ref.Estado("DOCUMENTOSCLIENTE", "GENERADO"));
        r.Items.Should().BeEquivalentTo([new MovimientoStockItemDisplay(Arroz, "ARROZ", 5), new MovimientoStockItemDisplay(Fideos, "FIDEOS", 2)],
            "las líneas repetidas del mismo ítem se agrupan");

        _repo.Stock[(Arroz, Local)].Should().Be(5);
        _repo.Stock[(Fideos, Local)].Should().Be(3);
        _repo.Stock[(Arroz, Sucursal)].Should().Be(1, "el destino recién suma al recibir");
        _repo.All<ItemsMovimientosDetalles>().Should().HaveCount(2).And.OnlyContain(m => m.IdSucursal == Local && m.Debe == 0 && m.Haber == m.Total
            && m.Total2 == -m.Total && m.Concepto == "M-0003-00000042" && m.IdComprobanteTipo == Ms);
        _repo.Operaciones.Should().StartWith($"Lock sucursal {Local}");
        _comprobantes.Operaciones.Should().Contain("Numerar 0003");
    }

    [Fact]
    public async Task Enviar_StockInsuficiente_400_SinGrabar()
    {
        var act = () => CreateSut().ExecuteAsync(Dto((Arroz, 11)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*Stock insuficiente de arroz*hay 10*enviar 11*");
        _repo.Added.Should().BeEmpty();
        _comprobantes.Operaciones.Should().NotContain(o => o.StartsWith("Numerar"));
    }

    [Fact]
    public async Task Enviar_ItemQueNoMueveStock_400()
    {
        var act = () => CreateSut().ExecuteAsync(Dto((Servicio, 1)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no mueve stock*");
    }

    [Fact]
    public async Task Enviar_ItemSinFilaEnDestino_400()
    {
        _repo.Stock.Remove((Fideos, Sucursal));

        var act = () => CreateSut().ExecuteAsync(Dto((Fideos, 1)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no está habilitado en las dos sucursales*");
    }

    [Fact]
    public async Task Enviar_MismaSucursal_400()
    {
        var act = () => CreateSut().ExecuteAsync(new CreateMovimientoStockDto { IdSucursalDestino = Local, FechaEmision = Fecha, Items = [Item(Arroz, 1)] });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*distinta*");
    }

    [Fact]
    public async Task Enviar_DesdeSucursalQueNoOpera_403()
    {
        var act = () => CreateSut().ExecuteAsync(new CreateMovimientoStockDto
        {
            IdSucursalOrigen = Sucursal, IdSucursalDestino = Local, FechaEmision = Fecha, Items = [Item(Arroz, 1)],
        });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Enviar_DesdeSucursalAsignadaEnUsuariosSucursales_Permitido()
    {
        _repo.UsuariosSucursales.Add((IdUsuario, Sucursal));

        await CreateSut().ExecuteAsync(new CreateMovimientoStockDto
        {
            IdSucursalOrigen = Sucursal, IdSucursalDestino = Local, FechaEmision = Fecha, Items = [Item(Arroz, 1)],
        });

        _repo.Stock[(Arroz, Sucursal)].Should().Be(0);
    }

    [Fact]
    public async Task Recibir_SumaAlDestino()
    {
        EnTransito();
        Usuario(sucursal: Sucursal);

        var r = await ResolverSut().RecibirAsync(IdMovimiento);

        r.Estado.Should().Be(_ref.Estado("DOCUMENTOSCLIENTE", "CONFIRMADO"));
        _repo.Stock[(Arroz, Sucursal)].Should().Be(5);
        _repo.Stock[(Arroz, Local)].Should().Be(10, "ya se había descontado al enviar");
        _repo.Single<ItemsMovimientosDetalles>().Should().Match<ItemsMovimientosDetalles>(m => m.IdSucursal == Sucursal && m.Debe == 4 && m.Total2 == 4);
        _repo.Operaciones.Should().NotContain(o => o.StartsWith("AnularDetalles"));
    }

    [Fact]
    public async Task Recibir_DesdeLaSucursalDeOrigen_403()
    {
        EnTransito();

        var act = () => ResolverSut().RecibirAsync(IdMovimiento);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Rechazar_VuelveAlOrigen()
    {
        EnTransito();
        Usuario(sucursal: Sucursal);

        var r = await ResolverSut().RechazarAsync(IdMovimiento);

        r.Estado.Should().Be(_ref.Estado("DOCUMENTOSCLIENTE", "RECHAZADO"));
        _repo.Stock[(Arroz, Local)].Should().Be(14);
        _repo.Stock[(Arroz, Sucursal)].Should().Be(1);
    }

    [Fact]
    public async Task Anular_VuelveAlOrigenYAnulaElDetalle()
    {
        EnTransito();

        var r = await ResolverSut().AnularAsync(IdMovimiento);

        r.Estado.Should().Be(_ref.Estado("DOCUMENTOSCLIENTE", "ANULADO"));
        _repo.Stock[(Arroz, Local)].Should().Be(14);
        _repo.Operaciones.Should().Contain($"AnularDetalles {IdMovimiento}");
    }

    [Fact]
    public async Task Anular_YaRecibido_409_SinMoverStock()
    {
        EnTransito(estado: "CONFIRMADO");

        var act = () => ResolverSut().AnularAsync(IdMovimiento);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*ya no está en tránsito*");
        _repo.Operaciones.Should().NotContain(o => o.StartsWith("Stock"));
    }

    [Fact]
    public async Task Anular_OtroTipoDeComprobante_404()
    {
        EnTransito();
        _repo.Documentos[IdMovimiento].IdComprobanteTipo = 11;

        var act = () => ResolverSut().AnularAsync(IdMovimiento);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task EnTransito_ListaLoPendienteDeLaSucursalDelUsuario()
    {
        EnTransito();
        Usuario(sucursal: Sucursal);

        var r = await new GetMovimientosStockEnTransitoUseCase(_repo, _ref, _user.Object).ExecuteAsync(null);

        r.Should().ContainSingle().Which.Should().Match<MovimientoStockDisplay>(m => m.IdMovimientoStock == IdMovimiento && m.IdSucursalOrigen == Local
            && m.Items.Count == 1 && m.Items[0].Cantidad == 4);
    }

    // ---------- helpers ----------

    private void Usuario(int sucursal, params string[] roles)
    {
        _user.Reset();
        _user.SetupGet(u => u.IdUsuario).Returns(IdUsuario);
        _user.SetupGet(u => u.IdSucursal).Returns(sucursal);
        _user.Setup(u => u.IsInRole(It.IsAny<string>())).Returns<string>(r => roles.Contains(r));
    }

    private void EnTransito(string estado = "GENERADO")
    {
        _repo.Documentos[IdMovimiento] = new DocumentosCliente
        {
            IdDocumentoCliente = IdMovimiento, IdComprobanteTipo = Ms, Letra = "M", PuntoVenta = "0003", Numero = "00000007",
            IdSucursal = Local, IdCliente = Sucursal, RazonSocial = "SUCURSAL", Estado = _ref.Estado("DOCUMENTOSCLIENTE", estado),
        };
        _repo.Detalles.Add(new DocumentosClienteDetalle { IdDocumentoClienteDetalle = 451, IdDocumentoCliente = IdMovimiento, IdItem = Arroz, Descripcion = "ARROZ", Cantidad = 4 });
    }

    private CreateMovimientoStockUseCase CreateSut() =>
        new(_repo, _comprobantes, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private ResolverMovimientoStockUseCase ResolverSut() =>
        new(_repo, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private static CreateMovimientoStockDto Dto(params (int IdItem, decimal Cantidad)[] items) => new()
    {
        IdSucursalDestino = Sucursal,
        FechaEmision = Fecha,
        Items = [.. items.Select(i => Item(i.IdItem, i.Cantidad))],
    };

    private static MovimientoStockItemDto Item(int idItem, decimal cantidad) => new() { IdItem = idItem, Cantidad = cantidad };
}
