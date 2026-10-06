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
de pago, caja, bancos, stock, libros IVA). En el ERP esos datos solo se graban dentro de flujos
transaccionales que tocan varias tablas; un POST suelto dejaría datos inconsistentes. Se habilitan a
medida que se porta cada flujo (ver "Flujos transaccionales").

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
- Flujos transaccionales: lógica de cada caso de uso con repositorios mockeados + traducción a SQL de sus consultas propias.
  La transacción real (`EfUnitOfWork`) no tiene test automático: necesita una base SQL Server.

## Flujos transaccionales

Operaciones que en el WebForms usan `IniciaTransaccion` / `FinalizaTransaccion` / `CancelaTransaccion`.
Se escriben a mano (no las genera el generador) con este patrón:

- **Caso de uso** en `UseCases/{Módulo}/`, que envuelve todo en `IUnitOfWork.ExecuteInTransactionAsync`:
  commit si termina bien, rollback si lanza. Es compatible con los reintentos de EF (`EnableRetryOnFailure`):
  ante un error transitorio se reintenta el bloque entero, así que **todas las lecturas y escrituras van adentro del delegado**.
- **Operaciones extra del repositorio** en un `partial` del repositorio generado (`Interfaces/{Módulo}/I{X}Repository.cs`
  + `Repositories/{Módulo}/{X}Repository.cs`).
- **Reglas de negocio** en `Domain/{Módulo}/`.
- **Endpoints** en un `partial` del controller generado (`Controllers/{Módulo}/{X}Controller.cs`).
- Fecha/hora: `IServerClock` (= `GETDATE()` del servidor, como `FechaHoraServidor()`), porque las columnas legacy guardan hora local.
- Usuario: `ICurrentUser` (sale del JWT; `IdUsuario` = `Usuarios.ID_Usuario`).
- Parámetros: `IParametroRepository.GetValorAsync` (= `SingletonParametro()`).

### ABM de ítems (`POST /api/Item`, `PUT /api/Item/{id}`)

Port de `Items_Agregar_Ws` / `Items_Modificar_Ws` (`FrmItemsABM`).

- **Alta:** `Items` + `ItemsImpuestos` + una fila de `ItemsSucursales` por sucursal (con su stock y estado).
- **Modificación:** historial en `ItemsPreciosActualizacion` (siempre la primera vez; después solo si cambió el precio a 2 decimales),
  datos del ítem, impuesto y datos por sucursal. El stock (del ítem y de cada sucursal) solo se pisa si el parámetro
  `CAMBIASTOCK/CAMBIASTOCK` vale 1.

Comportamientos del ERP que se **mantienen** a propósito:
- `Items.Neto` guarda el **costo** (la tabla `Items` no tiene columna `Costo`); el neto real va en `ItemsSucursales.Neto`.
- Valores fijos: `ID_Empresa = 1`, `TieneDetalle = 1`, `UnidadesXBulto = 1`, `EsDolar = 0`; `CuentaDebe/CuentaHaber/MtsKgs` = 1 en el alta y 0 en la modificación.
- Alícuota según `ID_Impuesto` hardcodeada: 1 = 21 %, 2 = 10,5 %, 3 = 27 %, 4 = 0 %.
- En la modificación, el precio anterior para el historial es el de la **primera** fila de `ItemsSucursales` del ítem,
  y una sucursal sin fila para el ítem se ignora (no se crea).

Diferencias **intencionales** con el ERP:
- `ID_Impuesto` desconocido: el ERP grababa alícuota 0; acá se rechaza (400).
- Descripción de más de 50 caracteres: el SP de modificación del ERP la truncaba en silencio; acá se rechaza (400).
- Código de balanza (empieza con "2"): el ERP lo cortaba a 7 dígitos solo al modificar (y fallaba si tenía menos de 7);
  acá se corta en alta y modificación, y solo si es más largo.
- Ítem sin ninguna fila en `ItemsSucursales`: el ERP fallaba con un error genérico; acá devuelve 400 con mensaje claro.
- El usuario tiene que tener fila en `Usuarios` para modificar (lo pide el historial de precios); si no, 403.

### Recibos de cobro (`/api/EntidadRecibo`)

Port de `Agregar_Ws` / `Editar_Ws` / `IniciarPuntoVenta_WS` / `BuscarComprobantes_WS` (`FrmRecibos`).

| Endpoint | Qué hace |
|---|---|
| `GET nuevo` | Planilla de caja abierta del usuario, punto de venta y número sugerido. |
| `GET comprobantes-pendientes?idEntidad=` | Comprobantes de la cta. cte. del cliente para imputar (saldo, vencimiento, días de mora, tasa de interés del cliente). |
| `POST` | Alta: recibo + imputación a comprobantes (cta. cte. y estados) + cta. cte. del recibo + formas de pago (caja, cheques en cartera, depósitos/tarjetas en bancos, retenciones) + detalle + movimientos + numeración. |
| `POST {id}/anular` | Anulación: recibo, detalle, caja, cheques, bancos y retenciones anulados; devuelve el saldo a los comprobantes imputados y les restaura el estado; anula la cta. cte. del recibo. |

Los IDs de tipos de comprobante, elementos de cobro, estados y categorías se resuelven por nombre contra
`Parametros` / `Estados` / `Categorias` (como `SingletonParametro` / `ValorEstado` / `ValorCategoria`), con memoria por request.

Diferencias **intencionales** con el ERP:
- **Planilla de caja y número del recibo los resuelve el servidor** (el ERP los recibía del navegador). El número se reserva con
  `UPDATE ... OUTPUT` dentro de la transacción: dos cajeros no pueden obtener el mismo número. Con `NUMERACION/REC = 1` se
  respeta la numeración manual.
- **Razón social, categoría de IVA y CUIT salen de la ficha del cliente**, no del request.
- **Totales calculados en el servidor** con las fórmulas del formulario: recibo = formas de pago + recargo de tarjeta;
  comprobantes = importes imputados + recargo de tarjeta.
- **Cada comprobante imputado se valida:** tiene que ser del cliente, estar pendiente en la cta. cte. y su importe tiene
  que ser saldo pendiente + interés (±0,01). El ERP confiaba en el importe que mandaba el navegador.
- Imputar una factura cuando el importe del recibo ya se consumió: el ERP la marcaba "cobrado parcial" con 0 imputado; acá se rechaza.
- Tipo de comprobante no imputable: el ERP lo ignoraba en silencio; acá se rechaza.
- Anular un recibo ya anulado devuelve 409 (el ERP devolvía status "300").

Comportamientos del ERP que se **mantienen** y conviene revisar con negocio (posibles bugs):
- La cta. cte. del recibo guarda el interés **del último** comprobante imputado, no la suma.
- Al anular, se devuelve al comprobante `ImporteRecibo - InteresAplicado`, donde el interés es el **acumulado** del comprobante (no solo el de este recibo).
- FC/COM se marcan PAGADO en `DocumentosCliente` al cobrar, pero la anulación los revierte en `DocumentosProveedor`.
- El recargo de tarjeta suma al total del recibo pero no entra en la caja ni en el detalle.
- Valores hardcodeados en el ERP: estado 56 para el recibo nuevo, estado 48 y tipos 4/12/9 (saldo invertido) y 11/3 (mora) en comprobantes pendientes.

## Pendiente (próximos tickets)

Flujos compuestos que hoy viven en los code-behind del WebForms y deben portarse como casos de uso
transaccionales: facturación y notas de crédito (AFIP), remitos, presupuestos, cobranzas/recibos
(alta y anulación ya portadas; falta la emisión masiva de FrmRecibosAutomaticos), órdenes de pago, compras y facturas de proveedor, ajustes y movimientos de stock,
planillas de caja, depósitos/extracciones, conciliación bancaria y alta de usuarios (Membership).
