using System.Security.Claims;
using System.Text.Json;
using API.Security;
using API.SERVICE.Models.Common;
using API.SERVICE.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;

namespace API.TESTS.Security;

/// <summary>Permisos por área: reglas, filtro global y coherencia de la configuración con los controllers y los roles reales.</summary>
public class PermisosPorAreaTests
{
    /// <summary>aspnet_Roles de producción (2026-10-08).</summary>
    private static readonly string[] RolesReales =
    [
        "ADMINISTRADOR", "CAJERA", "CEO", "COMERCIAL", "CONSULTOR", "DIRECTOR", "GESTION DE CLIENTES", "GESTION DE PROVEEDORES",
        "LOGISTICA", "RECAUDACION", "RESP. DE PLANTA", "RESP. DE PRODUCCION", "RESP. EQUIPO COMERCIAL", "TESORERIA Y FINANZAS",
    ];

    private static readonly PermisosOptions Opciones = CargarAppSettings();

    [Theory]
    [InlineData("CEO", "Sistema", true)]
    [InlineData("ADMINISTRADOR", "Bancos", true)]
    [InlineData("COMERCIAL", "Ventas", true)]
    [InlineData("COMERCIAL", "Compras", false)]
    [InlineData("CAJERA", "Caja", true)]
    [InlineData("CAJERA", "Ventas", true)]
    [InlineData("TESORERIA Y FINANZAS", "Pagos", true)]
    [InlineData("CONSULTOR", "Ventas", false)]
    [InlineData("DIRECTOR", "Maestros", false)]
    [InlineData("COMERCIAL", "Sistema", false)]
    public void Escritura_SegunLosRolesDelArea(string rol, string area, bool permitido) =>
        PermisosPorArea.Permite(Opciones, area, escritura: true, r => r == rol).Should().Be(permitido);

    [Theory]
    [InlineData("CONSULTOR", "Ventas", true)]
    [InlineData("CONSULTOR", "Sistema", true)]
    [InlineData("CONSULTOR", "Informes", false)]
    [InlineData("DIRECTOR", "Informes", true)]
    [InlineData("CEO", "Informes", true)]
    public void Lectura_LibreSalvoInformes(string rol, string area, bool permitido) =>
        PermisosPorArea.Permite(Opciones, area, escritura: false, r => r == rol).Should().Be(permitido);

    [Fact]
    public void SinArea_SoloRolesTotales()
    {
        PermisosPorArea.Permite(Opciones, null, escritura: true, r => r == "COMERCIAL").Should().BeFalse();
        PermisosPorArea.Permite(Opciones, null, escritura: false, r => r == "COMERCIAL").Should().BeFalse();
        PermisosPorArea.Permite(Opciones, null, escritura: true, r => r == "ADMINISTRADOR").Should().BeTrue();
    }

    [Fact]
    public void TodosLosControllers_TienenArea()
    {
        var sinArea = typeof(PermisoPorAreaFilter).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (Modulo: PermisoPorAreaFilter.Modulo(t), Nombre: t.Name[..^"Controller".Length]))
            .Where(c => !PermisosPorArea.ModulosLibres.Contains(c.Modulo) && PermisosPorArea.AreaDe(c.Modulo, c.Nombre) is null)
            .ToList();

        sinArea.Should().BeEmpty("cada controller nuevo tiene que caer en un área (o se le niega la escritura a todos salvo roles totales)");
    }

    [Fact]
    public void AppSettings_TieneTodasLasAreasYSoloRolesQueExisten()
    {
        Opciones.Areas.Keys.Should().BeEquivalentTo(AreasPermiso.Todas);
        Opciones.RolesTotales.Concat(Opciones.Areas.Values.SelectMany(a => a.Roles)).Should().OnlyContain(r => RolesReales.Contains(r),
            "un rol mal escrito deja el área sin permiso sin avisar");
    }

    [Theory]
    [InlineData("POST", "COMERCIAL", "API.Controllers.Ventas", true)]
    [InlineData("POST", "CONSULTOR", "API.Controllers.Ventas", false)]
    [InlineData("GET", "CONSULTOR", "API.Controllers.Ventas", true)]
    [InlineData("GET", "CONSULTOR", "API.Controllers.Reportes", false)]
    [InlineData("POST", "CONSULTOR", "API.Controllers.Auth", true)]
    public void Filtro_DevuelveForbiddenConElErrorEstandar(string metodo, string rol, string ns, bool permitido)
    {
        var context = Contexto(metodo, rol, ns, "Prueba");

        Filtro().OnAuthorization(context);

        if (permitido)
        {
            context.Result.Should().BeNull();
            return;
        }
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        result.Value.Should().BeOfType<ErrorCatchResponse>().Which.Message.Should().Contain("área");
    }

    [Fact]
    public void Filtro_AccionLibreDeArea_PasaAunqueElRolNoTengaElArea()
    {
        var context = Contexto("POST", "CONSULTOR", "API.Controllers.Sistema", "Usuario", new LibreDeAreaAttribute());

        Filtro().OnAuthorization(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void Filtro_AccionAnonima_NoSeControla()
    {
        var context = Contexto("POST", "CONSULTOR", "API.Controllers.Sistema", "Parametro", new AllowAnonymousAttribute());

        Filtro().OnAuthorization(context);

        context.Result.Should().BeNull();
    }

    // ---------- helpers ----------

    private static PermisoPorAreaFilter Filtro() =>
        new(Mock.Of<IOptionsMonitor<PermisosOptions>>(m => m.CurrentValue == Opciones));

    private static AuthorizationFilterContext Contexto(string metodo, string rol, string ns, string controlador, params object[] metadata)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, rol)], "Test")),
        };
        http.Request.Method = metodo;
        var descriptor = new ControllerActionDescriptor
        {
            ControllerName = controlador,
            ControllerTypeInfo = new FakeType(ns, controlador + "Controller"),
            EndpointMetadata = [.. metadata],
        };
        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), descriptor), []);
    }

    private static PermisosOptions CargarAppSettings()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "API", "appsettings.json")))
            dir = dir.Parent;
        var config = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(File.ReadAllBytes(Path.Combine(dir!.FullName, "API", "appsettings.json"))))
            .Build();
        return config.GetSection(PermisosOptions.SectionName).Get<PermisosOptions>()!;
    }

    /// <summary>Tipo con el namespace que se quiera, para simular controllers de otros módulos.</summary>
    private sealed class FakeType(string ns, string name) : System.Reflection.TypeDelegator(typeof(object))
    {
        public override string? Namespace => ns;
        public override string Name => name;
    }
}
