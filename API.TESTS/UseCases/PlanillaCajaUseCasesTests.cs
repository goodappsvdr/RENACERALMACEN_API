using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Caja;
using API.SERVICE.Models.Caja;
using API.SERVICE.UseCases.Caja;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Moq;

namespace API.TESTS.UseCases;

/// <summary>Planillas de caja: paridad con CajasPlanilla_Agregar_Ws / CajasPlanilla_Modificar_Ws de FrmPlanillasCajaABM.</summary>
public class PlanillaCajaUseCasesTests
{
    private static readonly DateTime Ahora = new(2026, 10, 6, 20, 30, 0);

    private readonly FakePlanillaCajaRepository _repo = new();
    private readonly FakeReferencias _ref = new();
    private readonly Mock<ICurrentUser> _user = new();

    private int Abierta => _ref.Estado("CAJASPLANILLA", "ABIERTA");
    private int Cerrada => _ref.Estado("CAJASPLANILLA", "CERRADA");

    public PlanillaCajaUseCasesTests()
    {
        _user.SetupGet(u => u.IdUsuario).Returns(38);
        _user.SetupGet(u => u.IdSucursal).Returns(2);
        _repo.Usuarios[38] = new Usuarios { IdUsuario = 38, IdSucursal = 2 };
        _repo.Usuarios[1] = new Usuarios { IdUsuario = 1, IdSucursal = 1 };
        _repo.SucursalesDeUsuario.Add((38, 2));
    }

    // ---------- Apertura ----------

    [Fact]
    public async Task Abrir_UsaElPuntoDeVentaDeLaSucursalYElSaldoAnterior()
    {
        _repo.SaldoAnterior[38] = 1500;

        var r = await AbrirSut().ExecuteAsync(new AbrirPlanillaCajaDto { PuntoVenta = "0002" });

        var p = _repo.Added.Single();
        p.Should().Match<CajaPlanillas>(x =>
            x.IdUsuario == 38 && x.PuntoVenta == "0002" && x.FechaApertura == Ahora && x.FechaCierre == Ahora.Date && x.SaldoInicial == 1500
            && x.TotalIngresos == 0 && x.Diferencia == 0 && x.Estado == Abierta && x.IdSucursal == 2 && x.IdEmpresa == 1);
        r.Abierta.Should().BeTrue();
        r.Saldo.Should().Be(1500);
        _repo.Bloqueos.Should().Equal(38);
    }

    [Fact]
    public async Task Abrir_YaTieneUnaAbierta_409()
    {
        _repo.Planillas[10] = new CajaPlanillas { IdPlanillaCaja = 10, IdUsuario = 38, Estado = Abierta };

        var act = () => AbrirSut().ExecuteAsync(new AbrirPlanillaCajaDto { PuntoVenta = "0002" });

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*caja abierta*");
        _repo.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Abrir_PuntoDeVentaDeOtraSucursal_SinRolGlobal_400()
    {
        var act = () => AbrirSut().ExecuteAsync(new AbrirPlanillaCajaDto { PuntoVenta = "0001" });

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*no está habilitado*");
    }

    [Fact]
    public async Task Abrir_ParaOtroUsuario_SinRolGlobal_403()
    {
        var act = () => AbrirSut().ExecuteAsync(new AbrirPlanillaCajaDto { IdUsuario = 1, PuntoVenta = "0002" });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Abrir_CeoParaOtroUsuario_CualquierPuntoYSucursalDelUsuarioDestino()
    {
        _user.Setup(u => u.IsInRole("CEO")).Returns(true);

        await AbrirSut().ExecuteAsync(new AbrirPlanillaCajaDto { IdUsuario = 1, PuntoVenta = "0001", SaldoInicial = 200 });

        _repo.Added.Single().Should().Match<CajaPlanillas>(x => x.IdUsuario == 1 && x.IdSucursal == 1 && x.SaldoInicial == 200);
        _repo.Bloqueos.Should().Equal(1);
    }

    // ---------- Modificación / cierre ----------

    [Fact]
    public async Task Cerrar_TotalesDelDetalleYDiferenciaCalculada()
    {
        Planilla(10, apertura: Ahora.Date.AddHours(8));
        _repo.Totales[10] = (263301.10m, 1000m);

        var r = await ModificarSut().ExecuteAsync(10, new ModificarPlanillaCajaDto { SaldoInicial = 500, TotalRendido = 262000, Cerrar = true });

        _repo.Modificaciones.Single().Should().Be((10, new PlanillaCajaCierre(Ahora, 500, 263301.10m, 1000m, 262000, 801.10m, Cerrada)));
        r.Should().Match<PlanillaCajaResumenDisplay>(x => !x.Abierta && x.Saldo == 801.10m && x.Estado == Cerrada && !x.PuedeCerrar);
    }

    [Fact]
    public async Task Modificar_SinCerrar_MantieneAbierta()
    {
        Planilla(10, apertura: Ahora.Date.AddHours(8));

        await ModificarSut().ExecuteAsync(10, new ModificarPlanillaCajaDto { SaldoInicial = 100 });

        _repo.Modificaciones.Single().Datos.Estado.Should().Be(Abierta);
    }

    [Fact]
    public async Task Modificar_Cerrada_409()
    {
        Planilla(10, apertura: Ahora.Date, estado: Cerrada);

        var act = () => ModificarSut().ExecuteAsync(10, new ModificarPlanillaCajaDto { Cerrar = true });

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*ya fue cerrada*");
    }

    [Fact]
    public async Task Cerrar_DeOtroDia_SoloAdministrador()
    {
        Planilla(10, apertura: Ahora.Date.AddDays(-1).AddHours(8));

        var act = () => ModificarSut().ExecuteAsync(10, new ModificarPlanillaCajaDto { Cerrar = true });
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*administrador*");

        _user.Setup(u => u.IsInRole("ADMINISTRADOR")).Returns(true);
        await ModificarSut().ExecuteAsync(10, new ModificarPlanillaCajaDto { Cerrar = true });
        _repo.Modificaciones.Should().ContainSingle();
    }

    [Fact]
    public async Task Modificar_DeSucursalQueNoOpera_403()
    {
        Planilla(10, apertura: Ahora.Date, sucursal: 1);

        var act = () => ModificarSut().ExecuteAsync(10, new ModificarPlanillaCajaDto());

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ---------- Lecturas ----------

    [Fact]
    public async Task Resumen_SaldoCalculadoYPuedeCerrarHoy()
    {
        Planilla(10, apertura: Ahora.Date.AddHours(7), saldoInicial: 100, rendido: 0);
        _repo.Totales[10] = (900m, 50m);

        var r = await new GetPlanillaCajaResumenUseCase(_repo, _ref, new FixedServerClock(Ahora), _user.Object).ExecuteAsync(10);

        r.Should().Match<PlanillaCajaResumenDisplay>(x => x.Ingresos == 900 && x.Egresos == 50 && x.Saldo == 950 && x.Abierta && x.PuedeCerrar);
    }

    [Fact]
    public async Task Nueva_SaldoSugeridoYPuntosHabilitados()
    {
        _repo.SaldoAnterior[38] = 75;

        var r = await new IniciarPlanillaCajaUseCase(_repo, _ref, _user.Object).ExecuteAsync(null);

        r.Should().BeEquivalentTo(new NuevaPlanillaCajaDisplay(38, 75, ["0002"], false));
    }

    // ---------- helpers ----------

    private AbrirPlanillaCajaUseCase AbrirSut() => new(_repo, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private ModificarPlanillaCajaUseCase ModificarSut() => new(_repo, _ref, new InlineUnitOfWork(), new FixedServerClock(Ahora), _user.Object);

    private void Planilla(int id, DateTime apertura, int? estado = null, int sucursal = 2, decimal saldoInicial = 0, decimal rendido = 0) =>
        _repo.Planillas[id] = new CajaPlanillas
        {
            IdPlanillaCaja = id, IdUsuario = 38, IdSucursal = sucursal, PuntoVenta = "0002", FechaApertura = apertura,
            Estado = estado ?? Abierta, SaldoInicial = saldoInicial, TotalRendido = rendido,
        };
}

public sealed class FakePlanillaCajaRepository : IPlanillaCajaRepository
{
    private int _nextId = 32253;

    public Dictionary<int, CajaPlanillas> Planillas { get; } = [];
    public Dictionary<int, Usuarios> Usuarios { get; } = [];
    public Dictionary<int, decimal> SaldoAnterior { get; } = [];
    public Dictionary<int, (decimal Debe, decimal Haber)> Totales { get; } = [];
    public List<(int IdUsuario, int IdSucursal)> SucursalesDeUsuario { get; } = [];
    public Dictionary<int, string> PuntoVentaSucursal { get; } = new() { [1] = "0003", [2] = "0002" };
    public List<CajaPlanillas> Added { get; } = [];
    public List<int> Bloqueos { get; } = [];
    public List<(int Id, PlanillaCajaCierre Datos)> Modificaciones { get; } = [];

    public Task<CajaPlanillas?> GetAsync(int idPlanillaCaja, CancellationToken cancellationToken = default) =>
        Task.FromResult(Planillas.GetValueOrDefault(idPlanillaCaja));

    public Task<List<PlanillaCajaRow>> GetDeSucursalesDelUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default) =>
        Task.FromResult(Planillas.Values.Where(p => SucursalesDeUsuario.Contains((idUsuario, p.IdSucursal ?? 0)))
            .Select(p => new PlanillaCajaRow(p, null, null)).ToList());

    public Task<bool> TienePlanillaEnEstadoAsync(int idUsuario, int estado, CancellationToken cancellationToken = default) =>
        Task.FromResult(Planillas.Values.Concat(Added).Any(p => p.IdUsuario == idUsuario && p.Estado == estado));

    public Task<decimal> GetSaldoAnteriorAsync(int idUsuario, CancellationToken cancellationToken = default) =>
        Task.FromResult(SaldoAnterior.GetValueOrDefault(idUsuario));

    public Task<(decimal Debe, decimal Haber)> GetTotalesDetalleAsync(int idPlanillaCaja, CancellationToken cancellationToken = default) =>
        Task.FromResult(Totales.GetValueOrDefault(idPlanillaCaja));

    public Task<List<string>> GetPuntosVentaAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<string> { "0001", "0002", "0003", "0004", "0005" });

    public Task<string?> GetPuntoVentaSucursalAsync(int idSucursal, CancellationToken cancellationToken = default) =>
        Task.FromResult(PuntoVentaSucursal.GetValueOrDefault(idSucursal));

    public Task<Usuarios?> GetUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default) =>
        Task.FromResult(Usuarios.GetValueOrDefault(idUsuario));

    public Task<bool> OperaSucursalAsync(int idUsuario, int idSucursal, CancellationToken cancellationToken = default) =>
        Task.FromResult(SucursalesDeUsuario.Contains((idUsuario, idSucursal)));

    public void Add(CajaPlanillas planilla) => Added.Add(planilla);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var p in Added.Where(p => p.IdPlanillaCaja == 0))
            p.IdPlanillaCaja = _nextId++;
        return Task.CompletedTask;
    }

    public Task ModificarAsync(int idPlanillaCaja, PlanillaCajaCierre datos, CancellationToken cancellationToken = default)
    {
        Modificaciones.Add((idPlanillaCaja, datos));
        return Task.CompletedTask;
    }

    public Task BloquearUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default)
    {
        Bloqueos.Add(idUsuario);
        return Task.CompletedTask;
    }
}
