namespace API.SERVICE.Security;

/// <summary>Áreas funcionales en las que se agrupan los endpoints para los permisos por rol.</summary>
public static class AreasPermiso
{
    public const string Ventas = "Ventas";
    public const string Cobranzas = "Cobranzas";
    public const string Caja = "Caja";
    public const string Compras = "Compras";
    public const string Pagos = "Pagos";
    public const string Bancos = "Bancos";
    public const string Stock = "Stock";
    public const string Maestros = "Maestros";
    public const string Informes = "Informes";
    public const string Sistema = "Sistema";

    public static readonly IReadOnlyList<string> Todas = [Ventas, Cobranzas, Caja, Compras, Pagos, Bancos, Stock, Maestros, Informes, Sistema];
}

/// <summary>Sección "Permisos" de appsettings: qué roles escriben en cada área. Se recarga en caliente (IOptionsMonitor).</summary>
public sealed class PermisosOptions
{
    public const string SectionName = "Permisos";

    /// <summary>Roles con acceso total a todas las áreas.</summary>
    public List<string> RolesTotales { get; set; } = [];

    public Dictionary<string, AreaPermisoOptions> Areas { get; set; } = [];
}

public sealed class AreaPermisoOptions
{
    /// <summary>Roles que pueden grabar, modificar o anular en el área (además de <see cref="PermisosOptions.RolesTotales"/>).</summary>
    public List<string> Roles { get; set; } = [];

    /// <summary>Si también las consultas (GET) quedan limitadas a esos roles. Si no, cualquier usuario logueado consulta.</summary>
    public bool LecturaRestringida { get; set; }
}

/// <summary>
/// A qué área pertenece cada controller y si un usuario puede usarlo. El área sale de la carpeta del controller
/// (API.Controllers.{Módulo}); unos pocos controllers se reasignan a mano. Qué roles tiene cada área es configuración.
/// </summary>
public static class PermisosPorArea
{
    /// <summary>Módulos sin control por área (el login es anónimo).</summary>
    public static readonly IReadOnlySet<string> ModulosLibres = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Auth" };

    private static readonly Dictionary<string, string> Modulos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ventas"] = AreasPermiso.Ventas,
        ["Clientes"] = AreasPermiso.Cobranzas,
        ["CuentasCorrientes"] = AreasPermiso.Cobranzas,
        ["Caja"] = AreasPermiso.Caja,
        ["Compras"] = AreasPermiso.Compras,
        ["Proveedores"] = AreasPermiso.Pagos,
        ["Bancos"] = AreasPermiso.Bancos,
        ["Stock"] = AreasPermiso.Stock,
        ["Items"] = AreasPermiso.Maestros,
        ["Entidades"] = AreasPermiso.Maestros,
        ["Geografia"] = AreasPermiso.Maestros,
        ["Transportes"] = AreasPermiso.Maestros,
        ["Reportes"] = AreasPermiso.Informes,
        ["Afip"] = AreasPermiso.Informes,
        ["Empresas"] = AreasPermiso.Sistema,
        ["Comprobantes"] = AreasPermiso.Sistema,
        ["Miscelaneas"] = AreasPermiso.Sistema,
        ["Sistema"] = AreasPermiso.Sistema,
    };

    /// <summary>Controllers cuya área no es la de su carpeta (nombre sin el sufijo "Controller").</summary>
    private static readonly Dictionary<string, string> Controladores = new(StringComparer.OrdinalIgnoreCase)
    {
        // Catálogos que usan compras y órdenes de pago: no son informes.
        ["Retencion"] = AreasPermiso.Maestros,
        ["OtroTributo"] = AreasPermiso.Maestros,
        ["AreaDeContacto"] = AreasPermiso.Maestros,
        ["Motivo"] = AreasPermiso.Maestros,
    };

    /// <summary>Área del controller, o null si no está mapeado (se trata como "solo roles totales" al escribir).</summary>
    public static string? AreaDe(string modulo, string controlador) =>
        Controladores.TryGetValue(controlador, out var area) ? area : Modulos.GetValueOrDefault(modulo);

    public static bool EsEscritura(string metodoHttp) =>
        !HttpMethods.IsGet(metodoHttp) && !HttpMethods.IsHead(metodoHttp) && !HttpMethods.IsOptions(metodoHttp);

    /// <summary>Si el usuario puede usar un endpoint del área. Roles totales: todo. Lectura: libre salvo área con lectura restringida.</summary>
    public static bool Permite(PermisosOptions opciones, string? area, bool escritura, Func<string, bool> tieneRol)
    {
        if (opciones.RolesTotales.Any(tieneRol))
            return true;

        var config = area is null ? null : opciones.Areas.FirstOrDefault(a => string.Equals(a.Key, area, StringComparison.OrdinalIgnoreCase)).Value;
        if (!escritura && config?.LecturaRestringida != true && area is not null)
            return true;
        return config is not null && config.Roles.Any(tieneRol);
    }

    private static class HttpMethods
    {
        public static bool IsGet(string m) => string.Equals(m, "GET", StringComparison.OrdinalIgnoreCase);
        public static bool IsHead(string m) => string.Equals(m, "HEAD", StringComparison.OrdinalIgnoreCase);
        public static bool IsOptions(string m) => string.Equals(m, "OPTIONS", StringComparison.OrdinalIgnoreCase);
    }
}
