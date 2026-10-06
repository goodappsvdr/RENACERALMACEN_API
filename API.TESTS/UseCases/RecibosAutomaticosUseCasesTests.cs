using API.DA.Entities;
using API.SERVICE.Domain.Cobranzas;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Models.Clientes;
using API.SERVICE.UseCases.Clientes;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using R = API.TESTS.Infrastructure.FakeReferencias;

namespace API.TESTS.UseCases;

/// <summary>
/// Emisión masiva de recibos: paridad con GenerarRecibos_WS (FrmRecibosAutomaticos).
/// Corre la cadena real (pendientes + alta de recibo) sobre los fakes en memoria.
/// </summary>
public class RecibosAutomaticosUseCasesTests
{
    private static readonly DateTime Ahora = new(2026, 10, 6, 15, 30, 0);

    private readonly FakeReciboCobroRepository _repo = new();
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    public RecibosAutomaticosUseCasesTests()
    {
        _user.SetupGet(u => u.IdUsuario).Returns(3);
        _repo.Entidades[6] = new Entidades { IdEntidad = 6, RazonSocial = "KIOSCO LA ESQUINA", IdCategoriaIva = 1, Cuit = "27222333444" };
    }

    [Fact]
    public async Task Generar_CancelaTodoConEfectivo_PrimeroLoQueEstaAFavor()
    {
        Pendiente(5, idComprobante: 50, R.Ven, saldo: 1000, "FAC-50");
        Pendiente(5, idComprobante: 60, R.Nc, saldo: 200, "NC-60"); // tipo 4: se muestra -200 (a favor del cliente)
        Grilla(5, 800);

        var resultado = await Sut().ExecuteAsync(Dto(5));

        resultado.Should().ContainSingle().Which.Should().Match<ReciboAutomaticoResultado>(r =>
            r.Generado && r.Importe == 800 && r.Recibo == "X-0003-00000042" && r.Mensaje == null && r.RazonSocial == "ALMACEN DON PEPE");

        var recibo = _repo.Single<EntidadesRecibos>();
        recibo.Should().Match<EntidadesRecibos>(r => r.Observaciones == "RECIBO AUTOMATICO" && r.IdSucursal == 2 && r.FechaEmision == Ahora.Date && r.Total == 800);

        // Primero la NC (negativa) y después la factura: la factura queda cobrada entera
        _repo.All<EntidadRecibosDocumentosCliente>().Select(i => (i.IdDocumentoCliente, i.ImporteRecibo, i.Saldo))
            .Should().Equal((60, -200m, 0m), (50, 1000m, 0m));
        _repo.EstadosDocumentoCliente[50].Should().Be(_ref.Estado(EstadosCobranza.DocumentoCobrado));
        _repo.EstadosDocumentoCliente[60].Should().Be(_ref.Estado(EstadosCobranza.DocumentoCobrado));
        _repo.Operaciones.Should().Contain(o => o.StartsWith("ImputarCtaCte 50/11 saldo=0.00 cancelado=True interes=0.00"));

        // Un solo elemento en efectivo, como el ERP
        _repo.Single<EntidadesRecibosDetalle>().Should().Match<EntidadesRecibosDetalle>(d =>
            d.Descripcion == "EFECTIVO" && d.Nro == "0000000000" && d.Total == 800 && d.IdElementoCobroPago == R.Efectivo);
        _repo.Single<CajasPlanillasDetalle>().Debe.Should().Be(800);
        _repo.Single<EntidadesCtaCte>().Should().Match<EntidadesCtaCte>(c => c.Saldo == 0 && c.Cancelado == true && c.InteresAplicado == 0);
        _repo.Bloqueos.Should().Equal(5);
    }

    [Fact]
    public async Task Generar_VariosClientes_CadaUnoIndependiente()
    {
        Pendiente(5, 50, R.Ven, 1000);
        Grilla(5, 1000);
        Pendiente(6, 70, R.Ven, 500);
        Grilla(6, 999); // no coincide con sus comprobantes

        var resultado = await Sut().ExecuteAsync(Dto(5, 6));

        resultado.Select(r => (r.IdEntidad, r.Generado)).Should().Equal((5, true), (6, false));
        resultado[1].Mensaje.Should().Be("No coincide: saldo en la grilla 999,00, comprobantes pendientes 500,00.");
        _repo.All<EntidadesRecibos>().Should().ContainSingle();
    }

    [Fact]
    public async Task Generar_ComprobanteDeProveedor_SeHaceAMano()
    {
        Pendiente(5, 50, R.Ven, 1000);
        Pendiente(5, 80, R.Op, 100, "OP-80");
        Grilla(5, 1100);

        var resultado = await Sut().ExecuteAsync(Dto(5));

        resultado.Single().Mensaje.Should().Be("Tiene comprobantes de proveedor pendientes (OP-80): hacer el recibo a mano.");
        _repo.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Generar_TipoDesconocido_NoSeCancela()
    {
        Pendiente(5, 90, 555, 100, "RAR-90");
        Grilla(5, 100);

        var resultado = await Sut().ExecuteAsync(Dto(5));

        resultado.Single().Mensaje.Should().Contain("no sabe cancelar (RAR-90)");
    }

    [Theory]
    [InlineData(false, "Ya no tiene saldo a cobrar.")]
    [InlineData(true, "No tiene comprobantes pendientes.")]
    public async Task Generar_SinSaldoOSinComprobantes_Informa(bool enGrilla, string mensaje)
    {
        if (enGrilla) Grilla(5, 100);

        var resultado = await Sut().ExecuteAsync(Dto(5));

        resultado.Single().Should().Match<ReciboAutomaticoResultado>(r => !r.Generado && r.Mensaje == mensaje);
    }

    [Fact]
    public async Task Generar_EntidadInexistente_Informa()
    {
        var resultado = await Sut().ExecuteAsync(Dto(404));

        resultado.Single().Mensaje.Should().Be("No se encontró la entidad.");
    }

    [Fact]
    public async Task Generar_IdsRepetidos_UnSoloRecibo()
    {
        Pendiente(5, 50, R.Ven, 1000);
        Grilla(5, 1000);

        var resultado = await Sut().ExecuteAsync(Dto(5, 5, 5));

        resultado.Should().ContainSingle();
    }

    [Fact]
    public async Task Generar_MasDeDiez_Rechaza()
    {
        var act = () => Sut().ExecuteAsync(Dto(Enumerable.Range(1, 11).ToArray()));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("Se pueden generar hasta 10 recibos por vez.");
    }

    [Fact]
    public async Task Generar_NumeracionManual_RechazaLaTanda()
    {
        _ref.Parametros[("NUMERACION", "REC")] = "1";

        var act = () => Sut().ExecuteAsync(Dto(5));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*numeración de recibos está configurada como manual*");
    }

    [Fact]
    public async Task Generar_SinSucursalLocal_RechazaLaTanda()
    {
        _repo.SucursalLocal = null;

        var act = () => Sut().ExecuteAsync(Dto(5));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("El usuario no tiene habilitada la sucursal LOCAL.");
    }

    [Fact]
    public async Task Generar_SinPlanillaAbierta_RechazaLaTanda()
    {
        _repo.Planilla = null;

        var act = () => Sut().ExecuteAsync(Dto(5));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*planilla de caja abierta*");
    }

    [Fact]
    public async Task Entidades_RedondeaElSaldoDeLaGrilla()
    {
        _repo.SaldosGrilla.Add(new SaldoEntidadRow(5, "ALMACEN DON PEPE", 1234.567m));

        var result = await new GetEntidadesRecibosAutomaticosUseCase(_repo).ExecuteAsync();

        result.Should().Equal(new EntidadReciboAutomaticoDisplay(5, "ALMACEN DON PEPE", 1234.57m));
    }

    // ---------- helpers ----------

    private GenerarRecibosAutomaticosUseCase Sut()
    {
        var clock = new FixedServerClock(Ahora);
        var pendientes = new GetComprobantesPendientesUseCase(_repo, clock);
        var crear = new CreateReciboUseCase(_repo, _ref, new InlineUnitOfWork(), clock, _user.Object);
        return new GenerarRecibosAutomaticosUseCase(_repo, _ref, pendientes, crear, clock, _user.Object, NullLogger<GenerarRecibosAutomaticosUseCase>.Instance);
    }

    private void Pendiente(int idEntidad, int idComprobante, int tipo, decimal saldo, string concepto = "COMPROBANTE") =>
        _repo.CtaCte.Add(new EntidadesCtaCte
        {
            IdEntidad = idEntidad, IdComprobante = idComprobante, IdComprobanteTipo = tipo, Saldo = saldo, Cancelado = false, Concepto = concepto,
        });

    private void Grilla(int idEntidad, decimal saldo) =>
        _repo.SaldosGrilla.Add(new SaldoEntidadRow(idEntidad, _repo.Entidades.GetValueOrDefault(idEntidad)?.RazonSocial, saldo));

    private static GenerarRecibosAutomaticosDto Dto(params int[] ids) => new() { IdsEntidad = [.. ids] };
}
