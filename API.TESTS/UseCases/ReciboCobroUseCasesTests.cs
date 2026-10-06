using API.DA.Entities;
using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Models.Clientes;
using API.SERVICE.UseCases.Clientes;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Moq;
using R = API.TESTS.Infrastructure.FakeReferencias;

namespace API.TESTS.UseCases;

/// <summary>Recibos de cobro: paridad con Agregar_Ws / Editar_Ws de FrmRecibos.</summary>
public class ReciboCobroUseCasesTests
{
    private static readonly DateTime Ahora = new(2026, 10, 6, 11, 0, 0);
    private static readonly DateTime FechaRecibo = new(2026, 10, 6);

    private readonly FakeReciboCobroRepository _repo = new();
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    public ReciboCobroUseCasesTests()
    {
        _user.SetupGet(u => u.IdUsuario).Returns(3);
    }

    // ---------- Reglas puras ----------

    [Theory]
    [InlineData(1000, 1000, false, 0, 0)]      // cubre justo
    [InlineData(1500, 1000, false, 0, 500)]    // sobra recibo
    [InlineData(400, 1000, true, 600, 0)]      // cobro parcial
    public void Imputar_Venta_ConsumeElSaldoDelRecibo(decimal saldoRecibo, decimal importe, bool parcial, decimal saldoCtaCte, decimal restante)
    {
        var r = ImputacionRules.Imputar(TipoImputacion.Venta, importe, saldoRecibo);

        r.Parcial.Should().Be(parcial);
        r.SaldoCtaCte.Should().Be(saldoCtaCte);
        r.Cancelado.Should().Be(!parcial);
        r.ImporteImputado.Should().Be(parcial ? saldoRecibo : importe);
        r.SaldoReciboRestante.Should().Be(restante);
    }

    [Fact]
    public void Imputar_VentaSinSaldoDeRecibo_Rechaza()
    {
        var act = () => ImputacionRules.Imputar(TipoImputacion.Venta, 100, 0);

        act.Should().Throw<BusinessException>().WithMessage("*no alcanza*");
    }

    [Fact]
    public void Imputar_OrdenDePago_SeImputaEnNegativoYSeCancelaEntera()
    {
        var r = ImputacionRules.Imputar(TipoImputacion.OrdenPago, 300, 1000);

        r.ImporteImputado.Should().Be(-300);
        r.Cancelado.Should().BeTrue();
        r.SaldoReciboRestante.Should().Be(700);
    }

    [Theory]
    [InlineData(1000, 1000, 0, true)]
    [InlineData(1200, 1000, 0, true)]
    [InlineData(800, 1000, -200, false)]   // pagó de más: queda saldo a favor del cliente
    public void SaldoRecibo_ComoElErp(decimal totalComprobantes, decimal totalRecibo, decimal saldo, bool cancelado)
    {
        ImputacionRules.SaldoRecibo(totalComprobantes, totalRecibo).Should().Be((saldo, cancelado));
    }

    // ---------- Alta ----------

    [Fact]
    public async Task Create_FacturaCubiertaConEfectivo_GrabaTodoComoElErp()
    {
        Pendiente(idComprobante: 50, tipo: R.Ven, saldo: 1000, concepto: "FAC A-0003-00000050");

        var result = await CreateSut().ExecuteAsync(Dto(
            [Imputacion(50, R.Ven, 1000)],
            [Elemento(R.Efectivo, 1000)]));

        // Recibo: datos del cliente desde su ficha, numeración del servidor y estado fijo 56
        var recibo = _repo.Single<EntidadesRecibos>();
        result.IdEntidadRecibo.Should().Be(recibo.IdEntidadRecibo);
        recibo.Should().Match<EntidadesRecibos>(r =>
            r.RazonSocial == "ALMACEN DON PEPE" && r.NroDoc == "20123456789" && r.Letra == "X" &&
            r.PuntoVenta == "0003" && r.Numero == "00000042" && r.Estado == 56 && r.Total == 1000 &&
            r.IdPlanillaCaja == 77 && r.IdUsuario == 3 && r.IdComprobanteTipo == R.Rec);

        // Imputación: cta. cte. del comprobante cancelada y estado COBRADO
        _repo.Operaciones.Should().Contain("ImputarCtaCte 50/11 saldo=0.00 cancelado=True interes=0.00 fecha=2026-10-06");
        _repo.EstadosDocumentoCliente[50].Should().Be(_ref.Estado(EstadosCobranza.DocumentoCobrado));
        _repo.Single<EntidadRecibosDocumentosCliente>().Should().Match<EntidadRecibosDocumentosCliente>(i =>
            i.ImporteRecibo == 1000 && i.Saldo == 0 && i.NumeroComprobante == "FAC A-0003-00000050" && i.NumeroRecibo == "X-0003-00000042");

        // Cta. cte. del recibo
        _repo.Single<EntidadesCtaCte>().Should().Match<EntidadesCtaCte>(c =>
            c.Concepto == "REC-X-0003-00000042" && c.Total == 1000 && c.Saldo == 0 && c.Cancelado == true &&
            c.Total2 == -1000 && c.Fecha == Ahora && c.FechaPago == FechaRecibo);

        // Efectivo: caja + detalle + movimiento de cta. cte.
        _repo.Single<CajasPlanillasDetalle>().Should().Match<CajasPlanillasDetalle>(c =>
            c.IdCajaPlanilla == 77 && c.Reducida == "REC" && c.Descripcion == "X-0003-00000042" && c.Debe == 1000 && c.Haber == 0);
        _repo.Single<EntidadesRecibosDetalle>().Total.Should().Be(1000);
        _repo.Single<EntidadesCtaCteMovimientos>().Should().Match<EntidadesCtaCteMovimientos>(m =>
            m.Concepto == "REC -X-0003-00000042" && m.AfavorEntidad == 1000);
        _repo.Operaciones.Should().Contain("Numerar 0003");
    }

    [Fact]
    public async Task Create_PagoParcial_DejaSaldoYEstadoCobradoParcial()
    {
        Pendiente(50, R.Ven, saldo: 1000);

        await CreateSut().ExecuteAsync(Dto([Imputacion(50, R.Ven, 1000)], [Elemento(R.Efectivo, 400)]));

        _repo.Operaciones.Should().Contain(o => o.StartsWith("ImputarCtaCte 50/11 saldo=600.00 cancelado=False"));
        _repo.EstadosDocumentoCliente[50].Should().Be(_ref.Estado(EstadosCobranza.DocumentoCobradoParcial));
        _repo.Single<EntidadRecibosDocumentosCliente>().Should().Match<EntidadRecibosDocumentosCliente>(i => i.ImporteRecibo == 400 && i.Saldo == 600);
        _repo.Single<EntidadesCtaCte>().Should().Match<EntidadesCtaCte>(c => c.Saldo == 0 && c.Cancelado == true);
    }

    [Fact]
    public async Task Create_PagaDeMas_QuedaSaldoAFavorEnLaCtaCteDelRecibo()
    {
        Pendiente(50, R.Ven, saldo: 800);

        await CreateSut().ExecuteAsync(Dto([Imputacion(50, R.Ven, 800)], [Elemento(R.Efectivo, 1000)]));

        _repo.Single<EntidadesCtaCte>().Should().Match<EntidadesCtaCte>(c => c.Saldo == -200 && c.Cancelado == false);
    }

    [Fact]
    public async Task Create_ConInteresYRecargo_TotalesYValidacionComoElFormulario()
    {
        Pendiente(50, R.Ven, saldo: 1000);
        var dto = Dto([Imputacion(50, R.Ven, 1050, interes: 50)], [Elemento(R.Tarjeta, 1050, cuenta: 4)]);
        dto.RecargoTarjeta = 105;

        await CreateSut().ExecuteAsync(dto);

        _repo.Single<EntidadesRecibos>().Total.Should().Be(1155);
        _repo.Operaciones.Should().Contain(o => o.StartsWith("ImputarCtaCte 50/11 saldo=0.00 cancelado=True interes=50.00"));
        _repo.Single<EntidadesCtaCte>().InteresAplicado.Should().Be(50);
        _repo.Single<BancosCuentasMovimientos>().Should().Match<BancosCuentasMovimientos>(m => m.IdMovimientoTipo == 4 && m.IdBancoCuenta == 4 && m.Importe == 1050);
    }

    [Fact]
    public async Task Create_NotaDeCreditoConSaldoInvertido_ValidaContraElSaldoVisible()
    {
        // Tipos 4, 12 y 9 se guardan en negativo en la cta. cte. y el formulario los muestra en positivo.
        Pendiente(60, R.Nc, saldo: -300);

        await CreateSut().ExecuteAsync(Dto([Imputacion(60, R.Nc, 300)], [Elemento(R.Efectivo, 500)]));

        _repo.EstadosDocumentoCliente[60].Should().Be(_ref.Estado(EstadosCobranza.DocumentoCobrado));
    }

    [Fact]
    public async Task Create_Cheque_QuedaEnCarteraYEntraEnCaja()
    {
        await CreateSut().ExecuteAsync(Dto([], [Elemento(R.Cheque, 700, numero: "00012345")]));

        _repo.Single<EntidadesCheques>().Should().Match<EntidadesCheques>(c =>
            c.Nro == "00012345" && c.Importe == 700 && c.ValorToma == 700 && c.IdEntidad == 5 &&
            c.Estado == _ref.Estado(EstadosCobranza.ChequeEnCartera) && c.Tipo == R.ChequeAutomatico && c.IdEntidadReciboTipo == R.Rec);
        _repo.Single<CajasPlanillasDetalle>().IdElementoCobro.Should().Be(R.Cheque);
    }

    [Fact]
    public async Task Create_Retencion_SinTipo_Rechaza()
    {
        var act = () => CreateSut().ExecuteAsync(Dto([], [Elemento(R.Retencion, 100)]));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*tipo de retención*");
    }

    [Fact]
    public async Task Create_ImporteQueNoCoincideConElSaldo_Rechaza()
    {
        Pendiente(50, R.Ven, saldo: 1000);

        var act = () => CreateSut().ExecuteAsync(Dto([Imputacion(50, R.Ven, 900)], [Elemento(R.Efectivo, 900)]));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no coincide*");
        _repo.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ComprobanteDeOtroClienteOCancelado_Rechaza()
    {
        Pendiente(50, R.Ven, saldo: 1000, idEntidad: 999);

        var act = () => CreateSut().ExecuteAsync(Dto([Imputacion(50, R.Ven, 1000)], [Elemento(R.Efectivo, 1000)]));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no está pendiente*");
    }

    [Fact]
    public async Task Create_TipoNoImputable_Rechaza()
    {
        var act = () => CreateSut().ExecuteAsync(Dto([Imputacion(50, 777, 10)], [Elemento(R.Efectivo, 10)]));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no se puede imputar*");
    }

    [Fact]
    public async Task Create_SinPlanillaAbierta_Rechaza()
    {
        _repo.Planilla = null;

        var act = () => CreateSut().ExecuteAsync(Dto([], [Elemento(R.Efectivo, 10)]));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*planilla de caja abierta*");
    }

    [Fact]
    public async Task Create_NumeracionManual_UsaElNumeroInformadoYIgualIncrementaElContador()
    {
        _ref.Parametros[("NUMERACION", "REC")] = "1";
        var dto = Dto([], [Elemento(R.Efectivo, 10)]);
        dto.PuntoVenta = "0009";
        dto.Numero = "00000123";

        await CreateSut().ExecuteAsync(dto);

        _repo.Single<EntidadesRecibos>().Should().Match<EntidadesRecibos>(r => r.PuntoVenta == "0009" && r.Numero == "00000123");
        _repo.Operaciones.Should().Contain("Numerar 0009");
    }

    [Fact]
    public async Task Create_NumeracionManualSinNumero_Rechaza()
    {
        _ref.Parametros[("NUMERACION", "REC")] = "1";

        var act = () => CreateSut().ExecuteAsync(Dto([], [Elemento(R.Efectivo, 10)]));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*numeración de recibos es manual*");
    }

    [Fact]
    public async Task Create_UsuarioSinFilaEnUsuarios_Forbidden()
    {
        _user.SetupGet(u => u.IdUsuario).Returns((int?)null);

        var act = () => CreateSut().ExecuteAsync(Dto([], [Elemento(R.Efectivo, 10)]));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ---------- Anulación ----------

    [Fact]
    public async Task Anular_RevierteTodoYRestauraEstados()
    {
        _repo.Recibo = new EntidadesRecibos { IdEntidadRecibo = 300, Estado = 56 };
        _repo.Imputaciones.AddRange(
        [
            new ImputacionRow(5, 50, R.Ven, ImporteRecibo: 1000, InteresAplicado: 50),
            new ImputacionRow(5, 51, R.Fv, ImporteRecibo: 200, InteresAplicado: 0),
            new ImputacionRow(5, 52, R.Ven, ImporteRecibo: 100, InteresAplicado: 0),
        ]);
        _repo.ConRelacion.Add(51);
        _repo.ConOtrasImputaciones.Add(52);

        await AnularSut().ExecuteAsync(300);

        _repo.EstadosRecibo[300].Should().Be(_ref.Estado(EstadosCobranza.ReciboAnulado));
        _repo.Operaciones.Should().ContainInOrder(
            "AnularDetalle 300", $"AnularCaja {R.Rec}/300", "AnularCheques 300", "AnularBancos 300", "AnularRetenciones 300",
            "BorrarImputaciones 300",
            "RevertirCtaCte 50/11 saldo=950.00 interes=50.00",
            "AnularCtaCte 9000");
        _repo.EstadosDocumentoCliente[50].Should().Be(_ref.Estado(EstadosCobranza.DocumentoGenerado));
        _repo.EstadosDocumentoCliente[51].Should().Be(_ref.Estado(EstadosCobranza.DocumentoCancelado), "la FV tiene relación");
        _repo.EstadosDocumentoCliente[52].Should().Be(_ref.Estado(EstadosCobranza.DocumentoCobradoParcial), "sigue imputada por otro recibo");
    }

    [Fact]
    public async Task Anular_ReciboYaAnulado_Conflict()
    {
        _repo.Recibo = new EntidadesRecibos { IdEntidadRecibo = 300, Estado = _ref.Estado(EstadosCobranza.ReciboAnulado) };

        var act = () => AnularSut().ExecuteAsync(300);

        await act.Should().ThrowAsync<ConflictException>();
        _repo.Operaciones.Should().BeEmpty();
    }

    [Fact]
    public async Task Anular_ReciboInexistente_NotFound()
    {
        var act = () => AnularSut().ExecuteAsync(404);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ---------- Inicio ----------

    [Fact]
    public async Task Iniciar_DevuelvePlanillaYNumeroSugerido()
    {
        var result = await new IniciarReciboUseCase(_repo, _ref, _user.Object).ExecuteAsync();

        result.Should().Be(new NuevoReciboDisplay(77, "0003", "X", false, "00000042"));
    }

    // ---------- helpers ----------

    private CreateReciboUseCase CreateSut() => new(new ReciboCobroWriter(_repo, _ref, new FixedServerClock(Ahora)), _repo, new InlineUnitOfWork(), _user.Object);

    private AnularReciboUseCase AnularSut() => new(_repo, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora));

    private void Pendiente(int idComprobante, int tipo, decimal saldo, string concepto = "COMPROBANTE", int idEntidad = 5) =>
        _repo.CtaCte.Add(new EntidadesCtaCte
        {
            IdEntidad = idEntidad, IdComprobante = idComprobante, IdComprobanteTipo = tipo, Saldo = saldo, Cancelado = false, Concepto = concepto,
        });

    private static CreateReciboDto Dto(List<ImputacionDto> imputaciones, List<ElementoCobroDto> elementos) => new()
    {
        IdEntidad = 5,
        FechaEmision = FechaRecibo,
        IdSucursal = 1,
        Imputaciones = imputaciones,
        Elementos = elementos,
    };

    private static ImputacionDto Imputacion(int idComprobante, int tipo, decimal importe, decimal interes = 0) =>
        new() { IdComprobante = idComprobante, IdComprobanteTipo = tipo, ImporteComprobante = importe, InteresAplicado = interes };

    private static ElementoCobroDto Elemento(int idElemento, decimal importe, int cuenta = 0, string? numero = null) =>
        new() { IdElementoCobro = idElemento, Importe = importe, IdBancoCuenta = cuenta, Numero = numero, Descripcion = "PAGO" };
}
