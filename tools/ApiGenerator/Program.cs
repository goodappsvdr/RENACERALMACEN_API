// Generador de la API de ELRENACERALMACEN.
//
// Lee el modelo EF de API.DA (sin conectarse a la base) + entities.config y genera, por entidad:
// Models (Display/Dto/Filter), Mappings, I{X}Repository, {X}Repository, UseCases, Controller y el registro en DI.
//
// Uso (desde la raíz del repo):   dotnet run --project tools/ApiGenerator
// Regenerar después de: re-scaffold del DbContext o cambios en entities.config.

using API.DA.DbContexts;
using ApiGenerator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

// Columnas sensibles: nunca salen en un Display ni se aceptan en un Dto.
var hiddenProperties = new HashSet<string> { "Pass", "Password", "PasswordSalt", "Token", "ClaveFiscal" };

var repoRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : FindRepoRoot();
var configPath = Path.Combine(repoRoot, "tools", "ApiGenerator", "entities.config");

var configLines = File.ReadAllLines(configPath)
    .Select(l => l.Trim())
    .Where(l => l.Length > 0 && !l.StartsWith('#'))
    .Select(ConfigLine.Parse)
    .ToList();

var options = new DbContextOptionsBuilder<ElRenacerDbContext>()
    .UseSqlServer("Server=design-time-only") // solo para construir el modelo; no se abre conexión
    .Options;
using var context = new ElRenacerDbContext(options);
var model = context.GetService<IDesignTimeModel>().Model;

var entityTypes = model.GetEntityTypes().Where(e => !e.HasSharedClrType).ToDictionary(e => e.ClrType.Name);
var specs = new List<EntitySpec>();
foreach (var line in configLines)
{
    if (!entityTypes.TryGetValue(line.Entity, out var entityType))
        throw new InvalidOperationException($"entities.config referencia '{line.Entity}', que no existe en API.DA/Entities.");
    specs.Add(EntitySpec.Build(line, entityType, hiddenProperties));
}

var duplicated = specs.GroupBy(s => s.S).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
if (duplicated.Count > 0)
    throw new InvalidOperationException($"Nombres en singular repetidos: {string.Join(", ", duplicated)}");

var notConfigured = entityTypes.Keys.Except(configLines.Select(c => c.Entity)).Order().ToList();
Console.WriteLine($"Entidades sin exponer (no están en entities.config): {string.Join(", ", notConfigured)}");

var api = Path.Combine(repoRoot, "API");
var service = Path.Combine(repoRoot, "API.SERVICE");

// Borra todo lo generado antes (solo *.g.cs) para no dejar huérfanos si se quita una entidad.
foreach (var dir in new[] { Path.Combine(api, "Controllers"), service })
    foreach (var file in Directory.EnumerateFiles(dir, "*.g.cs", SearchOption.AllDirectories))
        File.Delete(file);

var written = 0;
foreach (var e in specs)
{
    Write(Path.Combine(service, "Models", e.Module, $"{e.S}Models.g.cs"), Templates.Models(e));
    Write(Path.Combine(service, "Mappings", e.Module, $"{e.S}Mappings.g.cs"), Templates.Mappings(e));
    Write(Path.Combine(service, "Interfaces", e.Module, $"I{e.S}Repository.g.cs"), Templates.RepositoryInterface(e));
    Write(Path.Combine(service, "Repositories", e.Module, $"{e.S}Repository.g.cs"), Templates.Repository(e));
    Write(Path.Combine(service, "UseCases", e.Module, $"{e.S}UseCases.g.cs"), Templates.UseCases(e));
    Write(Path.Combine(api, "Controllers", e.Module, $"{e.S}Controller.g.cs"), Templates.Controller(e));
}
Write(Path.Combine(service, "DependencyInjection", "ServiceCollectionExtensions.g.cs"), Templates.Registrations(specs));

Console.WriteLine($"{specs.Count} entidades, {written} archivos generados.");
foreach (var g in specs.GroupBy(s => s.Module).OrderBy(g => g.Key))
    Console.WriteLine($"  {g.Key,-18} {string.Join(", ", g.Select(s => $"{s.S}[{Ops(s)}]"))}");

return;

void Write(string path, string content)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    content = content.Replace("\r\n", "\n").Replace("\n", "\r\n");
    File.WriteAllText(path, content, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    written++;
}

static string Ops(EntitySpec s) =>
    string.Concat(
        s.CanRead ? "R" : "",
        s.CanCreate ? "C" : "",
        s.CanUpdate ? "U" : "",
        s.CanDelete ? "D" : "",
        s.HasLookup ? "A" : "");

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ELRENACERALMACEN_API.sln")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("No se encontró ELRENACERALMACEN_API.sln; pasar la carpeta raíz del repo como argumento.");
}
