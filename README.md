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

## Pendiente (próximos tickets)

Flujos compuestos que hoy viven en los code-behind del WebForms y deben portarse como casos de uso
transaccionales: órdenes de pago,
compras y facturas de proveedor, ajustes y movimientos de stock, depósitos/extracciones, conciliación
bancaria y alta de usuarios (Membership).
