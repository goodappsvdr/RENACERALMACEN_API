using API.DA.DbContexts;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Afip;
using API.SERVICE.Interfaces.Auth;
using API.SERVICE.Interfaces.Caja;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;
using API.SERVICE.Repositories.Auth;
using API.SERVICE.Repositories.Base;
using API.SERVICE.Repositories.Caja;
using API.SERVICE.Repositories.Clientes;
using API.SERVICE.Repositories.Sistema;
using API.SERVICE.Repositories.Ventas;
using API.SERVICE.Security;
using API.SERVICE.Services.Afip;
using API.SERVICE.Services.Cache;
using API.SERVICE.UseCases.Auth;
using API.SERVICE.UseCases.Caja;
using API.SERVICE.UseCases.Clientes;
using API.SERVICE.UseCases.Items;
using API.SERVICE.UseCases.Ventas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace API.SERVICE.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    /// <summary>Único punto de registro de la aplicación. Program.cs solo llama a este método.</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection (user-secrets / variable de entorno).");

        services.AddDbContext<ElRenacerDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql
                .EnableRetryOnFailure(maxRetryCount: 3)
                // ELRENACER tiene compatibility_level 130 (aunque el servidor sea SQL Server 2022):
                // EF no debe generar SQL que requiera un nivel mayor.
                .UseCompatibilityLevel(130)));

        AddCache(services, configuration);

        // Auth
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<MembershipOptions>(configuration.GetSection(MembershipOptions.SectionName));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<ILoginUseCase, LoginUseCase>();

        // Infraestructura de flujos transaccionales
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IServerClock, SqlServerClock>();
        services.AddScoped<IReferenciasRepository, ReferenciasRepository>();

        // Repositorios, casos de uso y lookups generados por tools/ApiGenerator.
        AddGeneratedServices(services);

        // Flujos compuestos (escritos a mano, usan los repositorios generados + sus partial)
        services.AddScoped<ICreateItemUseCase, CreateItemUseCase>();
        services.AddScoped<IUpdateItemUseCase, UpdateItemUseCase>();

        services.AddScoped<IReciboCobroRepository, ReciboCobroRepository>();
        services.AddScoped<IPlanillaCajaRepository, PlanillaCajaRepository>();
        services.AddScoped<IGetPlanillasCajaUseCase, GetPlanillasCajaUseCase>();
        services.AddScoped<IGetPlanillaCajaResumenUseCase, GetPlanillaCajaResumenUseCase>();
        services.AddScoped<IIniciarPlanillaCajaUseCase, IniciarPlanillaCajaUseCase>();
        services.AddScoped<IAbrirPlanillaCajaUseCase, AbrirPlanillaCajaUseCase>();
        services.AddScoped<IModificarPlanillaCajaUseCase, ModificarPlanillaCajaUseCase>();
        services.AddScoped<IIniciarReciboUseCase, IniciarReciboUseCase>();
        services.AddScoped<IGetComprobantesPendientesUseCase, GetComprobantesPendientesUseCase>();
        services.AddScoped<IReciboCobroWriter, ReciboCobroWriter>();
        services.AddScoped<ICreateReciboUseCase, CreateReciboUseCase>();
        services.AddScoped<IAnularReciboUseCase, AnularReciboUseCase>();
        services.AddScoped<IGetEntidadesRecibosAutomaticosUseCase, GetEntidadesRecibosAutomaticosUseCase>();
        services.AddScoped<IGenerarRecibosAutomaticosUseCase, GenerarRecibosAutomaticosUseCase>();

        services.AddScoped<IVentaRepository, VentaRepository>();
        services.AddScoped<IVentaWriter, VentaWriter>();
        services.AddScoped<IVentaAnulador, VentaAnulador>();
        services.AddScoped<IIniciarVentaInternaUseCase, IniciarVentaInternaUseCase>();
        services.AddScoped<ICreateVentaInternaUseCase, CreateVentaInternaUseCase>();
        services.AddScoped<IAnularVentaInternaUseCase, AnularVentaInternaUseCase>();

        // Factura electrónica: gateway AFIP de IDEAS SA (BaseUrl y contraseña por configuración secreta)
        services.Configure<AfipGatewayOptions>(configuration.GetSection(AfipGatewayOptions.SectionName));
        services.AddHttpClient<IAfipGateway, IdeasAfipGateway>((sp, client) =>
        {
            var afip = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AfipGatewayOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(afip.BaseUrl))
                client.BaseAddress = new Uri(afip.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(afip.TimeoutSeconds);
        });
        services.AddScoped<IAutorizarFacturaElectronicaUseCase, AutorizarFacturaElectronicaUseCase>();
        services.AddScoped<IIniciarPresupuestoUseCase, IniciarPresupuestoUseCase>();
        services.AddScoped<ICreatePresupuestoUseCase, CreatePresupuestoUseCase>();
        services.AddScoped<IUpdatePresupuestoUseCase, UpdatePresupuestoUseCase>();
        services.AddScoped<IAnularPresupuestoUseCase, AnularPresupuestoUseCase>();
        services.AddScoped<IIniciarRemitoUseCase, IniciarRemitoUseCase>();
        services.AddScoped<ICreateRemitoUseCase, CreateRemitoUseCase>();
        services.AddScoped<IAnularRemitoUseCase, AnularRemitoUseCase>();
        services.AddScoped<IGetComprobantesParaRemitirUseCase, GetComprobantesParaRemitirUseCase>();
        services.AddScoped<IGetLineasPendientesUseCase, GetLineasPendientesUseCase>();
        services.AddScoped<IIniciarNotaCreditoUseCase, IniciarNotaCreditoUseCase>();
        services.AddScoped<ICreateNotaCreditoUseCase, CreateNotaCreditoUseCase>();
        services.AddScoped<IAutorizarNotaCreditoUseCase, AutorizarNotaCreditoUseCase>();
        services.AddScoped<IAutorizarComprobanteElectronicoUseCase, AutorizarComprobanteElectronicoUseCase>();
        services.AddScoped<IIniciarFacturaElectronicaUseCase, IniciarFacturaElectronicaUseCase>();
        services.AddScoped<ICreateFacturaElectronicaUseCase, CreateFacturaElectronicaUseCase>();
        services.AddScoped<IGetFacturasPendientesAfipUseCase, GetFacturasPendientesAfipUseCase>();

        return services;
    }

    static partial void AddGeneratedServices(IServiceCollection services);

    private static void AddCache(IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redis))
        {
            // Sin Redis configurado (desarrollo local): cache en memoria con el mismo contrato.
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redis;
                options.InstanceName = string.Empty; // el prefijo ya va en CacheKeys
            });
        }

        services.AddSingleton<IRedisCacheService, RedisCacheService>();
    }
}
