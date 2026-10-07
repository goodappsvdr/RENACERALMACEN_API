using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Compras;
using API.SERVICE.Models.Compras;
using API.SERVICE.UseCases.Compras;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Moq;
using R = API.TESTS.Infrastructure.FakeReferencias;

namespace API.TESTS.UseCases;

/// <summary>Orden de pago a proveedor: paridad con Agregar_Ws / Editar_Ws de FrmOrdendePago.</summary>
public class OrdenPagoUseCasesTests
{
    private const int Factura = 450, Factura2 = 451, NotaCredito = 460, Venta = 470, IdOrden = 800;
    private static readonly DateTime Ahora = new(2026, 10, 7, 16, 0, 0);
    private static readonly DateTime Fecha = new(2026, 10, 7);

    private readonly FakeReciboCobroRepository _recibos = new();
    private readonly FakeOrdenPagoRepository _ordenes = new();
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    public OrdenPagoUseCasesTests()
    {
        _user.SetupGet(u => u.IdUsuario).Returns(3);
        CtaCte(Factura, R.Fc, 1000, "FC -A-0012-00000345");
    }

    // ---------- Alta ----------

    [Fact]
    public async Task Create_PagoTotalEnEfectivo()
    {
        var r = await CreateSut().ExecuteAsync(Dto([(Factura, R.Fc, 1000)], Efectivo(1000)));

        var op = _ordenes.Single<ProveedoresRecibos>();
        op.Should().Match<ProveedoresRecibos>(o =>
            o.IdProveedor == 5 && o.Letra == "X" && o.PuntoVenta == "0003" && o.Numero == "00000042" && o.Total == 1000
            && o.Estado == _ref.Estado("ORDENESPAGO", "GENERADO") && o.IdPlanillaCaja == 77 && o.RazonSocial == "ALMACEN DON PEPE");
        r.IdProveedorRecibo.Should().Be(op.IdProveedorRecibo);
        _recibos.Bloqueos.Should().Contain(5);

        _recibos.Operaciones.Should().Contain($"ImputarCtaCte {Factura}/{R.Fc} saldo=0.00 cancelado=True interes=0.00 fecha=2026-10-07");
        _recibos.Operaciones.Should().Contain($"EstadoDocumentoProveedor {Factura}={_ref.Estado("DOCUMENTOSPROVEEDOR", "PAGADO")}");
        _ordenes.Single<EntidadOrdenPagoDocumentosProveedores>().Should().Match<EntidadOrdenPagoDocumentosProveedores>(i =>
            i.ImporteOrdenPago == 1000 && i.Saldo == 0 && i.NumeroOrdenPago == "X-0003-00000042" && i.NumeroComprobante == "FC -A-0012-00000345");

        _ordenes.Single<EntidadesCtaCte>().Should().Match<EntidadesCtaCte>(c =>
            c.IdComprobanteTipo == R.Op && c.Total == 1000 && c.Saldo == 0 && c.Cancelado == true && c.Total2 == 1000 && c.Concepto == "OP-X-0003-00000042");
        _ordenes.Single<CajasPlanillasDetalle>().Should().Match<CajasPlanillasDetalle>(c => c.Haber == 1000 && c.Debe == 0 && c.Total2 == -1000 && c.Reducida == "OP");
        _ordenes.Single<EntidadesCtaCteMovimientos>().AfavorEntidad.Should().Be(1000);
        _ordenes.Single<ProveedoresRecibosDetalle>().Total.Should().Be(1000);
        _recibos.Operaciones.Should().Contain("Numerar 0003");
    }

    [Fact]
    public async Task Create_PagoParcial()
    {
        await CreateSut().ExecuteAsync(Dto([(Factura, R.Fc, 1000)], Efectivo(400)));

        _recibos.Operaciones.Should().Contain($"ImputarCtaCte {Factura}/{R.Fc} saldo=600.00 cancelado=False interes=0.00 fecha=2026-10-07");
        _recibos.Operaciones.Should().Contain($"EstadoDocumentoProveedor {Factura}={_ref.Estado("DOCUMENTOSPROVEEDOR", "PAGADO PARCIAL")}");
        _ordenes.Single<EntidadOrdenPagoDocumentosProveedores>().Should().Match<EntidadOrdenPagoDocumentosProveedores>(i => i.ImporteOrdenPago == 400 && i.Saldo == 600);
    }

    [Fact]
    public async Task Create_ImporteNoAlcanzaParaTodas_400()
    {
        CtaCte(Factura2, R.Fc, 500, "FC -A-0012-00000346");

        var act = () => CreateSut().ExecuteAsync(Dto([(Factura, R.Fc, 1000), (Factura2, R.Fc, 500)], Efectivo(1000)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no alcanza*", "el ERP marcaba PAGADA la segunda factura sin aplicarle nada");
    }

    [Fact]
    public async Task Create_NotaCreditoDelProveedorSumaAlDisponible()
    {
        CtaCte(NotaCredito, R.Ncp, -200, "NCP -A-0012-00000010");

        await CreateSut().ExecuteAsync(Dto([(NotaCredito, R.Ncp, -200), (Factura, R.Fc, 1000)], Efectivo(800)));

        _recibos.Operaciones.Should().Contain($"ImputarCtaCte {Factura}/{R.Fc} saldo=0.00 cancelado=True interes=0.00 fecha=2026-10-07");
        _ordenes.All<EntidadOrdenPagoDocumentosProveedores>().Select(i => i.ImporteOrdenPago).Should().Equal(-200m, 1000m);
        _ordenes.Single<EntidadesCtaCte>().Saldo.Should().Be(0);
    }

    [Fact]
    public async Task Create_CompensaUnaVentaAlMismoEnte()
    {
        CtaCte(Venta, R.Ven, 300, "VEN -X-0001-00000099");

        await CreateSut().ExecuteAsync(Dto([(Venta, R.Ven, -300), (Factura, R.Fc, 1000)], Efectivo(700)));

        _recibos.Operaciones.Should().Contain($"EstadoDocumentoCliente {Venta}={_ref.Estado("DOCUMENTOSCLIENTE", "COBRADO")}");
        _ordenes.All<EntidadOrdenPagoDocumentosProveedores>().Select(i => i.ImporteOrdenPago).Should().Equal(300m, 1000m);
        _recibos.Operaciones.Should().Contain($"EstadoDocumentoProveedor {Factura}={_ref.Estado("DOCUMENTOSPROVEEDOR", "PAGADO")}");
    }

    [Fact]
    public async Task Create_ImporteDistintoDelSaldo_400()
    {
        var act = () => CreateSut().ExecuteAsync(Dto([(Factura, R.Fc, 900)], Efectivo(900)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no coincide con su saldo*");
        _ordenes.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ChequeDeTerceros_SeEntregaYQuedaRegistradoAlProveedor()
    {
        _ordenes.ChequesTerceros[50] = new EntidadesCheques { IdEntidadCheque = 50, Nro = "123", Importe = 1000, IdBanco = 4, IdSucursal = 9, Estado = _ref.Estado("CLIENTECHEQUE", "EN CARTERA") };

        await CreateSut().ExecuteAsync(Dto([(Factura, R.Fc, 1000)], Elemento(R.Cheque, 1000, idCheque: 50)));

        var op = _ordenes.Single<ProveedoresRecibos>();
        _ordenes.Operaciones.Should().Contain($"EntregarChequeTercero 50 -> OP {op.IdProveedorRecibo}");
        var entregado = _ordenes.Single<ProveedoresCheques>();
        entregado.Should().Match<ProveedoresCheques>(c =>
            c.IdProveedor == 5 && c.Nro == "123" && c.Importe == 1000 && c.IdSucursal == 9 && c.Estado == _ref.Estado("PROVEEDORCHEQUE", "ENTREGADO") && c.IdComprobanteTipo == R.Op);
        _ordenes.Single<ProveedoresRecibosDetalle>().IdElemento.Should().Be(entregado.IdProveedorCheque);
    }

    [Fact]
    public async Task Create_ChequeDeTercerosQueYaNoEstaEnCartera_409()
    {
        _ordenes.ChequesTerceros[50] = new EntidadesCheques { IdEntidadCheque = 50, Nro = "123", Importe = 1000, Estado = _ref.Estado("CLIENTECHEQUE", "ENTREGADO") };

        var act = () => CreateSut().ExecuteAsync(Dto([(Factura, R.Fc, 1000)], Elemento(R.Cheque, 1000, idCheque: 50)));

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*cartera*");
    }

    [Fact]
    public async Task Create_ChequeDeTercerosConOtroImporte_400()
    {
        _ordenes.ChequesTerceros[50] = new EntidadesCheques { IdEntidadCheque = 50, Nro = "123", Importe = 900, Estado = _ref.Estado("CLIENTECHEQUE", "EN CARTERA") };

        var act = () => CreateSut().ExecuteAsync(Dto([(Factura, R.Fc, 1000)], Elemento(R.Cheque, 1000, idCheque: 50)));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no coincide*");
    }

    [Fact]
    public async Task Create_ChequePropio_SaleDeLaCuentaDelCheque()
    {
        _ordenes.ChequesPropios[60] = new ChequePropioRow(
            new BancosCheques { IdBancoCheque = 60, IdBancoCuenta = 3, NroCheque = "0001", Estado = _ref.Estado("PERSONALCHEQUE", "EN CARTERA") }, 1, 2, 1, "123-4");

        await CreateSut().ExecuteAsync(Dto([(Factura, R.Fc, 1000)], Elemento(R.ChequePropio, 1000, idCheque: 60)));

        var op = _ordenes.Single<ProveedoresRecibos>();
        _ordenes.Operaciones.Should().Contain($"EntregarChequePropio 60 nro=0001 importe=1000.00 -> OP {op.IdProveedorRecibo}");
        _ordenes.Single<BancosCuentasMovimientos>().Should().Match<BancosCuentasMovimientos>(m =>
            m.IdBancoCuenta == 3 && m.IdMovimientoTipo == 2 && m.Haber == 1000 && m.Debe == 0 && m.Total == -1000 && m.NroCuentaOrigen == "123-4");
        _ordenes.Single<ProveedoresRecibosDetalle>().IdElemento.Should().Be(60);
    }

    [Fact]
    public async Task Create_Transferencia_SinCuenta400_ConCuentaMovimiento()
    {
        var sinCuenta = () => CreateSut().ExecuteAsync(Dto([(Factura, R.Fc, 1000)], Elemento(R.Deposito, 1000)));
        await sinCuenta.Should().ThrowAsync<BusinessException>().WithMessage("*cuenta bancaria*");

        var e = Elemento(R.Deposito, 1000);
        e.IdBancoCuenta = 3;
        await CreateSut().ExecuteAsync(Dto([(Factura, R.Fc, 1000)], e));

        _ordenes.Single<BancosCuentasMovimientos>().Should().Match<BancosCuentasMovimientos>(m => m.IdBancoCuenta == 3 && m.Haber == 1000 && m.Total == -1000);
    }

    [Fact]
    public async Task Create_PagaDeMas_QuedaSaldoAFavor()
    {
        await CreateSut().ExecuteAsync(Dto([(Factura, R.Fc, 1000)], Efectivo(1200)));

        _ordenes.Single<EntidadesCtaCte>().Should().Match<EntidadesCtaCte>(c => c.Saldo == -200 && c.Cancelado == false);
    }

    // ---------- Anulación ----------

    [Fact]
    public async Task Anular_DevuelveSaldosEstadosYAnulaTodo()
    {
        Orden("GENERADO");
        _ordenes.Imputaciones.Add(new EntidadOrdenPagoDocumentosProveedores { IdEntidadOrdenPago = IdOrden, IdDocumentoProveedor = Factura, IdComprobanteTipo = R.Fc, ImporteOrdenPago = 1000 });
        _ordenes.Imputaciones.Add(new EntidadOrdenPagoDocumentosProveedores { IdEntidadOrdenPago = IdOrden, IdDocumentoProveedor = Venta, IdComprobanteTipo = R.Ven, ImporteOrdenPago = 300 });

        await AnularSut().ExecuteAsync(IdOrden);

        _recibos.Operaciones.Should().ContainInOrder(
            $"EstadoOrdenPago {IdOrden}={_ref.Estado("ORDENESPAGO", "ANULADO")}",
            $"AnularCaja {R.Op}/{IdOrden}",
            $"AnularBancos {IdOrden}",
            $"AnularRetenciones {IdOrden}",
            $"RevertirCtaCte {Factura}/{R.Fc} saldo=1000.00 interes=0.00",
            $"EstadoDocumentoProveedor {Factura}={_ref.Estado("DOCUMENTOSPROVEEDOR", "GENERADO")}",
            $"RevertirCtaCte {Venta}/{R.Ven} saldo=300.00 interes=0.00",
            $"EstadoDocumentoCliente {Venta}={_ref.Estado("DOCUMENTOSCLIENTE", "GENERADO")}",
            "AnularCtaCte 9000");
        _ordenes.Operaciones.Should().Contain($"DevolverChequesTerceros {IdOrden}").And.Contain($"AnularChequesProveedor {IdOrden}")
            .And.Contain($"AnularChequesPropios {IdOrden}").And.Contain($"AnularDetalle {IdOrden}").And.Contain($"BorrarImputaciones {IdOrden}");
    }

    [Fact]
    public async Task Anular_FacturaPagadaTambienPorOtraOrden_QuedaPagadoParcial()
    {
        Orden("GENERADO");
        _ordenes.Imputaciones.Add(new EntidadOrdenPagoDocumentosProveedores { IdEntidadOrdenPago = IdOrden, IdDocumentoProveedor = Factura, IdComprobanteTipo = R.Fc, ImporteOrdenPago = 400 });
        _ordenes.ConOtrasImputaciones.Add((Factura, R.Fc));

        await AnularSut().ExecuteAsync(IdOrden);

        _recibos.Operaciones.Should().Contain($"EstadoDocumentoProveedor {Factura}={_ref.Estado("DOCUMENTOSPROVEEDOR", "PAGADO PARCIAL")}");
    }

    [Fact]
    public async Task Anular_Relacionada_409()
    {
        Orden("RELACIONADA");

        var act = () => AnularSut().ExecuteAsync(IdOrden);

        await act.Should().ThrowAsync<ConflictException>();
        _recibos.Operaciones.Should().BeEmpty();
    }

    // ---------- Lecturas ----------

    [Fact]
    public async Task Pendientes_InviertenElSaldoDeLosComprobantesDeVenta()
    {
        _ordenes.Pendientes.Add(new EntidadesCtaCte { IdEntidad = 5, IdComprobante = Factura, IdComprobanteTipo = R.Fc, Saldo = 1000 });
        _ordenes.Pendientes.Add(new EntidadesCtaCte { IdEntidad = 5, IdComprobante = Venta, IdComprobanteTipo = R.Ven, Saldo = 300 });

        var r = await new GetComprobantesPendientesPagoUseCase(_ordenes, _ref).ExecuteAsync(5);

        r.Select(x => x.Saldo).Should().Equal(1000m, -300m);
    }

    // ---------- helpers ----------

    private CreateOrdenPagoUseCase CreateSut() => new(_ordenes, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private AnularOrdenPagoUseCase AnularSut() => new(_ordenes, _recibos, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private void CtaCte(int id, int tipo, decimal saldo, string concepto) =>
        _recibos.CtaCte.Add(new EntidadesCtaCte { IdEntidad = 5, IdComprobante = id, IdComprobanteTipo = tipo, Saldo = saldo, Total = saldo, Cancelado = false, Concepto = concepto });

    private void Orden(string estado) =>
        _ordenes.Ordenes[IdOrden] = new ProveedoresRecibos { IdProveedorRecibo = IdOrden, IdProveedor = 5, Estado = _ref.Estado("ORDENESPAGO", estado), Total = 1000 };

    private static CreateOrdenPagoDto Dto((int Id, int Tipo, decimal Importe)[] imputaciones, params ElementoPagoDto[] elementos) => new()
    {
        IdProveedor = 5,
        FechaEmision = Fecha,
        IdSucursal = 1,
        Imputaciones = imputaciones.Select(i => new ImputacionPagoDto { IdComprobante = i.Id, IdComprobanteTipo = i.Tipo, ImporteComprobante = i.Importe }).ToList(),
        Elementos = [.. elementos],
    };

    private static ElementoPagoDto Efectivo(decimal importe) => Elemento(R.Efectivo, importe);

    private static ElementoPagoDto Elemento(int idElemento, decimal importe, int idCheque = 0) =>
        new() { IdElementoCobro = idElemento, Importe = importe, IdCheque = idCheque, Descripcion = "PAGO" };
}
