using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ApiGenerator;

/// <summary>Una línea de entities.config.</summary>
public sealed record ConfigLine(string Entity, string Singular, string Module, string Operations, IReadOnlySet<string> NoWrite)
{
    public static ConfigLine Parse(string line)
    {
        var parts = line.Split('|').Select(p => p.Trim()).ToArray();
        if (parts.Length is < 4 or > 5)
            throw new FormatException($"Línea inválida en entities.config: '{line}'");

        var noWrite = parts.Length == 5
            ? parts[4].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet()
            : new HashSet<string>();

        return new ConfigLine(parts[0], parts[1], parts[2], parts[3].ToUpperInvariant(), noWrite);
    }
}

public sealed record PropSpec(string Name, Type ClrType, bool IsNullable, int? MaxLength, bool IsGenerated)
{
    public Type Underlying => Nullable.GetUnderlyingType(ClrType) ?? ClrType;

    public bool IsString => Underlying == typeof(string);

    public bool IsValueType => Underlying.IsValueType;

    /// <summary>Tipo C# tal cual en la entidad (para Display).</summary>
    public string EntityTypeName => TypeNames.Of(Underlying) + (IsNullable && (IsValueType || IsString) ? "?" : "");

    /// <summary>Tipo C# para Dto / Filter: siempre nullable (el Required lo valida ModelState).</summary>
    public string NullableTypeName => TypeNames.Of(Underlying) + "?";
}

/// <summary>Todo lo que el generador necesita saber de una entidad, ya resuelto contra el modelo EF.</summary>
public sealed class EntitySpec
{
    // Prioridad de columnas de texto para el filtro libre "Q".
    private static readonly string[] SearchColumnPriority =
    [
        "Descripcion", "Nombre", "RazonSocial", "NombreFantasia", "Apellido", "Denominacion",
        "Codigo", "CodigoBarra", "CodigoBarras", "Cuit", "Numero", "Email", "Usuario", "Detalle",
    ];

    private static readonly string[] FlagColumns = ["Estado", "Activo", "Baja", "Anulado"];

    public required ConfigLine Config { get; init; }
    public required string EntityClass { get; init; }
    public required string TableName { get; init; }
    public required IReadOnlyList<PropSpec> Properties { get; init; }
    public PropSpec? Key { get; init; }

    public string S => Config.Singular;
    public string Module => Config.Module;

    public bool CanRead => Config.Operations.Contains('R');
    public bool CanCreate => HasKey && Config.Operations.Contains('C');
    public bool CanUpdate => HasKey && Config.Operations.Contains('U');
    public bool CanDelete => HasKey && Config.Operations.Contains('D');
    public bool HasLookup => HasKey && Config.Operations.Contains('A');
    public bool HasKey => Key is not null;
    public bool IsWritable => CanCreate || CanUpdate || CanDelete;

    public string KeyType => Key is null ? "" : TypeNames.Of(Key.Underlying);

    public string RouteConstraint => KeyType switch
    {
        "long" => "long",
        "Guid" => "guid",
        _ => "int",
    };

    /// <summary>Columnas visibles en el Display.</summary>
    public IEnumerable<PropSpec> DisplayProps => Properties;

    /// <summary>Columnas que se escriben desde el Dto (sin identity / calculadas / bloqueadas). La clave solo si no es identity.</summary>
    public IReadOnlyList<PropSpec> DtoProps => Properties
        .Where(p => !p.IsGenerated && !Config.NoWrite.Contains(p.Name))
        .ToList();

    public bool DtoIncludesKey => Key is not null && DtoProps.Any(p => p.Name == Key.Name);

    public IReadOnlyList<PropSpec> IdFilterProps => Properties
        .Where(p => p != Key
                    && p.Name.Length > 2 && p.Name.StartsWith("Id", StringComparison.Ordinal) && char.IsUpper(p.Name[2])
                    && (p.Underlying == typeof(int) || p.Underlying == typeof(long) || p.Underlying == typeof(short) || p.Underlying == typeof(Guid)))
        .ToList();

    public IReadOnlyList<PropSpec> FlagFilterProps => Properties
        .Where(p => p != Key && FlagColumns.Contains(p.Name)
                    && (p.Underlying == typeof(int) || p.Underlying == typeof(bool) || p.Underlying == typeof(short)))
        .ToList();

    public PropSpec? DateFilterProp => Properties.FirstOrDefault(p =>
        p.Name.StartsWith("Fecha", StringComparison.Ordinal) &&
        (p.Underlying == typeof(DateTime) || p.Underlying == typeof(DateOnly)));

    public IReadOnlyList<PropSpec> SearchProps => SearchColumnPriority
        .Select(name => Properties.FirstOrDefault(p => p.Name == name && p.IsString))
        .Where(p => p is not null)
        .Take(4)
        .ToList()!;

    /// <summary>Orden por defecto: catálogos por texto, el resto por clave descendente (lo más nuevo primero).</summary>
    public (string Column, bool Descending) DefaultOrder
    {
        get
        {
            if (HasLookup && SearchProps.Count > 0)
                return (SearchProps[0].Name, false);
            if (Key is not null)
                return (Key.Name, true);
            return (Properties[0].Name, false);
        }
    }

    public static EntitySpec Build(ConfigLine config, IEntityType entityType, IReadOnlySet<string> hiddenProperties)
    {
        var declarationOrder = entityType.ClrType.GetProperties().Select(p => p.Name).ToList();

        var properties = entityType.GetProperties()
            .Where(p => p.PropertyInfo is not null && !hiddenProperties.Contains(p.Name))
            .OrderBy(p => declarationOrder.IndexOf(p.Name))
            .Select(p => new PropSpec(
                p.Name,
                p.ClrType,
                p.IsNullable,
                p.GetMaxLength(),
                p.ValueGenerated != ValueGenerated.Never && (p.GetDefaultValueSql() is null || p.IsPrimaryKey())))
            .ToList();

        var pk = entityType.FindPrimaryKey();
        PropSpec? key = null;
        if (pk is { Properties.Count: 1 })
            key = properties.Single(p => p.Name == pk.Properties[0].Name);
        else if (pk is not null)
            Console.WriteLine($"  ! {entityType.ClrType.Name}: clave compuesta, se expone solo el listado.");

        return new EntitySpec
        {
            Config = config,
            EntityClass = entityType.ClrType.Name,
            TableName = $"{entityType.GetSchema() ?? "dbo"}.{entityType.GetTableName()}",
            Properties = properties,
            Key = key,
        };
    }
}

public static class TypeNames
{
    public static string Of(Type type) => type switch
    {
        _ when type == typeof(int) => "int",
        _ when type == typeof(long) => "long",
        _ when type == typeof(short) => "short",
        _ when type == typeof(byte) => "byte",
        _ when type == typeof(bool) => "bool",
        _ when type == typeof(string) => "string",
        _ when type == typeof(decimal) => "decimal",
        _ when type == typeof(double) => "double",
        _ when type == typeof(float) => "float",
        _ when type == typeof(byte[]) => "byte[]",
        _ => type.Name, // DateTime, DateOnly, TimeOnly, Guid
    };
}
