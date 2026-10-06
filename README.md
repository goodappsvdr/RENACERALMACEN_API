# ELRENACERALMACEN API (.NET 8)

API REST del ERP ELRENACERALMACEN (el WebForms vive en el repo [goodappsvdr/ELRENACERALMACEN](https://github.com/goodappsvdr/ELRENACERALMACEN)).
Convive con ese sistema: usa **la misma base**
(`ELRENACER`) y **los mismos usuarios** (ASP.NET Membership), sin cambios de esquema.

## Arquitectura (Onion GoodApps)

```
API  ──►  API.SERVICE  ──►  API.DA  ──►  (nada)
              ▲
          API.TESTS
```

| Proyecto | Contiene |
|---|---|
| `API` | Controllers (uno por recurso, agrupados por módulo), `CustomExceptionHandler`, `Program.cs` (JWT, Swagger, CORS). No ve `API.DA`. |
| `API.SERVICE` | `Interfaces/` y `Repositories/` (EF Core / LINQ), `UseCases/`, `Models/` (Dto / Display / Filter), `Mappings/`, `Domain/Exceptions/`, `Services/Cache/` (Redis), `Security/` (JWT + Membership), `DependencyInjection/`. |
| `API.DA` | Infraestructura "tonta": `ElRenacerDbContext` + entidades EF scaffoldeadas desde la base. Sin lógica. |
| `API.TESTS` | xUnit + Moq + FluentAssertions. |
| `tools/ApiGenerator` | Generador de los slices CRUD (ver abajo). |

> **Desvío respecto al documento de arquitectura:** el ejemplo del documento pone las entidades en
> `API.SERVICE/Domain` y el `DbContext` en `API.DA`, pero eso no compila (`API.DA` no puede ver
> `API.SERVICE`). Las entidades EF viven en `API.DA/Entities` junto al `DbContext`, y `API.SERVICE`
> las convierte a DTOs. La API nunca ve entidades ni el `DbContext`.

## Correr local

```bash
dotnet restore
dotnet tool restore                       # dotnet-ef (manifiesto local)

# Secretos: NUNCA en appsettings.json
dotnet user-secrets --project API set "ConnectionStrings:DefaultConnection" "Server=...;Database=ELRENACER;User ID=...;Password=...;TrustServerCertificate=True"
dotnet user-secrets --project API set "Jwt:Key" "<clave aleatoria de 32+ caracteres>"
# opcional; sin Redis se usa cache en memoria:
dotnet user-secrets --project API set "ConnectionStrings:Redis" "<host>:6380,password=...,ssl=True,abortConnect=False,connectTimeout=2000,syncTimeout=1000"

dotnet run --project API                  # Swagger en /swagger (Development)
dotnet test
```

En servidores, los mismos valores van como variables de entorno (`ConnectionStrings__DefaultConnection`,
`Jwt__Key`, `ConnectionStrings__Redis`). `Swagger:Enabled=true` habilita Swagger fuera de Development.

## Autenticación

`POST /api/Auth/login` con `{ "usuario", "password" }` → JWT (Bearer). Valida contra `aspnet_Membership`
con el mismo algoritmo que el `SqlMembershipProvider` del WebForms (SHA1 con salt), respetando
usuario aprobado/bloqueado y bloqueando tras 5 intentos fallidos en 10 minutos (igual que el `Web.config`).

El token incluye `sub` (UserId de Membership), `unique_name`, `id_usuario`, `id_sucursal` y los roles.
Todos los endpoints exigen token. En el ERP los chequeos de rol no restringen acceso (solo
controlaban menús ya comentados), así que la API tampoco filtra por rol todavía; los roles ya viajan
en el token para agregar policies cuando se definan.

## Endpoints

Uno por tabla expuesta (110), agrupados por módulo en Swagger. Ruta `api/{Recurso}` en singular:

| Método | Ruta | Cuándo |
|---|---|---|
| GET | `api/Marca?page=1&pageSize=25&q=...&idEmpresa=...` | Siempre. Paginado (máx. 200), filtros por cada `Id*`, `Estado`/`Activo`, rango por la primera `Fecha*` y búsqueda `q`. |
| GET | `api/Marca/{id}` | Tablas con clave. |
| GET | `api/Marca/lookup` | Catálogos chicos: lista completa, cacheada 12 h (`Parametro`: 15 min). |
| POST / PUT | `api/Marca`, `api/Marca/{id}` | Maestros y catálogos. |
| DELETE | `api/Marca/{id}` | Solo donde el ERP ya borra físicamente. |

Qué operación tiene cada tabla está en [`tools/ApiGenerator/entities.config`](tools/ApiGenerator/entities.config).

**Solo lectura por diseño:** comprobantes y movimientos (ventas, compras, cta. cte., recibos, órdenes
de pago, caja, bancos, stock, libros IVA) e **Items**. En el ERP esos datos solo se graban dentro de
flujos transaccionales que tocan varias tablas (p. ej. alta de ítem = ítem + sucursales + impuestos
+ historial de precios). Un POST suelto dejaría datos inconsistentes. Se habilitan al portar cada flujo.

**Nunca se exponen:** `Usuarios.Pass`, `Usuarios.Token`, `DatosEmpresa.Clave_Fiscal` y las tablas
`aspnet_*`. `Usuarios.Usuario` / `UserId` no se editan por CRUD (atan la fila a Membership).

Errores: `{ "message", "statusCode" }` (400 negocio, 401, 403, 404, 409 FK/duplicado, 500 genérico).
Validación de entrada: `ValidationProblemDetails` (400).

## Generador (`tools/ApiGenerator`)

Los 110 recursos comparten estructura, así que se generan a partir del modelo EF + `entities.config`:
`Models/*Models.g.cs`, `Mappings/`, `Interfaces/I*Repository.g.cs`, `Repositories/`, `UseCases/`,
`Controllers/` y el registro en DI. Los casos de uso generados heredan de las bases en
`API.SERVICE/UseCases/Crud` y los repositorios de `Repositories/Base`.

- **No editar `*.g.cs`.** Todo es `partial`: la lógica propia va en un archivo hermano sin `.g`
  (ej. `UseCases/Items/MarcaUseCases.cs` con `partial class CreateMarcaUseCase` que overridea `ValidateAsync`).
- Regenerar: `dotnet run --project tools/ApiGenerator`. Borra y reescribe solo `*.g.cs`.

Si cambia el esquema de la base (la cadena de conexión se pasa en el comando; no se guarda en el repo):

```bash
dotnet ef dbcontext scaffold "<cadena>" Microsoft.EntityFrameworkCore.SqlServer --project API.DA --startup-project API.DA \
  --context ElRenacerDbContext --context-dir DbContexts --context-namespace API.DA.DbContexts \
  --output-dir Entities --namespace API.DA.Entities --no-pluralize --no-onconfiguring --force -t dbo.Tabla1 -t dbo.Tabla2 ...
dotnet run --project tools/ApiGenerator
```

Los ajustes al modelo scaffoldeado (claves de tablas sin PK) van en `ElRenacerDbContext.Keys.cs`, que el scaffold no pisa.
La base se sigue modificando con scripts versionados, como hoy; esta API no agrega migraciones EF.

## Cache (estándar GoodApps Redis)

`IRedisCacheService` con `GetOrSetAsync`, valores en `byte[]` (System.Text.Json camelCase), TTL obligatorio,
lock por key contra stampede y fallback a la base si Redis falla. Keys `goodapps:elrenacer:{módulo}:{recurso}:lookup:v1`;
las escrituras del recurso invalidan su key.

## Tests

- `GeneratedQueriesTranslationTests`: ejecuta **todos** los listados (con todos los filtros activos), búsquedas
  por id y lookups contra el proveedor SQL Server real, interceptando la conexión. Falla si algún LINQ no traduce a SQL.
  No necesita base.
- Login/Membership (hash verificado contra .NET Framework, bloqueo por intentos), cache (fallback, stampede) y casos de uso CRUD.

## Pendiente (próximos tickets)

Flujos compuestos que hoy viven en los code-behind del WebForms y deben portarse como casos de uso
transaccionales: facturación y notas de crédito (AFIP), remitos, presupuestos, cobranzas/recibos
(incluye emisión masiva), órdenes de pago, compras y facturas de proveedor, ajustes y movimientos de stock,
planillas de caja, depósitos/extracciones, conciliación bancaria, ABM de ítems y alta de usuarios (Membership).
