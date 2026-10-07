using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Models.Bancos;
using API.SERVICE.Models.Compras;
using API.SERVICE.UseCases.Bancos;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Moq;
using R = API.TESTS.Infrastructure.FakeReferencias;

namespace API.TESTS.UseCases;

/// <summary>Órdenes de depósito / extracción: paridad con Agregar_Ws / Editar_Ws de FrmOrdendeDeposito y FrmOrdendeExtraccion.</summary>
public class OrdenBancariaUseCasesTests
{
    private const int Od = 40, Oe = 41, IdCuenta = 15, IdOrden = 610;
    private static readonly DateTime Ahora = new(2026, 10, 7, 22, 0, 0);
    private static readonly DateTime Fecha = new(2026, 10, 7);

    private readonly FakeOrdenBancariaRepository _ordenes = new();
    private readonly FakeOrdenPagoRepository _cheques = new();
    private readonly FakeReciboCobroRepository _recibos = new();
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    public OrdenBancariaUseCasesTests()
    {
        _ref.Parametros[("COMPROBANTE", "OD")] = Od.ToString();
        _ref.Parametros[("COMPROBANTE", "OE")] = Oe.ToString();
        _ordenes.Cuentas[IdCuenta] = new BancosCuentas
        {
            IdBancoCuenta = IdCuenta, IdBanco = 4, IdBancoSucursal = 9, IdCuentaTipo = 2, NroCuenta = "123/4", Estado = _ref.Estado("BANCOSCUENTAS", "ACTIVA"),
        };
        _user.SetupGet(u => u.IdUsuario).Returns(3);
    }

    [Fact]
    public async Task Iniciar_SugiereElProximoNumeroDeLaPlanilla()
    {
        var r = await new IniciarOrdenBancariaUseCase(_recibos, _ref, _user.Object).ExecuteAsync("OD");

        r.PuntoVenta.Should().Be("0003");
        r.NumeroSugerido.Should().Be("00000042");
    }

    [Fact]
    public async Task Deposito_EfectivoYCheque_IngresaEnLaCuentaYDepositaElCheque()
    {
        _cheques.ChequesTerceros[50] = new EntidadesCheques
        {
            IdEntidadCheque = 50, Importe = 300, IdBanco = 7, IdSucursal = 8, Nro = "0001234", Estado = _ref.Estado("CLIENTECHEQUE", "EN CARTERA"),
        };

        var r = await DepositoSut().ExecuteAsync(Deposito(Efectivo(1000), new ElementoPagoDto { IdElementoCobro = R.Cheque, Importe = 300, IdCheque = 50 }));

        var od = _ordenes.Single<OrdenesDepositos>();
        od.Should().Match<OrdenesDepositos>(o => o.IdComprobanteTipo == Od && o.Letra == "X" && o.PuntoVenta == "0003" && o.Numero == "00000042"
            && o.Total == 1300 && o.IdPlanillaCaja == 77 && o.Estado == _ref.Estado("ORDENDEPOSITO", "GENERADA"));
        r.IdOrdenDepostio.Should().Be(od.IdOrdenDepostio);

        var movimientos = _ordenes.All<BancosCuentasMovimientos>().ToList();
        movimientos.Should().HaveCount(2).And.OnlyContain(m => m.IdBancoCuenta == IdCuenta && m.IdComprobanteTipo == Od && m.IdComprobante == od.IdOrdenDepostio
            && m.Haber == 0 && m.Total == m.Debe && m.Estado == _ref.Estado("BANCOCUENTAMOVIMIENTO", "ACTIVA"));
        movimientos[0].Should().Match<BancosCuentasMovimientos>(m => m.IdMovimientoTipo == 1 && m.Debe == 1000);
        movimientos[1].Should().Match<BancosCuentasMovimientos>(m => m.IdMovimientoTipo == 5 && m.Debe == 300 && m.IdBancoOrigen == 7 && m.NroCuentaOrigen == "0001234");

        _cheques.ChequesTerceros[50].Estado.Should().Be(_ref.Estado("CLIENTECHEQUE", "DEPOSITADO"));
        _ordenes.All<OrdenesDepositosDetalle>().Select(d => d.IdElemento).Should().Equal(movimientos.Select(m => (int?)m.IdBancoCuentaMovimiento));
        _ordenes.All<CajasPlanillasDetalle>().Should().BeEmpty("el ERP no toca la caja al depositar");
    }

    [Fact]
    public async Task Deposito_Tarjeta_EsUnIngresoPositivo()
    {
        await DepositoSut().ExecuteAsync(Deposito(new ElementoPagoDto { IdElementoCobro = R.Tarjeta, Importe = 250 }));

        _ordenes.Single<BancosCuentasMovimientos>().Should().Match<BancosCuentasMovimientos>(m => m.Debe == 250 && m.Total == 250);
    }

    [Fact]
    public async Task Deposito_ChequeNoEnCartera_409()
    {
        _cheques.ChequesTerceros[50] = new EntidadesCheques { IdEntidadCheque = 50, Importe = 300, Estado = _ref.Estado("CLIENTECHEQUE", "DEPOSITADO") };

        var act = () => DepositoSut().ExecuteAsync(Deposito(new ElementoPagoDto { IdElementoCobro = R.Cheque, Importe = 300, IdCheque = 50 }));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Deposito_ImporteDistintoAlDelCheque_400()
    {
        _cheques.ChequesTerceros[50] = new EntidadesCheques { IdEntidadCheque = 50, Importe = 300, Estado = _ref.Estado("CLIENTECHEQUE", "EN CARTERA") };

        var act = () => DepositoSut().ExecuteAsync(Deposito(new ElementoPagoDto { IdElementoCobro = R.Cheque, Importe = 200, IdCheque = 50 }));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no coincide*");
    }

    [Fact]
    public async Task Deposito_Retencion_400()
    {
        var act = () => DepositoSut().ExecuteAsync(Deposito(new ElementoPagoDto { IdElementoCobro = R.Retencion, Importe = 10 }));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no se puede depositar*");
    }

    [Fact]
    public async Task Deposito_CuentaInactiva_400()
    {
        _ordenes.Cuentas[IdCuenta].Estado = _ref.Estado("BANCOSCUENTAS", "INACTIVA");

        var act = () => DepositoSut().ExecuteAsync(Deposito(Efectivo(100)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no está activa*");
    }

    [Fact]
    public async Task Deposito_SinPlanillaAbierta_NoGraba()
    {
        _recibos.Planilla = null;

        var act = () => DepositoSut().ExecuteAsync(Deposito(Efectivo(100)));

        await act.Should().ThrowAsync<BusinessException>();
        _ordenes.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Extraccion_EgresoDeLaCuenta()
    {
        var r = await ExtraccionSut().ExecuteAsync(new CreateOrdenExtraccionDto { IdBancoCuenta = IdCuenta, FechaEmision = Fecha, Importe = 500 });

        var oe = _ordenes.Single<OrdenesExrtacciones>();
        oe.Should().Match<OrdenesExrtacciones>(o => o.IdComprobanteTipo == Oe && o.Numero == "00000042" && o.Total == 500
            && o.Estado == _ref.Estado("ORDENEXTRACCION", "GENERADA"));
        r.IdOrdenExtraccion.Should().Be(oe.IdOrdenExtraccion);
        var m = _ordenes.Single<BancosCuentasMovimientos>();
        m.Should().Match<BancosCuentasMovimientos>(x => x.IdMovimientoTipo == 6 && x.Debe == 0 && x.Haber == 500 && x.Total == -500
            && x.IdBancoOrigen == 4 && x.NroCuentaOrigen == "123/4" && x.IdComprobanteTipo == Oe);
        _ordenes.Single<OrdenesExtraccionesDetalle>().Should().Match<OrdenesExtraccionesDetalle>(d => d.IdElemento == m.IdBancoCuentaMovimiento
            && d.IdElementoCobroPago == R.Efectivo && d.Total == 500);
    }

    [Fact]
    public async Task AnularDeposito_AnulaMovimientosYDevuelveCheques()
    {
        _ordenes.Depositos[IdOrden] = new OrdenesDepositos { IdOrdenDepostio = IdOrden, Total = 1300, Estado = _ref.Estado("ORDENDEPOSITO", "GENERADA") };

        await AnularSut().AnularDepositoAsync(IdOrden);

        _ordenes.Depositos[IdOrden].Estado.Should().Be(_ref.Estado("ORDENDEPOSITO", "ANULADA"));
        _recibos.Operaciones.Should().Contain($"AnularBancos {IdOrden}");
        _cheques.Operaciones.Should().Contain($"DevolverChequesTerceros {IdOrden}");
        _ordenes.Operaciones.Should().Contain($"AnularDetalleDeposito {IdOrden}");
    }

    [Fact]
    public async Task AnularDeposito_YaAnulada_409()
    {
        _ordenes.Depositos[IdOrden] = new OrdenesDepositos { IdOrdenDepostio = IdOrden, Estado = _ref.Estado("ORDENDEPOSITO", "ANULADA") };

        var act = () => AnularSut().AnularDepositoAsync(IdOrden);

        await act.Should().ThrowAsync<ConflictException>();
        _recibos.Operaciones.Should().BeEmpty();
    }

    [Fact]
    public async Task AnularExtraccion_AnulaElMovimiento()
    {
        _ordenes.Extracciones[IdOrden] = new OrdenesExrtacciones { IdOrdenExtraccion = IdOrden, Total = 500, Estado = _ref.Estado("ORDENEXTRACCION", "GENERADA") };

        await AnularSut().AnularExtraccionAsync(IdOrden);

        _ordenes.Extracciones[IdOrden].Estado.Should().Be(_ref.Estado("ORDENEXTRACCION", "ANULADA"));
        _recibos.Operaciones.Should().Contain($"AnularBancos {IdOrden}");
        _ordenes.Operaciones.Should().Contain($"AnularDetalleExtraccion {IdOrden}");
    }

    [Fact]
    public async Task AnularExtraccion_Inexistente_404()
    {
        var act = () => AnularSut().AnularExtraccionAsync(999);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ---------- helpers ----------

    private CreateOrdenDepositoUseCase DepositoSut() =>
        new(_ordenes, _cheques, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private CreateOrdenExtraccionUseCase ExtraccionSut() =>
        new(_ordenes, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private AnularOrdenBancariaUseCase AnularSut() =>
        new(_ordenes, _cheques, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private static CreateOrdenDepositoDto Deposito(params ElementoPagoDto[] elementos) => new()
    {
        IdBancoCuenta = IdCuenta,
        FechaEmision = Fecha,
        Elementos = [.. elementos],
    };

    private static ElementoPagoDto Efectivo(decimal importe) => new() { IdElementoCobro = R.Efectivo, Importe = importe, Descripcion = "EFECTIVO" };
}
