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
dotnet user-secrets --project API set "AfipGateway:BaseUrl" "<url del gateway>"
dotnet user-secrets --project API set "AfipGateway:Password" "<contraseña del certificado>"
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
Todos los endpoints exigen token.

### Permisos por rol

En el ERP los roles casi no restringen nada (solo CEO / ADMINISTRADOR ven el menú de informes). La API agrupa los
endpoints en **áreas** y la sección `Permisos` de `appsettings.json` dice qué roles **operan** (POST / PUT / DELETE) en
cada una. Se recarga sin reiniciar: negocio puede ajustar roles editando el archivo.

| Área | Controllers (carpeta) |
|---|---|
| Ventas | `Ventas` |
| Cobranzas | `Clientes` (recibos, cheques de terceros), `CuentasCorrientes` |
| Caja | `Caja` |
| Compras | `Compras` |
| Pagos | `Proveedores` (órdenes de pago) |
| Bancos | `Bancos` (incluye depósitos y extracciones) |
| Stock | `Stock` (incluye transferencias entre sucursales) |
| Maestros | `Items`, `Entidades`, `Geografia`, `Transportes`, y `Retencion`, `OtroTributo`, `AreaDeContacto`, `Motivo` |
| Informes | `Reportes`, `Afip` (libros IVA) — **también la consulta** está restringida (`LecturaRestringida`) |
| Sistema | `Sistema`, `Empresas`, `Comprobantes`, `Miscelaneas` |

- `RolesTotales` (CEO, ADMINISTRADOR) pueden todo. Las consultas (GET) son libres para cualquier usuario logueado,
  salvo en Informes; así el front puede cargar combos (sucursales, estados, puntos de venta) con cualquier rol.
- Sin permiso: **403** con el `ErrorCatchResponse` estándar. Un controller sin área asignada solo lo usan los roles totales;
  un test exige que todos tengan área y que los roles de la configuración existan en `aspnet_Roles`.
- `[LibreDeArea]` exime una acción (hoy solo `POST /api/Usuario/cambiar-password`, sobre el propio usuario). `Auth` no se controla.
- Se suman a los controles propios de cada flujo: administrar usuarios y roles sigue siendo solo de ADMINISTRADOR, la caja y las
  transferencias de stock exigen además operar la sucursal.

Asignación inicial (a validar con negocio): Ventas → COMERCIAL, RESP. EQUIPO COMERCIAL, GESTION DE CLIENTES; Cobranzas → esos +
CAJERA, RECAUDACION, TESORERIA Y FINANZAS; Caja → CAJERA, RECAUDACION, TESORERIA Y FINANZAS; Compras → GESTION DE PROVEEDORES,
RESP. DE PLANTA; Pagos → GESTION DE PROVEEDORES, TESORERIA Y FINANZAS; Bancos → TESORERIA Y FINANZAS; Stock → LOGISTICA,
RESP. DE PLANTA, RESP. DE PRODUCCION; Maestros → COMERCIAL, GESTION DE CLIENTES, GESTION DE PROVEEDORES; Informes → DIRECTOR;
Sistema → solo roles totales. CONSULTOR queda de solo lectura. Hoy solo operan un CEO y un ADMINISTRADOR, que tienen todo.

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

**Concurrencia:** cada recibo toma un lock exclusivo por cliente (`sp_getapplock`, dueño la transacción). Dos recibos
simultáneos del mismo cliente (de distintos usuarios o instancias de la API) se serializan, y el segundo ve los
comprobantes ya cancelados. Si no obtiene el lock en 15 s responde 409.

### Emisión masiva de recibos (`/api/EntidadRecibo/automaticos`)

Port de `FrmRecibosAutomaticos` (ticket DES-1723).

| Endpoint | Qué hace |
|---|---|
| `GET automaticos/entidades` | Clientes principales con saldo a cobrar (`RecibosAutomaticos_BuscarEntidadesPrincipales`). |
| `POST automaticos` | `{ "idsEntidad": [...] }` (hasta 10): un recibo en efectivo por cliente que cancela todos sus comprobantes pendientes con interés 0. Devuelve el resultado de cada cliente. |

Cada recibo se graba con **el mismo caso de uso que el recibo manual** (`CreateReciboUseCase`), en su propia
transacción: si un cliente falla se informa en su resultado y se sigue con los demás. Se mantienen las reglas del ERP:
sucursal LOCAL del usuario, planilla abierta, no se permite con numeración manual, el saldo de la grilla se recalcula en
el servidor y tiene que coincidir con los comprobantes pendientes, los clientes con comprobantes de proveedor (OP/FC/COM)
o de tipos desconocidos se hacen a mano, y se imputa primero lo que está a favor del cliente y después las facturas.

Diferencia con el ERP: el `SyncLock` del WebForms solo serializaba dentro de un proceso; acá lo resuelve el lock por cliente
en la base, que también cubre varias instancias de la API y los recibos manuales.

### Comprobante interno de venta — VEN (`/api/DocumentoCliente`)

Port de `Agregar_Ws` / `GenerarRecibo` / `Editar_Ws` / `AnularRecibo` / `IniciarPuntoVenta_WS` (`FrmFacturas`). Letra X, sin AFIP.

| Endpoint | Qué hace |
|---|---|
| `GET interno/nuevo` | Planilla de caja abierta, punto de venta y número sugerido (`NUMERACION/RV` = 1 → manual). |
| `POST interno` | Cabecera, observación, vencimiento (`DiasInteres` del cliente), detalle, stock, presupuestos/remitos facturados, cta. cte. y, si vienen formas de pago, el **recibo del cobro en el momento** en la misma transacción. |
| `POST {id}/anular` | Devuelve stock (o el saldo de los remitos/presupuestos), ofertas y números de serie; anula detalle, caja, cheques, bancos, retenciones, cta. cte., el recibo del cobro en el momento y el comprobante. Solo VEN en estado 42/104. |

Stock por línea, como el ERP: venta directa → descuenta stock y registra el movimiento; desde un **remito** → no mueve stock
(ya lo movió el remito) y consume su saldo pendiente; desde un **presupuesto** → consume su saldo y descuenta stock.

El recibo del cobro en el momento usa el mismo `IReciboCobroWriter` que el recibo manual (validaciones, caja, cheques,
bancos, retenciones y numeración incluidas).

Reglas que el ERP aplicaba **solo en el navegador** y ahora valida el servidor:
- Cliente **sin cta. cte. habilitada** → la venta tiene que tener formas de pago (contado).
- Cliente **con cta. cte.**: si saldo (suma de `Total2`) + venta supera `LimiteCtaCte` → 409, salvo `confirmarExcesoLimite: true`
  (equivale al "¿desea continuar igual?" de la pantalla).

Diferencias **intencionales** con el ERP:
- La oferta por agotamiento se descuenta/devuelve **dentro** de la transacción (el ERP lo hacía fuera: si la venta fallaba, la oferta quedaba descontada).
- Cada línea graba su propio `Otros` (el ERP grababa el `Otros` de la cabecera en todas las líneas).
- La observación completa va a `DocumentosClienteObservaciones`; en la cabecera se trunca a 50 como hacía el SP.
- Planilla y número los resuelve el servidor (`UPDATE ... OUTPUT`), con lock por cliente.
- Razón social, CUIT, etc. pueden venir en el request (consumidor final con nombre); si no, salen de la ficha.

Pendiente de definir con negocio:
- **Importes de líneas y totales se graban como llegan** (igual que el ERP: los calcula la pantalla). El cálculo de
  recargo/descuento global del JavaScript reutiliza variables y no es confiable como referencia; conviene definir la
  fórmula oficial y validarla en el servidor.
- `ItemsOfertas_*Cantidad_Disponible` actualiza `OfertasAgotamiento` de **todas** las sucursales de la oferta (se mantiene).
- En la anulación sin remitos el ERP indexaba la cabecera por número de línea (`ods22.Rows(i)`); acá cada línea usa su propio detalle.

### Factura electrónica — FV (`/api/DocumentoCliente`)

Port de `Agregar_Ws` / `IniciarPuntoVenta_WS` (`FrmFacturasAFIP`) y del cliente `API_GA_AFIP.vb`. Letras A, B y C.

| Endpoint | Qué hace |
|---|---|
| `GET electronica/nuevo?letra=A&idSucursal=1` | Planilla abierta, punto de venta AFIP de la sucursal y próximo número según AFIP. 503 si AFIP no responde. |
| `POST electronica` | **Fase 1:** graba la factura completa (igual que el interno: detalle, stock, cta. cte., recibo) con `CAE = "0"` y la confirma. **Fase 2:** pide el CAE. 201 autorizada · 202 AFIP no respondió (queda pendiente) · 422 AFIP la rechazó · 503 AFIP no respondió antes de grabar (no se grabó nada). |
| `POST {id}/autorizar` | Reintento de una pendiente. |
| `GET electronica/pendientes` | Facturas grabadas que todavía no tienen CAE. |

**Por qué dos fases:** el ERP pedía el CAE con la transacción abierta y confirmaba después. Si AFIP aprobaba pero el commit fallaba,
quedaba un **CAE emitido sin factura** (y el número consumido en AFIP), y las tablas quedaban bloqueadas durante la llamada.
Ahora:
- **Aprobada** → transacción corta: CAE, número definitivo (el que asigna AFIP), código de barras, QR (imagen PNG en base64, como el ERP)
  y libro de IVA ventas + `TxtVentasAlicuotas` si la sucursal es Responsable Inscripto.
- **Rechazada** → se revierte la factura con el mismo anulador del interno (stock, cta. cte., cobro) y queda anulada con la
  observación `RECHAZADO POR AFIP: ...`.
- **Sin respuesta** → queda pendiente (`CAE = "0"`, la misma marca que ya usaba el ERP: no hace falta ningún estado nuevo).
- **Reintento** → antes de pedir otro CAE se consulta el último autorizado en AFIP: si ya alcanzó el número esperado, el intento
  anterior pudo haberse emitido y responde 409 para verificar a mano (evita facturas duplicadas en AFIP).
- Lock por factura (`sp_getapplock` de sesión) mientras se pide el CAE: dos reintentos simultáneos no pueden emitir dos veces.

Se mantiene del ERP: sucursal RI → `GenerateVoucher` con IVA por alícuota (21 % → Id 5, 10,5 % → 4, 27 % → 6, exento en `ImpOpEx`)
y descuento global aplicado a cada alícuota; si no es RI → `GenerateVoucherMono` sin IVA. Tipo de documento por largo del CUIT
(vacío / 11 dígitos / DNI). Certificado por sucursal (`API/CARPETA` y `API/CERTFICADO` con `ID_Empresa` = sucursal).
`DocumentosCliente_Modificar_DatosAfip` no actualiza `ID_PuntoVenta` (el SP hace `ID_PuntoVenta = ID_PuntoVenta`): se mantiene.

Diferencias **intencionales**:
- Con neto exento, el ERP ponía en 0 el **IVA del 27 %** (bug); acá se pone en 0 el IVA del exento.
- El QR usa el número que asignó AFIP (el ERP usaba el estimado al abrir la pantalla).
- Letra inválida se rechaza antes de grabar (el ERP fallaba después de insertar).

**Configuración** (`AfipGateway`): `BaseUrl` (el ERP usa `http://ideassa.com.ar/AFIP_GA_API_48/api`), `Password` del certificado
(**solo** por user-secrets / variable de entorno `AfipGateway__Password`), `IsProdEnvironment` (**false por defecto**: homologación;
para emitir facturas reales hay que ponerlo en true explícitamente) y `TimeoutSeconds`.

**Sin probar contra AFIP:** el cliente del gateway está probado con un HTTP simulado que reproduce los JSON de `API_GA_AFIP.vb`.
Falta validarlo en homologación (formato de fechas: el ERP mandaba `/Date(...)/` y acá va ISO 8601).

### Nota de crédito electrónica — NC (`/api/DocumentoCliente`)

Port de `Agregar_Ws` / `IniciarPuntoVenta_WS` (`FrmNotasCreditoAFIP`). Se hace siempre sobre una factura FV autorizada; la letra,
la sucursal y el cliente salen de la factura.

| Endpoint | Qué hace |
|---|---|
| `GET {idFactura}/nota-credito/nuevo` | Planilla abierta, punto de venta AFIP y próximo número de NC según AFIP. |
| `POST nota-credito` | **Fase 1:** graba el borrador (cabecera, detalle y relación factura → NC en `DocumentosClienteRelacion`) con `CAE = "0"` y **sin efectos**. **Fase 2:** pide el CAE con la factura como comprobante asociado (`GenerateVoucherCbteAsoc` / `GenerateVoucherCbteAsocMono`) y, si AFIP aprueba, aplica los efectos. 201 · 202 · 422 · 503 como la factura. |
| `POST {id}/autorizar` | El mismo endpoint de la factura: según el tipo del comprobante reintenta la FV o la NC. |
| `GET electronica/pendientes` | Incluye las NC pendientes. |

Cada línea referencia una línea de la factura (`idDocumentoClienteDetalle`): ítem, descripción, impuesto y lista salen de ella; cantidad e
importes vienen en el request (como en el ERP).

Efectos al aprobar (misma transacción que el CAE): si la factura vino de remitos/presupuestos se les devuelve el saldo; si no, se suma
el stock de cada línea, se registra el movimiento y se descuenta el saldo de la línea de la factura. Cta. cte. de la NC a favor del cliente
(saldo y `Total2` negativos, vencimiento a 30 días) con su movimiento. Si la factura se había cobrado con recibos, **se anulan esos
recibos** (como el ERP); si no, la factura pasa a CANCELADO. Se liberan los números de serie de la factura. Libro de IVA ventas como NC
si la sucursal es RI; QR y código de barras (con la fecha de emisión, como el ERP).

Diferencias **intencionales** con el ERP:
- **Los efectos se aplican recién con el CAE.** El ERP los aplicaba antes de llamar a AFIP con la transacción abierta; como incluyen
  anular recibos (que no se puede deshacer limpiamente), acá un rechazo solo anula el borrador y lo desvincula de la factura.
- **Validaciones nuevas en el servidor:** la factura tiene que ser FV, no anulada y con CAE; cada línea tiene que ser de esa factura y
  no acreditar más cantidad que la facturada; el total de la NC más las NC previas no puede superar el total de la factura. Lo previo
  se lee bajo el lock del cliente, así que dos NC simultáneas no pueden pasarse.
- El IVA de la NC se agrupa sin descuento global (las NC no tienen recargo/descuento: se graban en 0, como el ERP).

Comportamientos del ERP que se **mantienen** y conviene revisar con negocio:
- `DocumentosCliente.Porcentaje` de la NC guarda el **total de envases**.
- Anular los recibos de la factura deja anulado **todo** el recibo aunque haya imputado a otros comprobantes o la NC sea parcial.
- Con NC parcial sin recibos la factura igual pasa a CANCELADO.

### Remitos de venta — RV (`/api/DocumentoCliente`)

Port de `Agregar_Ws` / `Editar_Ws` / `IniciarPuntoVenta_WS` / `BuscarComprobantes_WS` / `BuscarRemito_PorID_Remito_Seleccionar_Ws` (`FrmRemitos`).
**Sin uso en producción** al portarlo (0 remitos y 0 relaciones en la base; el cliente usa solo VEN): se portó para cerrar el
circuito presupuesto → remito → factura, que el VEN y la FV ya consumen.

| Endpoint | Qué hace |
|---|---|
| `GET remito/nuevo?idEntidad=` | Letra (`ComprobantesLetras` según la categoría de IVA del cliente: hoy siempre R), planilla abierta, punto de venta y número sugerido. |
| `GET remito/comprobantes-pendientes?idEntidad=` | Presupuestos, internos y facturas del cliente con `Remitar = 1`, `Pendiente = 1`, no anulados ni cancelados. |
| `GET {id}/lineas-pendientes` | Líneas del comprobante con saldo pendiente y su `relacion`, listas para un remito **o una factura** (sirve también para facturar remitos/presupuestos). |
| `POST remito` | Alta (ver abajo). |
| `POST remito/{id}/anular` | Anulación (ver abajo). |

Alta, por línea como el ERP: **directa** → descuenta stock y queda con saldo para facturar; **de un presupuesto** → consume su saldo,
descuenta stock y queda para facturar; **de un interno/factura** → solo consume su saldo (el stock lo movió la venta). Cada comprobante
entregado queda ENTREGADO o ENTREGADO PARCIAL (con `Pendiente`) y se relaciona en `DocumentosClienteRemitos`
(`ID_DocumentoCliente` = entregado, `ID_Remito` = remito). El remito directo o de presupuesto queda `Facturar = 1, Pendiente = 1`.

Anulación: devuelve el saldo a los comprobantes entregados (GENERADO si no les queda nada entregado, si no ENTREGADO PARCIAL),
devuelve el stock de las líneas directas y de presupuesto, anula los movimientos de stock, libera números de serie y anula el remito.

Diferencias **intencionales** con el ERP:
- **Validaciones nuevas en el servidor** (bajo el lock del cliente): los comprobantes entregados tienen que ser del cliente y estar pendientes
  de remitir (409 si no); cada línea relacionada tiene que ser de uno de ellos, del mismo ítem y sin superar su saldo; cada comprobante
  informado tiene que entregar alguna línea. El tipo del comprobante relacionado lo toma el servidor (no el request).
- Letra, planilla y número los resuelve el servidor (`UPDATE ... OUTPUT`).
- **Remito facturado no se anula** (409). `DocumentosClienteRemitos` guarda con `ID_Remito` = remito también las facturas que lo facturaron;
  el ERP permitía anular en estado FACTURADO y las trataba como comprobantes entregados (les cambiaba el estado y borraba la relación).
- Remito con líneas directas **y** relacionadas: el ERP no devolvía el stock de las directas al anular; acá sí. Tampoco usa más
  la cabecera indexada por línea (`ods22.Rows(i)`) para el movimiento de stock.
- `ComprobantesCarga`: el SP del ERP grababa el `ID_Usuario` en `ID_Comprobante`; acá se graba el remito.
- Cada línea graba su propio `Otros` (el ERP grababa el de la cabecera).
- Observaciones de más de 50 caracteres se rechazan (el SP las recibe en varchar(50) y el remito no usa `DocumentosClienteObservaciones`).

Se mantiene del ERP y conviene revisar con negocio: transporte, chofer y unidad se graban vacíos (el ERP los tenía comentados);
al anular, el comprobante entregado vuelve a GENERADO/ENTREGADO PARCIAL aunque antes estuviera COBRADO.

### Presupuestos — PV (`/api/DocumentoCliente`)

Port de `Agregar_Ws` / `Modificar_Ws` / `Anular_Ws` / `IniciarPuntoVenta_WS` (`FrmPresupuestosABM`). Casi sin uso en producción
(3 presupuestos, de 2024).

| Endpoint | Qué hace |
|---|---|
| `GET presupuesto/nuevo?idEntidad=` | Letra (`ComprobantesLetras`: hoy P, solo configurada para las categorías de IVA 1 y 2), planilla abierta, punto de venta y número (`NUMERACION/PV`). |
| `POST presupuesto` | Cabecera (`Remitar = Facturar = Pendiente = 1`), observación, vencimiento a 30 días y cada línea con su saldo pendiente (`Total = Saldo = cantidad`, `Saldo2 = 0`). **No mueve stock ni cta. cte.**: lo hace el remito o la factura que lo consuma. |
| `PUT presupuesto/{id}` | Cabecera (cliente, datos impresos, totales, observación) y reemplazo de todas las líneas y sus saldos. Letra, número, fecha y sucursal no cambian. |
| `POST presupuesto/{id}/anular` | Baja de las líneas (libro IVA BAJA), saldos en 0 y comprobante ANULADO. |

Para remitirlo o facturarlo: `GET {id}/lineas-pendientes` y esas líneas (con su `relacion`) en `POST remito` o `POST interno` / `electronica`.

Diferencias **intencionales** con el ERP:
- **Modificar y anular solo en GENERADO y sin nada remitido ni facturado** (409). El ERP no validaba nada al modificar: borraba las
  líneas y saldos aunque un remito o una factura ya los hubiera consumido, dejando esas relaciones apuntando a líneas inexistentes.
- Al anular se ponen en 0 los saldos (el ERP los dejaba pendientes y seguían apareciendo como líneas para remitir/facturar).
- Letra, planilla y número los resuelve el servidor; si el cliente cambia, se bloquean ambos clientes.
- Cada línea graba su propio `Otros` (en el alta el ERP grababa el de la cabecera; en la modificación ya usaba el de la línea).

Se mantiene del ERP: la observación completa va a `DocumentosClienteObservaciones` solo en el alta (la modificación actualiza la
de la cabecera, truncada a 50); clientes de categorías de IVA sin letra configurada no pueden tener presupuestos (400 con mensaje claro).

### Planillas de caja (`/api/CajaPlanilla`)

Port de `FrmPlanillasCajaABM`. En uso diario: cada cajero abre una planilla a la mañana y la cierra a la noche (≈1.300 planillas;
todos los comprobantes de venta y recibos exigen una planilla abierta del usuario).

| Endpoint | Qué hace |
|---|---|
| `GET mias` | Planillas de las sucursales que opera el usuario (`UsuariosSucursales`), más nuevas primero. |
| `GET {id}/resumen` | Planilla con ingresos (Σ Debe) y egresos (Σ Haber) de `CajasPlanillasDetalle`, saldo = inicial + ingresos − egresos − rendido, y si el usuario puede cerrarla. |
| `GET nueva?idUsuario=` | Saldo inicial sugerido (diferencia de la última planilla del usuario), puntos de venta habilitados y si ya tiene una abierta. |
| `POST abrir` | `{ puntoVenta, idUsuario?, saldoInicial? }`. 409 si el usuario ya tiene una planilla abierta. |
| `PUT {id}` | `{ saldoInicial, totalRendido, cerrar }`: recalcula totales y diferencia y, con `cerrar: true`, la cierra. 409 si ya está cerrada. |

Permisos como el ERP: sin rol CEO/CTO solo se abre la caja propia y con el punto de venta de la sucursal del usuario; CEO/CTO
eligen usuario y punto de venta. Cerrar una planilla abierta **otro día** solo lo puede hacer un ADMINISTRADOR (en el ERP el combo
de estado quedaba deshabilitado).

Diferencias **intencionales** con el ERP:
- **Ingresos, egresos y diferencia los calcula el servidor** con el detalle de la planilla (el ERP guardaba lo que mandaba la pantalla:
  en 20 planillas los ingresos grabados no coinciden con el detalle). La pantalla del ERP casi nunca calculaba la diferencia
  (queda en 0 en todas las planillas cerradas), así que el saldo inicial sugerido era siempre 0.
- En el alta, el ERP convertía la diferencia a `Integer` (perdía los centavos); acá es decimal.
- "Ya tiene una caja abierta" mira cualquier planilla abierta del usuario (el ERP solo las de puntos de venta con VEN letra X) y se
  resuelve con un lock por usuario: dos aperturas simultáneas no crean dos cajas.
- La sucursal de la planilla es la del usuario dueño de la caja (el ERP usaba la del usuario logueado, que para un CEO abriendo la
  caja de otro dejaba la planilla en otra sucursal).
- Ver o modificar una planilla de una sucursal que el usuario no opera devuelve 403 (salvo CEO/CTO/ADMINISTRADOR).
- No se puede reabrir una planilla cerrada ni cambiar a un estado arbitrario (el ERP aceptaba cualquier ID de estado del combo).

`FrmAjustesCajaABM` (ajustes manuales de caja) no se portó: `CajasPlanillasDetalle` solo tiene movimientos de recibos.

### Usuarios (`/api/Usuario`)

Port de `FrmUsuariosABM` y `FrmCambiarPass`. Un usuario del ERP son cuatro cosas que se mantienen juntas en una transacción:
`aspnet_Users` + `aspnet_Membership` (login), `aspnet_UsersInRoles` (un rol), la fila de `Usuarios` (la que usan comprobantes y caja)
y `UsuariosSucursales`.

| Endpoint | Qué hace |
|---|---|
| `GET gestion` · `GET gestion/{id}` · `GET gestion/opciones` | Usuarios con rol y estado; detalle con rol y sucursales; roles y sucursales activas. **Nunca devuelve contraseñas.** |
| `POST gestion` | Alta: `{ usuario, password, nombre, email, idRol, activo, sucursales[] }`. 409 si el nombre de usuario está en uso. |
| `PUT gestion/{id}` | Nombre, email (también en Membership), estado (`Usuarios.ID_Estado` 15/16 y `IsApproved`), rol, sucursales y, si viene, contraseña. |
| `POST cambiar-password` | `{ passwordActual, passwordNueva }` del usuario logueado. |

`gestion*` exige rol **ADMINISTRADOR**. La contraseña se guarda con el mismo formato que `SqlMembershipProvider`
(`Base64(SHA1(salt + UTF-16LE(pw)))`, sal de 16 bytes, `PasswordFormat = 1`), así el usuario entra igual al WebForms y a la API.
El usuario y `UsuariosSucursales` pasaron a **solo lectura** en el CRUD generado (`entities.config`): antes cualquier usuario
logueado podía editar estados o darse acceso a otras sucursales sin pasar por Membership.

Diferencias **intencionales** con el ERP (seguridad):
- **No se guarda la contraseña en claro.** El ERP la copiaba en `Usuarios.Pass` y la devolvía al navegador al editar. La API escribe
  `Pass = ''` y, cuando cambia una contraseña, borra la copia que hubiera. Consecuencia: `FrmCambiarPass` del WebForms (que compara
  contra esa copia) deja de funcionar para esos usuarios; tienen que usar `POST cambiar-password`.
- `cambiar-password` valida la actual contra el hash de Membership (el ERP la comparaba con la copia en claro; además
  `Usuarios_CambiarPass` filtraba por `Email LIKE`, así que cambiaba la copia de todos los usuarios con ese email).
- Solo un ADMINISTRADOR administra usuarios (el ERP no controlaba roles: cualquier usuario logueado podía crear administradores).
- Contraseñas nuevas de **mínimo 8** caracteres (el Web.config del ERP pide 2). Las existentes siguen valiendo.
- Alta inactiva queda con `IsApproved = 0` (el ERP la dejaba aprobada y el usuario podía loguearse).
- `Usuarios.ID_Sucursal` = la primera sucursal elegida (el ERP grababa 0, y la caja y el login usan ese campo).
- El nombre de usuario no se puede cambiar (el ERP lo cambiaba solo en `Usuarios` y el login dejaba de encontrar su fila).
- Un administrador no puede darse de baja a sí mismo.

Se mantiene: SHA1 como hash (lo exige la compatibilidad con el WebForms; migrar a un hash moderno requiere que el WebForms deje de
validar contraseñas), email no único (`requiresUniqueEmail = false`), un solo rol por usuario. ABM de roles (`FrmRolesABM`): `GET/POST gestion/roles`, `PUT gestion/roles/{idRol}`; nombre en mayúsculas y único, y los roles que el código usa para permisos (ADMINISTRADOR, CEO, CTO) no se pueden renombrar (el ERP lo permitía y dejaba sin permisos a sus usuarios).

### Facturas de compra — FC (`/api/DocumentoProveedor`)

Port de `FrmFacturasCompras`. **Sin uso en producción** al portarlo (0 comprobantes de proveedor). El circuito de compras replica
al de ventas: orden de compra (OC) ≈ presupuesto, remito de compra (RC) ≈ remito, factura (FC), nota de crédito (NCP) y compra
con pago (COM + OP). Se portó primero la factura, que es la que carga la deuda con el proveedor.

| Endpoint | Qué hace |
|---|---|
| `GET factura/nueva?idProveedor=&idSucursal=` | Planilla abierta del usuario y letras posibles (`ComprobantesLetras`: proveedor RI → A, monotributo → C, resto → B). |
| `GET pendientes-facturar?idProveedor=` | Remitos de compra y órdenes de compra del proveedor con líneas pendientes (sin rol CEO/CTO, solo de la sucursal del usuario). |
| `GET {id}/lineas-pendientes` | Líneas con saldo y su `relacion`. |
| `POST factura` | Alta (ver abajo). 409 si la factura del proveedor ya está registrada. |
| `POST factura/{id}/anular` | Anulación. 409 si no está GENERADA/LIQUIDADA o tiene pagos imputados. |

Alta, como el ERP: punto de venta y número son los del proveedor (se completan con ceros). Por línea: **directa** → suma stock;
**de un remito de compra** → solo consume su saldo (el stock ingresó con el remito); **de una orden de compra** → consume su saldo
y suma stock. Números de serie nuevos en `ItemsNroSeries`, otros tributos, relación y estado FACTURADO / FACTURADO PARCIAL de cada
remito u orden, deuda en la cta. cte. del proveedor (saldo = total, `Total2` negativo) y, si la sucursal es RI, libro IVA compras +
`TxtComprasAlicuotas` por alícuota. Anulación: resta el stock sumado, devuelve saldos, borra libro IVA y tributos, anula cta. cte.,
números de serie y comprobante.

Diferencias **intencionales** con el ERP:
- **Control de duplicados que funciona:** el ERP comparaba el número completado a 4 dígitos contra el guardado a 8, así que nunca
  detectaba una factura repetida. El concepto usa también el número a 8 dígitos (el ERP mezclaba 4 y 8).
- Validaciones nuevas (bajo el lock del proveedor): letra válida para la sucursal y el proveedor, remitos / órdenes del proveedor y
  no anulados, cada línea relacionada del mismo ítem y sin superar su saldo.
- Anulación bloqueada si la factura tiene pagos (saldo de cta. cte. ≠ total); el ERP solo miraba el estado.
- Anulación de facturas mixtas (líneas directas y de remito/orden): el ERP solo restaba el stock de las líneas de orden de compra;
  acá resta también el de las directas.
- `TxtComprasAlicuotas_Anular` comparaba `ID_ComprobanteTipo` consigo mismo (borraba filas de otros tipos con el mismo ID);
  `DocumentosProveedorRemitos_Anular` borraba por `ID_DocumentoProveedor` recibiendo el ID de la relación. Acá se borra por la clave correcta.
- Cada línea graba su propio `Otros` (el ERP grababa el de la cabecera); el IVA del exento no se suma.
- "Pendientes de facturar" incluye órdenes de compra y remitos facturados parcialmente (el ERP solo listaba remitos en GENERADO).

Se mantiene del ERP y conviene revisar con negocio: en el libro IVA compras los **impuestos provinciales (2) y la percepción de IVA (6)
no se registran** (solo IIBB 5, percepciones 7/8/9, nacionales 1, municipales 3, internos 4 y otros 18); el movimiento de cta. cte. va
"en contra" del proveedor, igual que una venta.

### Notas de crédito de proveedor — NCP (`/api/DocumentoProveedor`)

Port de `FrmNotaCreditoProveedor`. **Sin uso en producción**. Es la factura de compra con los signos invertidos y comparte su código
(`UseCases/Compras/CompraEscritura.cs`): `GET nota-credito/nueva`, `POST nota-credito`, `POST nota-credito/{id}/anular`.

Alta: resta stock por cada línea (mercadería devuelta), números de serie que vuelven al proveedor (NO DISPONIBLE), otros tributos,
libro IVA compras como nota de crédito (`AFIP/NC A|B|C`) si la sucursal es RI y **saldo a favor** en la cta. cte. del proveedor
(saldo negativo, `Total2` positivo, movimiento a favor), que después se usa en una orden de pago. No se relaciona con remitos ni
órdenes de compra (en el ERP esa parte está comentada) y la letra sale de `ComprobantesLetras` para el tipo NCP.

Diferencias **intencionales** con el ERP:
- **Anulación:** el ERP copiaba la de la factura y volvía a **restar** el stock (lo descontaba dos veces); acá se devuelve.
- Anulación bloqueada si la nota ya se usó en una orden de pago (su saldo dejó de ser el total a favor).
- Mismos arreglos que la factura: control de duplicados que funciona, borrados por la clave correcta, `Otros` por línea.

### Órdenes de compra — OC (`/api/DocumentoProveedor`)

Port de `FrmOrdenCompraABM`. **Sin uso en producción**. Espejo del presupuesto: `GET orden-compra/nueva`, `POST orden-compra`,
`PUT orden-compra/{id}`, `POST orden-compra/{id}/anular`. Letra X y numeración propia (`NUMERACION/OC`); no mueve stock ni cta. cte.:
cada línea queda con saldo pendiente (`Total = Saldo = cantidad`, `Saldo2 = 0`) para recibirla con un remito de compra o facturarla.

Diferencias **intencionales** con el ERP:
- **La anulación del ERP anulaba una venta.** `Anular_Ws` usaba las clases de ventas (`DocumentosCliente`, `DocumentosClienteDetalle`)
  con el ID de la orden: si existía un comprobante de venta con ese mismo ID en estado GENERADO, lo anulaba y daba de baja sus líneas,
  sin devolver stock ni cta. cte. Acá se anula la orden en `DocumentosProveedor`. **Conviene corregirlo en el WebForms** antes de que
  alguien use órdenes de compra: con 130.000 ventas, los IDs bajos de cualquier orden nueva coinciden con ventas reales.
- Modificar y anular solo en GENERADA y sin nada recibido ni facturado (el ERP no validaba nada al modificar).
- Al anular, los saldos quedan en 0 (si no, la orden anulada seguiría ofreciendo líneas para recibir o facturar).
- Numeración manual según `NUMERACION/OC` (el ERP consultaba `NUMERACION/PV`, la de presupuestos).
- `ComprobantesCarga` guarda la orden (el ERP guardaba el ID de usuario en `ID_Comprobante`).

### Remitos de compra — RC (`/api/DocumentoProveedor`)

Port de `FrmRemitosCompra`. **Sin uso en producción**. Espejo del remito de venta, con el mismo código compartido de compras:
`GET pendientes-remitir?idProveedor=` (facturas y órdenes de compra con mercadería pendiente de recibir), `POST remito`,
`POST remito/{id}/anular`. Letra siempre R (el tipo RC no tiene filas en `ComprobantesLetras`); punto de venta, número y CAI son
los del remito del proveedor.

Por línea, como el ERP: **directa** → suma stock y queda pendiente de facturar; **de una orden de compra** → consume su saldo,
suma stock y queda para facturar (la orden pasa a ENTREGADO / ENTREGADO PARCIAL); **de una factura de compra** → solo consume su
saldo (el stock lo sumó la factura). No mueve cta. cte. ni libro IVA. Anulación: resta el stock sumado, devuelve saldos, anula
movimientos, números de serie y el comprobante.

Diferencias **intencionales** con el ERP:
- **No pisa el estado de la factura de origen.** `DocumentosProveedor.Estado` de una factura refleja el pago (PAGADO / PAGADO
  PARCIAL, que usa la orden de pago); el ERP le ponía ENTREGADO / ENTREGADO PARCIAL al remitirla y GENERADO al anular el remito.
  Acá solo se actualiza su `Pendiente`; a las órdenes de compra sí se les cambia el estado como en el ERP.
- Un remito facturado no se anula (409), y una **factura de compra con mercadería remitida tampoco** (antes había que anular el remito).
- Validaciones nuevas (bajo el lock del proveedor): comprobantes del proveedor, no anulados, del mismo ítem y sin superar su saldo.
- Al anular, también resta el stock de las líneas directas de un remito con relaciones (el ERP solo restaba las de orden de compra).
- `ComprobantesCarga` guarda el remito (el ERP guardaba el ID de usuario en `ID_Comprobante`).

### Compra con pago en el momento — COM (`/api/DocumentoProveedor`)

Port de `FrmCompras` (`Agregar_Ws` + `generarOrdenDePago` + `Editar_Ws`). **Sin uso en producción**. `GET compra/nueva`, `POST compra`,
`POST compra/{id}/anular`. Letra X y numeración propia (`NUMERACION/COM`). Suma stock por línea (sin dejar saldo para remitir ni facturar,
como el ERP), actualiza el costo del ítem (`Items.Neto` = precio unitario sin IVA) si `CAMBIAPRECIO/CAMBIAPRECIO = 1`, carga la deuda
en la cta. cte. del proveedor y, si se informan formas de pago, genera **en la misma transacción** la orden de pago que la imputa,
con el mismo `IOrdenPagoWriter` que el alta de orden de pago (efectivo, cheques, transferencias, tarjetas, retenciones; pago parcial
→ PAGADO PARCIAL). Respuesta: `{ compra, ordenPago }`. No va al libro IVA compras (como el ERP). Anulación solo en GENERADO y sin
pagos: si se pagó, primero se anula la orden de pago (que la devuelve a GENERADO).

Diferencias con el ERP: la COM se graba como deuda y la orden de pago la cancela con las mismas validaciones que una OP manual
(el ERP la grababa cancelada y después volvía a imputarla, sin validar cheques ni importes); la anulación resta el stock de la sucursal
de la compra (el ERP usaba una variable de sucursal sin cargar).

### Órdenes de pago — OP (`/api/ProveedorRecibo`)

Port de `FrmOrdendePago`. **Sin uso en producción** al portarlo (0 órdenes de pago). Es el espejo del recibo de cobro y reusa sus
piezas (cta. cte., caja, bancos, retenciones, numeración, lock por ente).

| Endpoint | Qué hace |
|---|---|
| `GET nueva` | Planilla abierta, punto de venta y número sugerido (letra X, `NUMERACION/OP`). |
| `GET comprobantes-pendientes?idProveedor=` | Cta. cte. pendiente del proveedor; REC/FV/VEN/NC con el saldo invertido, como la pantalla. |
| `POST` | Alta (ver abajo). |
| `POST {id}/anular` | Anulación. 409 si no está GENERADA. |

Imputación, en el orden en que llegan: **FC / COM / NCP** admiten pago parcial (PAGADO / PAGADO PARCIAL en `DocumentosProveedor`;
una NCP, con saldo negativo, suma al disponible); **VEN / FV / NC** del mismo ente como cliente se compensan enteros (COBRADO);
**REC** queda RELACIONADO y una **OP** anterior con saldo, RELACIONADA. Formas de pago: efectivo; **cheque de terceros** en cartera
(pasa a ENTREGADO y se registra en `ProveedoresCheques`); **cheque propio** de una chequera (ENTREGADO + movimiento de salida en su
cuenta); transferencia/depósito y tarjeta (movimiento de salida en la cuenta propia); retención. Todo sale de la caja (`Haber`).
Lo pagado de más queda como saldo negativo en la cta. cte. de la OP. Anulación: cheques de terceros vuelven a cartera, cheques
propios y los registrados al proveedor se anulan, se anulan caja / bancos / retenciones / detalle, se devuelve el saldo a cada
comprobante con su estado y se anula la cta. cte. de la OP.

Diferencias **intencionales** con el ERP:
- **Pago sin plata:** con el importe de la OP ya consumido, el ERP igual cancelaba la factura siguiente y la marcaba PAGADA con $0
  aplicado. Acá se rechaza (400).
- **Estado al anular:** el SP que decide PAGADO PARCIAL vs GENERADO comparaba `ID_DocumentoProveedor = ID_DocumentoProveedor`, o sea
  miraba todas las imputaciones de la base; acá mira las de ese comprobante.
- Anular solo en GENERADA (el ERP anulaba incluso una OP RELACIONADA, ya usada en otro comprobante).
- **Cheques:** se valida que el cheque de terceros siga en cartera y que el importe coincida, y que el cheque propio esté disponible;
  la actualización es condicional al estado, así que dos OP simultáneas no entregan el mismo cheque (409). Al anular, los cheques
  registrados en `ProveedoresCheques` quedan ANULADOS (el ERP los dejaba ENTREGADOS). `ProveedoresCheques.ID_Sucursal` guarda la
  sucursal del banco del cheque (el ERP grababa la sucursal de la empresa).
- Totales calculados en el servidor y cada comprobante validado contra su saldo pendiente (el ERP confiaba en el navegador).
- `ID_Elemento` del detalle apunta al cheque propio entregado (el ERP dejaba 1).

### Depósitos y extracciones bancarias — OD / OE (`/api/OrdenDeposito`, `/api/OrdenExtraccion`)

Port de `FrmOrdendeDeposito` / `FrmOrdendeExtraccion` (`Agregar_Ws` + `Editar_Ws`). **Sin uso en producción** (0 órdenes).
En cada controller: `GET nueva` (planilla abierta, punto de venta y número sugerido; letra X, `NUMERACION/OD` y `NUMERACION/OE`),
`POST` y `POST {id}/anular` (409 si ya está anulada). La cuenta destino/origen tiene que estar ACTIVA.

- **Depósito:** orden GENERADA con un movimiento de ingreso (`Debe`) en la cuenta por cada elemento: efectivo y depósito
  bancario/transferencia (tipo 1, con los datos de origen/destino informados), **cheque de terceros** en cartera (tipo 5, origen = banco
  y número del cheque; el cheque pasa a DEPOSITADO) y tarjeta (tipo 1). Retenciones y demás elementos → 400. El detalle apunta al
  movimiento generado (`ID_Elemento`).
- **Extracción:** solo efectivo: movimiento de egreso (tipo 6, `Haber`, `Total` negativo) con origen = la cuenta propia.
- **Anulación:** orden ANULADA (total 0), movimientos bancarios ANULADOS, cheques depositados vuelven a EN CARTERA, detalle anulado.

Como el ERP, **no se toca la caja**: depositar efectivo no genera egreso de la planilla ni extraer genera ingreso (revisar con negocio
si deberían). Diferencias **intencionales** con el ERP:
- **Extracción:** el ERP copió la pantalla de depósito; con cheques / transferencias / tarjetas grababa ingresos en una extracción.
  Acá solo se extrae efectivo.
- **Tarjeta:** el ERP grababa `Debe` = importe con `Total` negativo; acá es un ingreso coherente.
- **Cheques:** se valida que el cheque siga en cartera y que el importe coincida; la actualización es condicional al estado (409 si
  otra operación lo usó). No se crea la fila en `ProveedoresCheques` que el ERP generaba para un depósito (no hay proveedor).
- Anulación solo una vez (el ERP no controlaba el estado y repetía los efectos); `Total` de la orden calculado en el servidor.
- Ojo con los parámetros: `COMPROBANTE/OE` = 21 es el mismo ID que FVC; la API respeta lo que diga la tabla de parámetros.

### Transferencias de stock entre sucursales — MS (`/api/MovimientoStock`)

Reemplaza a `FrmMovimientoStockABM` / `FrmMovimientoStockRecibirABM`. **Nunca se usó en producción** (0 comprobantes MS, 0
movimientos) y el legacy no funcionaba (ver abajo), así que **no es un port literal**: se rediseñó como transferencia en dos pasos
con los estados que ya existen en DOCUMENTOSCLIENTE.

| Endpoint | Qué hace |
|---|---|
| `GET nuevo?idSucursalOrigen=` | Punto de venta de la sucursal de origen (por defecto la del usuario) y número sugerido (letra M, `NUMERACION/MS`). |
| `GET en-transito?idSucursalDestino=` | Lo que le enviaron a la sucursal (por defecto la del usuario) y falta recibir. |
| `GET {id}` | Movimiento con sus ítems. |
| `POST` | **Envío**: descuenta del stock de la sucursal de origen y queda **GENERADO** (en tránsito). |
| `POST {id}/recibir` | El destino lo recibe: suma a su stock → **CONFIRMADO**. |
| `POST {id}/rechazar` | El destino no lo acepta: vuelve al stock del origen → **RECHAZADO**. |
| `POST {id}/anular` | El origen lo cancela antes de que lo reciban: vuelve a su stock → **ANULADO**. |

- Solo se mueve `ItemsSucursales.Stock` (y `ItemsMovimientosDetalles` por cada salida / entrada): `Items.StockActual` no cambia,
  porque una transferencia no altera el stock total de la empresa. Mientras está en tránsito la mercadería no figura en ninguna sucursal.
- Validaciones del envío: origen ≠ destino, ítems que mueven stock y habilitados en las dos sucursales, y **stock suficiente en el
  origen** (lock por sucursal de origen para que dos envíos simultáneos no usen el mismo stock). Líneas repetidas del mismo ítem se suman.
- Permisos: enviar / anular quien opera la sucursal de origen; ver en tránsito / recibir / rechazar quien opera la de destino. "Opera" =
  es su sucursal, la tiene en `UsuariosSucursales`, o es ADMINISTRADOR / CEO.
- Recibir, rechazar y anular solo sobre GENERADO, con actualización condicional (409 si otro ya lo resolvió). Un movimiento recibido no se
  anula: se hace una transferencia inversa.
- Columnas: origen en `ID_Sucursal`, destino en `ID_Cliente` (donde lo guardaba el ERP; la tabla no tiene otra) con su nombre en
  `RazonSocial`, e `ID_Usuario` = el usuario real. Sin caja (`ID_PlanillaCaja = 0`) ni importes. No maneja números de serie (el ERP los
  marcaba NO DISPONIBLE pero nunca cambiaba su sucursal).

Lo que tenía el ERP: el combo de empresas consulta `EmpresaSucursales`, una tabla que no existe; la sucursal de origen se guardaba en
`ID_Usuario`; el alta descontaba del origen y la confirmación volvía a descontarlo (doble descuento) antes de sumar al destino; la
anulación por `CambiarEstado` no controlaba el estado previo (se podía anular algo ya recibido).

## Pendiente (próximos tickets)

Todos los flujos compuestos del WebForms están portados. Sin portar a propósito por no tener uso: ajuste de stock y
ajustes de caja. `FrmConciliacionBancaria` es una copia de la pantalla de facturas: no hay conciliación bancaria que portar.
