using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Ventas;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Moq;
using R = API.TESTS.Infrastructure.FakeReferencias;

namespace API.TESTS.UseCases;

/// <summary>Presupuesto (PV): paridad con Agregar_Ws / Modificar_Ws / Anular_Ws de FrmPresupuestosABM.</summary>
public class PresupuestoUseCasesTests
{
    private const int IdPresupuesto = 381;
    private static readonly DateTime Ahora = new(2026, 10, 6, 17, 45, 30);
    private static readonly DateTime Fecha = new(2026, 10, 6);

    private readonly FakeReciboCobroRepository _recibos = new();
    private readonly FakeVentaRepository _ventas;
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    public PresupuestoUseCasesTests()
    {
        _ventas = new FakeVentaRepository(_recibos) { Letra = "P" };
        _user.SetupGet(u => u.IdUsuario).Returns(3);
    }

    // ---------- Alta ----------

    [Fact]
    public async Task Create_GrabaPendienteDeRemitirYFacturarSinMoverStock()
    {
        var dto = Dto(Item(10, 3, otros: 5), Item(11, 1.5m));
        dto.Observaciones = new string('x', 80);

        var r = await CreateSut().ExecuteAsync(dto);

        var doc = _ventas.Single<DocumentosCliente>();
        doc.Should().Match<DocumentosCliente>(d =>
            d.IdComprobanteTipo == R.Pv && d.Letra == "P" && d.PuntoVenta == "0003" && d.Numero == "00000042" && d.Estado == 42
            && d.Remitar == true && d.Facturar == true && d.Pendiente == true && d.VtoCae == "06/10/2026" && d.Observaciones!.Length == 50);
        r.IdDocumentoCliente.Should().Be(doc.IdDocumentoCliente);
        _ventas.Single<DocumentosClienteObservaciones>().Observaciones.Should().HaveLength(80);
        _ventas.Single<DocumentosClienteVencimientos>().FechaVencimiento.Should().Be(Fecha.AddDays(30));

        _ventas.All<DocumentosClienteDetalle>().Select(d => d.Otros).Should().Equal(5m, 0m);
        _ventas.All<EntidadesCtaCteStockMovimientosDetalle>().Should().HaveCount(2).And.AllSatisfy(s =>
            s.Should().Match<EntidadesCtaCteStockMovimientosDetalle>(x =>
                x.IdComprobanteTipo == R.Pv && x.Saldo == x.Total && x.Saldo2 == 0 && x.Concepto == "PV -P-0003-00000042" && x.IdComprobanteRelacion == 0));
        _ventas.Operaciones.Should().NotContain(o => o.Contains("Stock") && !o.StartsWith("Ajustar"), "un presupuesto no mueve stock");
        _ventas.All<ItemsMovimientosDetalles>().Should().BeEmpty();
        _recibos.Operaciones.Should().Contain("Numerar 0003");
    }

    [Fact]
    public async Task Create_SinLetraParaLaCategoria_Rechaza()
    {
        _ventas.Letra = null;

        var act = () => CreateSut().ExecuteAsync(Dto(Item(10, 1)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*ComprobantesLetras*");
    }

    [Fact]
    public async Task Create_LineaConRelacion_Rechaza()
    {
        var item = Item(10, 1);
        item.Relacion = new ComprobanteRelacionadoDetalleDto { IdDocumentoCliente = 5, IdComprobanteTipo = R.Ven, IdDocumentoClienteDetalle = 1 };

        var act = () => CreateSut().ExecuteAsync(Dto(item));

        await act.Should().ThrowAsync<BusinessException>();
        _ventas.Added.Should().BeEmpty();
    }

    // ---------- Modificación ----------

    [Fact]
    public async Task Update_ReemplazaCabeceraLineasYSaldos()
    {
        Existente();

        await UpdateSut().ExecuteAsync(IdPresupuesto, new UpdatePresupuestoDto { IdEntidad = 5, Total = 700, Items = [Item(12, 7)] });

        _ventas.Operaciones.Should().ContainInOrder(
            $"ModificarPresupuesto {IdPresupuesto} cliente=5 total=700",
            $"BorrarDetalles {IdPresupuesto}",
            $"BorrarMovimientosStock {IdPresupuesto}/{R.Pv}");
        _ventas.All<DocumentosClienteDetalle>().Single().IdItem.Should().Be(12);
        _ventas.Single<EntidadesCtaCteStockMovimientosDetalle>().Should().Match<EntidadesCtaCteStockMovimientosDetalle>(s =>
            s.IdComprobante == IdPresupuesto && s.Total == 7 && s.Saldo == 7 && s.Concepto == "PV -P-0001-00000001" && s.IdSucursal == 1 && s.Fecha == Fecha);
        _recibos.Bloqueos.Should().Contain(5);
    }

    [Fact]
    public async Task Update_CambiaDeCliente_BloqueaAmbos()
    {
        Existente();
        _recibos.Entidades[6] = new Entidades { IdEntidad = 6, RazonSocial = "OTRO", IdCategoriaIva = 1 };

        await UpdateSut().ExecuteAsync(IdPresupuesto, new UpdatePresupuestoDto { IdEntidad = 6, Items = [Item(12, 1)] });

        _recibos.Bloqueos.Should().Contain([5, 6]);
        _ventas.Single<EntidadesCtaCteStockMovimientosDetalle>().IdEntidad.Should().Be(6);
    }

    [Fact]
    public async Task Update_ConAlgoRemitidoOFacturado_409()
    {
        Existente();
        _ventas.SaldosStock[IdPresupuesto] = 1;

        var act = () => UpdateSut().ExecuteAsync(IdPresupuesto, new UpdatePresupuestoDto { IdEntidad = 5, Items = [Item(12, 1)] });

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*remitida o facturada*");
        _ventas.Operaciones.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_NoGenerado_409()
    {
        Existente();
        _ventas.Documentos[IdPresupuesto].Estado = _ref.Estado("DOCUMENTOSCLIENTE", "ENTREGADO PARCIAL");

        var act = () => UpdateSut().ExecuteAsync(IdPresupuesto, new UpdatePresupuestoDto { IdEntidad = 5, Items = [Item(12, 1)] });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_NoEsPresupuesto_400()
    {
        Existente();
        _ventas.Documentos[IdPresupuesto].IdComprobanteTipo = R.Ven;

        var act = () => UpdateSut().ExecuteAsync(IdPresupuesto, new UpdatePresupuestoDto { IdEntidad = 5, Items = [Item(12, 1)] });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no es un presupuesto*");
    }

    // ---------- Anulación ----------

    [Fact]
    public async Task Anular_BajaDeLineasSaldosYComprobante()
    {
        Existente();

        await AnularSut().ExecuteAsync(IdPresupuesto);

        _ventas.Operaciones.Should().ContainInOrder(
            "AnularDetalle 900",
            $"AnularMovimientosStock {IdPresupuesto}/{R.Pv}",
            $"AnularDocumento {IdPresupuesto}={_ref.Estado("DOCUMENTOSCLIENTE", "ANULADO")}");
        _recibos.Bloqueos.Should().Contain(5);
    }

    [Fact]
    public async Task Anular_YaAnulado_409()
    {
        Existente();
        _ventas.Documentos[IdPresupuesto].Estado = _ref.Estado("DOCUMENTOSCLIENTE", "ANULADO");

        var act = () => AnularSut().ExecuteAsync(IdPresupuesto);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Iniciar_LetraPYNumeroSugerido()
    {
        var r = await new IniciarPresupuestoUseCase(_ventas, _recibos, _ref, _user.Object).ExecuteAsync(5);

        r.Should().Be(new NuevaVentaInternaDisplay(77, "0003", "P", false, "00000042"));
    }

    // ---------- helpers ----------

    private CreatePresupuestoUseCase CreateSut() => new(_ventas, _recibos, _ref, new InlineUnitOfWork(), _user.Object);

    private UpdatePresupuestoUseCase UpdateSut() => new(_ventas, _recibos, _ref, new InlineUnitOfWork(), _user.Object);

    private AnularPresupuestoUseCase AnularSut() => new(_ventas, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private void Existente()
    {
        _ventas.Documentos[IdPresupuesto] = new DocumentosCliente
        {
            IdDocumentoCliente = IdPresupuesto, IdComprobanteTipo = R.Pv, IdCliente = 5, IdSucursal = 1, Letra = "P", PuntoVenta = "0001",
            Numero = "00000001", FechaEmision = Fecha, Estado = _ref.Estado("DOCUMENTOSCLIENTE", "GENERADO"), Remitar = true, Facturar = true, Pendiente = true,
        };
        _ventas.Detalles.Add(new DocumentosClienteDetalle { IdDocumentoClienteDetalle = 900, IdDocumentoCliente = IdPresupuesto, IdItem = 10, Cantidad = 2 });
    }

    private static CreatePresupuestoDto Dto(params VentaItemDto[] items) => new()
    {
        IdEntidad = 5,
        IdSucursal = 1,
        FechaEmision = Fecha,
        Total = items.Sum(i => i.Total),
        Items = [.. items],
    };

    private static VentaItemDto Item(int idItem, decimal cantidad, decimal otros = 0) => new()
    {
        IdItem = idItem,
        Descripcion = "articulo",
        Cantidad = cantidad,
        PrecioUnitario = 100,
        PrecioNeto = 100 * cantidad,
        Otros = otros,
        Total = 100 * cantidad + otros,
    };
}
