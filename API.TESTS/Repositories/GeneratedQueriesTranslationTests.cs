using System.Reflection;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Models.Common;
using API.TESTS.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace API.TESTS.Repositories;

/// <summary>
/// Recorre todos los casos de uso generados y los ejecuta contra el proveedor SQL Server real
/// (sin conexión): falla si algún filtro, orden o búsqueda no se puede traducir a SQL.
/// </summary>
public class GeneratedQueriesTranslationTests
{
    private static readonly ServiceCollection Services = TestServiceProvider.CreateServices(new OfflineSqlServerInterceptor());

    public static IEnumerable<object[]> ListUseCases() => UseCasesMatching("List");

    public static IEnumerable<object[]> ByIdUseCases() => UseCasesMatching("ById");

    public static IEnumerable<object[]> LookupUseCases() => UseCasesMatching("Lookup");

    [Fact]
    public void Registrations_CoverAllGeneratedEntities_AtLeastOneHundredListUseCases()
    {
        ListUseCases().Should().HaveCountGreaterThanOrEqualTo(100);
    }

    [Theory]
    [MemberData(nameof(ListUseCases))]
    public async Task ListUseCase_WithEveryFilterSet_TranslatesToSql(string useCaseInterface)
    {
        var (useCase, interceptor, scope) = Resolve(useCaseInterface);
        using var _ = scope;

        var execute = useCase.GetType().GetMethod("ExecuteAsync")!;
        var filter = BuildFilterWithEveryValueSet(execute.GetParameters()[0].ParameterType);

        var task = (Task)execute.Invoke(useCase, [filter, CancellationToken.None])!;
        await task;

        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        result.GetType().GetProperty(nameof(PagedResult<object>.TotalCount))!.GetValue(result).Should().Be(0);

        interceptor.Commands.Should().HaveCount(2, "se ejecuta un COUNT y una página");
        var hasFilters = filter.GetType().GetProperties().Any(p => p.DeclaringType != typeof(PagedQuery));
        if (hasFilters)
            interceptor.Commands.Should().AllSatisfy(sql => sql.Should().Contain("WHERE"));
        interceptor.Commands.Last().Should().Contain("OFFSET").And.Contain("FETCH NEXT");
    }

    [Theory]
    [MemberData(nameof(ByIdUseCases))]
    public async Task ByIdUseCase_WhenRowDoesNotExist_ThrowsNotFound(string useCaseInterface)
    {
        var (useCase, interceptor, scope) = Resolve(useCaseInterface);
        using var _ = scope;

        var execute = useCase.GetType().GetMethod("ExecuteAsync")!;
        var keyType = execute.GetParameters()[0].ParameterType;

        var act = async () => await (Task)execute.Invoke(useCase, [SampleValue(keyType), CancellationToken.None])!;

        await act.Should().ThrowAsync<NotFoundException>();
        interceptor.Commands.Should().ContainSingle();
    }

    [Theory]
    [MemberData(nameof(LookupUseCases))]
    public async Task LookupUseCase_TranslatesToSql(string useCaseInterface)
    {
        var (useCase, interceptor, scope) = Resolve(useCaseInterface);
        using var _ = scope;

        var execute = useCase.GetType().GetMethod("ExecuteAsync")!;
        await (Task)execute.Invoke(useCase, [CancellationToken.None])!;

        interceptor.Commands.Should().ContainSingle().Which.Should().Contain("SELECT TOP");
    }

    private static IEnumerable<object[]> UseCasesMatching(string kind) =>
        Services
            .Select(d => d.ServiceType)
            .Where(t => t.IsInterface && t.Name.StartsWith("IGet", StringComparison.Ordinal) && t.Name.EndsWith($"{kind}UseCase", StringComparison.Ordinal))
            .Select(t => new object[] { t.FullName! })
            .OrderBy(x => (string)x[0]);

    private static (object UseCase, OfflineSqlServerInterceptor Interceptor, IServiceScope Scope) Resolve(string interfaceName)
    {
        // Contenedor propio por test: interceptor y cache limpios.
        var interceptor = new OfflineSqlServerInterceptor();
        var provider = TestServiceProvider.CreateServices(interceptor).BuildServiceProvider();
        var scope = provider.CreateScope();
        var type = Services.Select(d => d.ServiceType).Single(t => t.FullName == interfaceName);
        return (scope.ServiceProvider.GetRequiredService(type), interceptor, scope);
    }

    private static object BuildFilterWithEveryValueSet(Type filterType)
    {
        var filter = Activator.CreateInstance(filterType)!;
        foreach (var property in filterType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.DeclaringType != typeof(PagedQuery) && p.CanWrite))
        {
            property.SetValue(filter, SampleValue(property.PropertyType));
        }
        return filter;
    }

    private static object SampleValue(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string)) return "abc";
        if (t == typeof(int)) return 1;
        if (t == typeof(long)) return 1L;
        if (t == typeof(short)) return (short)1;
        if (t == typeof(bool)) return true;
        if (t == typeof(Guid)) return Guid.NewGuid();
        if (t == typeof(DateTime)) return new DateTime(2026, 1, 15);
        if (t == typeof(DateOnly)) return new DateOnly(2026, 1, 15);
        throw new NotSupportedException($"Tipo de filtro no contemplado en el test: {t.Name}");
    }
}
