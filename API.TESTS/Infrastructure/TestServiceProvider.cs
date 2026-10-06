using API.DA.DbContexts;
using API.SERVICE.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace API.TESTS.Infrastructure;

/// <summary>El contenedor real de la aplicación (AddApplicationServices) con la base reemplazada por el interceptor offline.</summary>
public static class TestServiceProvider
{
    private const string OfflineConnection = "Server=offline;Database=ELRENACER;TrustServerCertificate=True";

    public static ServiceCollection CreateServices(OfflineSqlServerInterceptor interceptor)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = OfflineConnection,
                ["Jwt:Key"] = new string('k', 64),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices(configuration);

        services.RemoveAll<DbContextOptions<ElRenacerDbContext>>();
        services.RemoveAll<DbContextOptions>();
        services.AddDbContext<ElRenacerDbContext>(options => options
            .UseSqlServer(OfflineConnection)
            .AddInterceptors(interceptor));

        return services;
    }
}
