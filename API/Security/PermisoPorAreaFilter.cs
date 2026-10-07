using API.SERVICE.Models.Common;
using API.SERVICE.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace API.Security;

/// <summary>
/// Filtro global de permisos por área: el área sale de la carpeta del controller y los roles de la sección "Permisos"
/// de appsettings (se recarga sin reiniciar). 403 con <see cref="ErrorCatchResponse"/> si el rol no alcanza.
/// </summary>
public sealed class PermisoPorAreaFilter : IAuthorizationFilter
{
    private const string PrefijoControllers = "API.Controllers.";

    private readonly IOptionsMonitor<PermisosOptions> _opciones;

    public PermisoPorAreaFilter(IOptionsMonitor<PermisosOptions> opciones)
    {
        _opciones = opciones;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor accion
            || context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()
            || context.ActionDescriptor.EndpointMetadata.OfType<LibreDeAreaAttribute>().Any()
            || context.HttpContext.User.Identity?.IsAuthenticated != true)
            return;

        var modulo = Modulo(accion.ControllerTypeInfo);
        if (PermisosPorArea.ModulosLibres.Contains(modulo))
            return;

        var area = PermisosPorArea.AreaDe(modulo, accion.ControllerName);
        var escritura = PermisosPorArea.EsEscritura(context.HttpContext.Request.Method);
        if (PermisosPorArea.Permite(_opciones.CurrentValue, area, escritura, context.HttpContext.User.IsInRole))
            return;

        var mensaje = area is null
            ? "Este endpoint no tiene un área de permisos asignada: solo lo usan los roles con acceso total."
            : $"Su rol no tiene permiso para {(escritura ? "operar" : "consultar")} en el área {area}.";
        context.Result = new ObjectResult(new ErrorCatchResponse(mensaje, StatusCodes.Status403Forbidden))
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }

    /// <summary>"API.Controllers.Ventas" → "Ventas".</summary>
    public static string Modulo(Type controller)
    {
        var ns = controller.Namespace ?? string.Empty;
        return ns.StartsWith(PrefijoControllers, StringComparison.Ordinal) ? ns[PrefijoControllers.Length..] : ns;
    }
}
