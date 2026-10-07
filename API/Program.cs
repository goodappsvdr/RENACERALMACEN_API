using System.Text;
using API.Middleware;
using API.Security;
using API.SERVICE.DependencyInjection;
using API.SERVICE.Interfaces;
using API.SERVICE.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// API solo ve API.SERVICE: DbContext, repositorios y casos de uso se registran ahí.
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key ausente o menor a 32 caracteres. Configurarla con user-secrets o variable de entorno Jwt__Key.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "unique_name",
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
    });
builder.Services.AddAuthorization();

builder.Services.Configure<PermisosOptions>(builder.Configuration.GetSection(PermisosOptions.SectionName));
builder.Services.AddControllers(options => options.Filters.Add<PermisoPorAreaFilter>());

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "ELRENACERALMACEN API", Version = "v1" });

    // Agrupa por módulo ([Tags] en cada controller) y ordena alfabéticamente.
    options.TagActionsBy(api =>
    [
        api.ActionDescriptor.EndpointMetadata.OfType<TagsAttribute>().SelectMany(t => t.Tags).FirstOrDefault()
            ?? api.ActionDescriptor.RouteValues["controller"]!,
    ]);
    options.OrderActionsBy(api => $"{api.RelativePath}_{api.HttpMethod}");
    options.CustomSchemaIds(type => type.FullName!.Replace("API.SERVICE.Models.", string.Empty).Replace('+', '.'));

    var xml = Path.Combine(AppContext.BaseDirectory, "API.xml");
    if (File.Exists(xml))
        options.IncludeXmlComments(xml);

    var bearer = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Token obtenido en POST /api/Auth/login",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", bearer);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = [] });
});

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
// El middleware de errores va antes de UseAuthorization (estándar GoodApps).
app.UseMiddleware<CustomExceptionHandler>();
app.UseAuthorization();

app.MapControllers();

app.Run();
