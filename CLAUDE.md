# ASO Producs — gestión para productores locales

Aplicación de gestión de escritorio para **productores locales de distinta índole**: **WPF ·
.NET 8** (`net8.0-windows`), instalación local en LAN, una sola organización por instalación.
Arranca desde el scaffold **ASO Genérico** con su armazón completo y tres módulos heredados
(Finanzas, Inventario y Materia Prima). Sobre ese armazón ya se construyó, con la receta de "Cómo
se agrega un submódulo", un cuarto módulo — **Procesos** (Producción y Despacho) — con Finanzas
ganando además Clientes y Cuentas por Cobrar; ver la sección "Procesos" más abajo.

## Origen de este proyecto (2026-09-11)

Copia de **ASO_GENERIC** (commit `df2dbb1`, "Scaffold inicial"), que viene de **ASO_RTR**
(planta de embotellado), que a su vez venía de **ASO** (`ASO_SOFTWARE_BASE`, cosecha mecanizada
de caña de azúcar). El historial de git no se hereda: este repo empieza de cero. Lo que cambió
al copiar:

- **Rebranding técnico**: proyecto, solución y namespace `ASO_GENERIC` → `ASO_PRODUCS`;
  `AsoGenericoDbContext` → `AsoProductoresDbContext`; cadena de conexión `AsoProductoresDb` y
  base `AsoProductores.mdf`; nombre visible "ASO Productores". `ASO_PRODUCS` es solo el
  identificador técnico (el nombre de la carpeta); el nombre visible es placeholder, ver
  PROVISIONAL.
- **Restos de ASO_RTR corregidos**: ASO_GENERIC todavía guardaba las preferencias en
  `%AppData%\ASO RTR` y titulaba "ASO RTR" los mensajes de error del arranque (`App.xaml.cs`).
  Ahora es "ASO Productores" en los dos sitios.
- **La migración `Baseline` se conserva** tal cual (solo cambia el nombre del DbContext que
  referencia): el modelo de datos no cambió al copiar.

Del scaffold se heredan, además, los dos módulos de ejemplo. No son arbitrarios: entre los dos
cubren los cuatro patrones que vale la pena tener a la vista al construir el primer módulo real:

- **CRUD simple** — `Proveedor`.
- **Documento con líneas** — `FacturaProveedor`, `EntradaInventario`/`SalidaInventario`.
- **Documento con máquina de estados** — `CuentasPorPagarService`, `MovimientosService`,
  `EntradasInventarioService`/`SalidasInventarioService`.
- **Contenedor de dos padrones** — `CuentasPorPagarViewModel` (facturas + proveedores),
  `MovimientosViewModel` (movimientos + cuentas).
- Y, solo en Inventario: **existencia derivada** (kardex calculado, no tecleado) y **grilla de
  líneas editable** (`Controls/PuenteDeDatos.cs`), que no tenía ningún otro módulo del scaffold.

La migración de EF Core también se reinició: `Migrations/` de este proyecto empieza en una única
migración `Baseline`, generada contra el modelo ya recortado — no había una migración limpia que
heredar una vez que se quitaron las tablas de los tres módulos eliminados.

### Sumado después de la copia (2026-09-11)

- **El submódulo Finanzas · Banco se renombró a Movimientos** (clave `Finanzas.Movimientos`):
  `BancoViewModel` → `MovimientosViewModel`, `BancoView` → `MovimientosView`, `BancoService` →
  `MovimientosService`, la clase de permisos `Permisos.Banco` → `Permisos.Movimientos`. Las
  entidades `CuentaBancaria`/`MovimientoBanco` y el nombre del icono (`Iconos.Banco`) NO
  cambiaron: son el dato, no la pantalla.
- **Se agregó el módulo Materia Prima** (`Existencias`, `Recepciones`, `Salidas`), traído de
  **ASO_RTR** (su módulo `MateriaPrima`: `Custodia`/`Recepciones`/`Despachos`, para custodiar
  botellas de un tercero) y despojado de todo lo propio de esa planta — ver la sección "Materia
  Prima" más abajo para qué cambió y por qué.
- **Se agregó el módulo Procesos** (`Producción`, `Despacho`), construido de cero para este
  proyecto (a diferencia de Materia Prima, no viene de ningún scaffold anterior), y con él
  `Finanzas · Clientes` y `Finanzas · Cuentas por Cobrar` — ver la sección "Procesos" más abajo.

## Cómo ejecutar

Es escritorio, no web (no hay dev server / puerto). `dotnet run` dentro de
`ASO_PRODUCS.Desktop`, o F5 en Visual Studio (`ASO_PRODUCS.slnx`).

## Distribución (2026-09-12)

El cliente no instala el .NET Runtime a mano ni reemplaza el `.exe` a mano: **Velopack**
(`Velopack` NuGet, `App.xaml.cs`) empaqueta un instalador de un clic y resuelve las
actualizaciones solo. La base de datos sigue siendo LocalDB por máquina, sin cambios — eso es
aparte de esto.

- **`App()` llama a `VelopackApp.Build().Run()` como lo primero que corre**, antes de
  `OnStartup`: así intercepta los argumentos que Windows le pasa al instalar/actualizar/
  desinstalar (`--veloapp-install`, etc.) sin llegar a tocar la base ni mostrar el login.
- **`App.OnStartup` llama a `RevisarActualizaciones()` antes que nada más.** Si
  `AppConfig.RutaActualizaciones` (clave `Actualizaciones:Ruta`, ver
  `appsettings.local.example.json`) está vacía, no revisa nada — así el scaffold sigue
  arrancando igual que siempre mientras un cliente nuevo no tenga carpeta de actualizaciones
  configurada. Si hay una ruta y hay versión nueva, la descarga y reinicia con
  `ApplyUpdatesAndRestart` — el resto de `OnStartup` no llega a correr ese arranque. Cualquier
  falla (sin red, carpeta no alcanzable) se traga y sigue con la versión que ya tenía: nunca
  vale la pena bloquear el arranque por esto.
- **La ruta de actualizaciones es una carpeta local o de red** (`\\servidor\carpeta`, un feed
  que arma `vpk pack`), no un servidor web — no hace falta levantar nada aparte, alcanza con un
  recurso compartido de la LAN del cliente (coherente con "instalación local en LAN" del resto
  del proyecto). Va en `appsettings.local.json`, la misma ruta en TODAS las máquinas de un mismo
  cliente (no es un dato por máquina como la cadena de conexión: es la carpeta compartida donde
  ese cliente recibe sus actualizaciones).
- **`AppConfig.CarpetaDatos` (el archivo `.mdf` de LocalDB) vive en
  `%AppData%\ASO Productores\App_Data`**, misma carpeta base que ya usaba
  `AjustesStoreJson` para las preferencias. Antes se calculaba subiendo tres niveles desde
  `AppContext.BaseDirectory`, asumiendo el layout de "dotnet run"/F5 en Debug
  (`bin/Debug/netX.0-windows`); eso se rompía en cuanto la app corría empaquetada, porque
  Velopack instala en `%LocalAppData%\<PackId>\current\`, sin ese layout de tres niveles.

### Cómo publicar una versión nueva

1. `dotnet publish ASO_PRODUCS.Desktop.csproj -c Release --self-contained -r win-x64 -o .\publish`
   (autocontenido: el cliente no necesita el .NET Runtime instalado).
2. `dotnet tool install -g vpk` (una sola vez por máquina de quien publica).
3. `vpk pack --packId AsoProductores --packVersion X.Y.Z --packDir .\publish --mainExe ASO_PRODUCS.Desktop.exe --outputDir .\releases`
   — **`packId` no cambia nunca** entre versiones (es la identidad de la app para Velopack);
   `packVersion` sube en cada release (semver).
4. Copiar el contenido de `.\releases\` a la carpeta compartida que apunta
   `Actualizaciones:Ruta` (reemplaza lo que había: el feed (`releases.win.json`) siempre
   describe el estado completo, no solo lo nuevo).
5. **Primera instalación en una máquina nueva del cliente**: correr una vez el
   `AsoProductoresSetup.exe` de esa misma carpeta — sin asistente, sin permisos de administrador.
   Las máquinas ya instaladas se actualizan solas en su próximo arranque.

### Pendiente, no bloqueante

El `.exe`/instalador no está firmado todavía: Windows SmartScreen puede advertir en la primera
ejecución de cada máquina ("Editor desconocido"). No impide instalar ni actualizar; firmar con un
certificado de código es un paso aparte para cuando el cliente lo pida.

## Decisiones de arquitectura (heredadas, sin cambios)

- **WPF con MVVM ligero**: `ViewModels/ViewModelBase.cs` (INotifyPropertyChanged). Lógica fuera
  del code-behind.
- **Datos detrás de interfaces** (`Services/I<X>DataSource.cs`), resueltas en
  `Configuration/DataSourceFactory.cs`. La UI y los ViewModels no conocen EF Core.
- **Sin mocks**: el único camino es SQL Server vía EF Core. Un modo "sin BD" no sobrevive al
  aislamiento por organización, porque los mocks no pasan por el filtro global de EF.
- **Regla de oro**: toda regla de negocio vive en servicios de dominio, nunca en eventos de
  botones.
- **Autorización en capas**: hoy solo la capa 1 (RBAC por permiso de comando) y la 3
  (segregación de funciones: aprobador ≠ solicitante, en `PeticionService`) están completas. La
  autorización se comprueba en el `CanExecute` de los comandos y, para las transiciones propias
  de un documento, dentro del servicio de dominio (`CuentasPorPagarService`, `MovimientosService`
  exigen `ISesionActual` por constructor). El CRUD genérico (`CrudViewModelBase`) todavía escribe
  directo contra el `IDataSource` sin repetir la comprobación — hueco puntual heredado, ver
  "Próximo paso sugerido".

## Estructura de módulos

Hoy hay **cuatro módulos de negocio**: los tres heredados del scaffold — **Finanzas** (Cuentas por
Pagar, Cuentas por Cobrar, Movimientos, Proveedores y Clientes), **Inventario** (Almacén, Entradas
y Salidas) y **Materia Prima** (Existencias, Recepciones y Salidas) — más **Procesos** (Producción
y Despacho), agregado después con la receta de "Cómo se agrega un submódulo" (ver la sección
"Procesos" más abajo). A esto se suman **cuatro módulos fijados** sin submódulos: **Inicio**,
**Peticiones** (bandeja de solicitudes de cambio), **Administración** (usuarios con sus permisos,
y los datos de la propia organización) y **Configuración** (apariencia, cuenta propia,
preferencias de la máquina — anclada al pie del sidebar, fuera de su `ScrollViewer`, porque no es
trabajo del día).

- `Navigation/ModuloCatalogo.cs` — **fuente única** de la estructura (clave, nombre, descripción,
  icono, submódulos). Sidebar, Inicio, dashboard y enrutado leen de aquí.
- `Controls/Sidebar.xaml(.cs)` + `ViewModels/SidebarViewModel.cs` — menú de dos niveles; solo
  emite `NavegacionSolicitada`. `MainWindow.Navegar(modulo, submodulo)` decide la vista.
- `Views/InicioView` — lanzador con una tarjeta por módulo.
- `Views/ModuloDashboardView` — resumen del módulo: indicadores + tarjeta por submódulo. Los
  valores los calcula `ModuloDashboardViewModel.CalcularIndicadores` (un `switch` por clave de
  módulo) — hoy con los casos `"Finanzas"`, `"Inventario"`, `"MateriaPrima"` y `"Procesos"`.
- `Views/SubmoduloView` — submódulo en construcción, para cuando se agregue uno nuevo al catálogo
  antes de tener su pantalla real.
- Framework CRUD reutilizable (`CrudViewModelBase`, `CrudEditorViewModelBase`, `CrudEditorWindow`,
  `IServicioDialogo`), login/sesión y capa de datos (`Models`, `Services`, `BD`,
  `DataSourceFactory`).

## Cómo se agrega un submódulo (receta)

Archivos a **crear**: `Models/<X>.cs` (con `Clonar()`, y **`: IDeOrganizacion` con su
`OrganizacionId`** si la entidad pertenece a una organización; los modelos NO implementan
INotifyPropertyChanged) · `Services/I<X>DataSource.cs` + `BD/Sql<X>DataSource.cs` ·
`Services/<X>Service.cs` si es documento (métodos `PuedeX` puros para el `CanExecute` +
transiciones que revalidan y lanzan `InvalidOperationException` en español) ·
`ViewModels/<Submodulo>ViewModel.cs` · editores · vistas XAML.

La fuente de datos es una línea, no una clase: hereda de `SqlCrudDataSource<T, TId>` y solo
declara las consultas que sean suyas. Si la entidad es una raíz con hijos que se guardan juntos,
hereda de `SqlAgregadoDataSource<T, TId>` en su lugar (ver `FacturaProveedor`/`FacturaProveedorLinea`
como ejemplo de documento con líneas). El ViewModel de la pantalla hereda de
`PantallaViewModelBase`, o de `PantallaCrudViewModel<T, TId>` si además es el listado CRUD de un
maestro; las dos ramas cumplen `IPantalla`, que es lo que el shell enruta.

Archivos a **modificar** siempre: `Configuration/DataSourceFactory.cs` (campo cacheado `??=`),
`BD/DbContext.cs` (`DbSet` + configuración en `OnModelCreating`), la tabla `Pantallas` de
`MainWindow.xaml.cs` (una línea por clave de submódulo), `Styles/PantallaTemplates.xaml` (un
`DataTemplate` por pantalla, o el área de contenido muestra el nombre del tipo del ViewModel),
`Styles/EditorTemplates.xaml` (un `DataTemplate` por editor, o la ventana sale vacía) y
`Styles/Theme.xaml` (un `Chip…Style` por enum de estado nuevo, con
`BasedOn="{StaticResource ChipBaseStyle}"`). Después, `dotnet ef migrations add`.

El permiso de navegación **no se declara**: `Submodulo.Permiso` lo deriva de la clave
(`Ver.<clave>`), así que basta con dar de alta el submódulo en `ModuloCatalogo`. Lo que sí hay
que hacer es sumarlo al rol que corresponda en `Services/MatrizPermisos.cs`, o no lo verá nadie
salvo el Desarrollador.

Arquetipos a copiar:

- **Finanzas · Cuentas por Pagar** — `Models/Proveedor.cs` (CRUD simple),
  `Models/FacturaProveedor.cs` (documento con líneas), `Services/CuentasPorPagarService.cs`
  (documento con máquina de estados), `ViewModels/CuentasPorPagarViewModel.cs` (contenedor de dos
  padrones, junto con `MovimientosViewModel` de Finanzas · Movimientos).
- **Inventario** — kardex derivado (`InventarioService.ExistenciasPorArticulo`), documento con
  líneas editables en vivo (`ViewModels/EntradasViewModel.cs` + `Controls/PuenteDeDatos.cs`), y
  acoplamiento entre módulos (`EntradasInventarioService` exige `CuentasPorPagarService`).
- **Materia Prima** — mismo patrón que Inventario (existencia derivada, documento con líneas
  editables) pero **desacoplado de Finanzas**: `RecepcionesMateriaPrimaService` no depende de
  `CuentasPorPagarService`, así que sirve de ejemplo de un módulo de existencias que no genera
  deuda al recibir.
- **Procesos · Producción** — un patrón que ningún otro módulo tenía: agregado de DOS niveles
  (`ProcesoProduccion.LineasIniciales` + `Etapas`, cada etapa con sus propias líneas) cuyo consumo
  puede venir indistintamente de dos padrones de existencia (`OrigenMaterial.MateriaPrima`/
  `Articulo`) y que, al guardarse, dispara una salida real y numerada en el módulo que
  corresponda (`ProcesosProduccionService.ConstruirSalidas` + `RegistrarSinPermiso` de
  `SalidasMateriaPrimaService`/`SalidasInventarioService`) — el ejemplo a copiar para un documento
  que necesita escribir en más de un módulo existente a la vez.

## Inventario

Tres submódulos: **Almacén** (catálogo de artículos con su existencia), **Entradas** (lo que
llega) y **Salidas** (boletos de salida). Las decisiones que no se deducen del código:

- **La existencia NO se guarda: se deriva.** `Articulo` no tiene columna de existencia;
  `InventarioService.ExistenciasPorArticulo()` la calcula como Σ líneas de entradas − Σ líneas de
  salidas, sin contar documentos anulados. `Articulo.Existencia` es una propiedad `Ignore()`-ada
  que rellena el servicio antes de pintar, igual que `CuentaBancaria.SaldoActual`. Se eligió así
  porque un número editable a mano se desincroniza del historial y no deja rastro de por qué
  cambió; el precio a pagar es que **hay que refrescar la vista a mano** tras rellenarla, porque
  los modelos no notifican cambios (ver `AlmacenViewModel.Recargar`). El cálculo es una pasada por
  cada tabla sobre un diccionario, no una consulta por artículo.
- **El stock inicial se carga con una entrada de tipo `Ajuste`**, que es el único tipo que no
  genera cuenta por pagar. Un ajuste negativo es una salida con motivo `Merma` o `Traslado`; no
  hay entradas con cantidad negativa.
- **Registrar una entrada de compra crea su cuenta por pagar en Finanzas**, y la dependencia no es
  opcional: `EntradasInventarioService` exige `CuentasPorPagarService` por constructor, igual que
  éste exige `BancoService`. La factura se escribe **antes** que la entrada, por el mismo motivo
  que el asiento de banco va antes de marcar una factura pagada: es la operación que puede
  rechazar. Si aun así la entrada fallara al guardarse, se retira la factura recién creada — no
  hay transacción que abarque las dos tablas, porque cada fuente de datos abre su propio contexto.
- **El anti-duplicado ya existía**: `CuentasPorPagarService.Validar` rechaza un número de documento
  repetido para el mismo proveedor, que es exactamente el caso "la misma factura se cargó dos
  veces". No hizo falta un `GetByOrigen` como el de Banco.
- **El comercio de una compra externa entra al padrón de proveedores** (se busca por nombre, se da
  de alta solo si no estaba), porque la deuda tiene que quedar a nombre de alguien y así se paga
  por el camino de siempre. Riesgo conocido: un nombre mal escrito crea un proveedor duplicado.
- **Anular una entrada** exige dos cosas, comprobadas antes de escribir nada: que el almacén no
  quede en negativo, y que su factura no esté ya pagada (eso se deshace con una nota de crédito).
  Anular una salida no exige nada: la existencia vuelve sola porque el kardex ignora lo anulado.
- **Los correlativos** (`ENT-000123`, `SAL-000123`) los asigna el servicio al registrar, con
  "el último + 1", y el índice único `(OrganizacionId, Numero)` es la red por si dos puestos
  coincidieran. Se persisten, no se derivan del `Id`, porque la descripción de la cuenta por pagar
  cita el número de la entrada y hace falta antes de insertar.
- **Quién autoriza un boleto no es un campo del formulario**: lo estampa el servicio con el usuario
  de la sesión, para que no se pueda escribir otro nombre en el papel.
- **`Agregar()` de `CrudViewModelBase` es `protected virtual`** (igual que `Editar` y
  `Eliminar`): Entradas y Salidas lo redefinen para que el alta pase por el servicio de dominio y
  no escriba directo contra la fuente de datos.
- **`Controls/PuenteDeDatos.cs`** es genérico: las columnas de un `DataGrid` no están en el árbol
  visual y no heredan `DataContext`, así que un `Binding` puesto en una columna falla en
  silencio. Lo usan las dos grillas de líneas (Entradas, Salidas) para ocultar la columna de
  precios en un ajuste y para llegar al comando de quitar línea.

## Materia Prima (2026-09-11)

Tres submódulos: **Existencias** (catálogo de tipos con su existencia calculada — CRUD, mismo
arquetipo que Almacén), **Recepciones** (lo que entra) y **Salidas** (lo que sale). Viene del
módulo `MateriaPrima` de **ASO_RTR** (`Custodia`/`Recepciones`/`Despachos`, para llevar la
custodia de botellas de un tercero, Dusa), traído a este scaffold y despojado de todo lo
específico de esa planta:

- **`Custodia` → `Existencias`, `Despachos` → `Salidas`** (los nombres que pidió este proyecto);
  `Recepciones` se conservó igual.
- **`TipoBotella` (del módulo Catálogo de ASO_RTR, no traído) se reemplazó por
  `Models/TipoMateriaPrima.cs`**, un catálogo simple (Id, Nombre, UnidadMedida, Activo) que vive
  DENTRO de este módulo — no hay módulo Catálogo aparte. Es la misma idea que `Articulo`, pero sin
  la especificidad de paletas y patrón de paletizado.
- **Se fueron las columnas de paletizado y el acoplamiento con Operaciones**
  (`BotellasPorPaletaSnapshot`, `CantidadPaletas`, la dependencia con `CustodiaProduccionService`
  de ASO_RTR): la cantidad de cada línea es un `decimal` genérico
  (`RecepcionMateriaPrimaLinea.Cantidad`/`SalidaMateriaPrimaLinea.Cantidad`), sin conversión de
  unidades. Tampoco se trajeron los campos específicos de transporte (`Gandolero`, `Placa`,
  `NumeroOrdenEntrega` → `Referencia` genérica) ni el placeholder de facturación a Dusa
  (`FacturaDusaReferencia`/`FacturaDusaFecha`).
- **La existencia se deriva igual que en Inventario.**
  `MateriaPrimaService.ExistenciasPorTipo()` suma las líneas de recepciones registradas y resta
  las de salidas registradas, sin contar documentos anulados — mismo patrón que
  `InventarioService.ExistenciasPorArticulo()`, con `TipoMateriaPrimaId` en vez de `ArticuloId`.
  `SalidasMateriaPrimaService.Validar` revisa esa existencia EN VIVO antes de dejar salir algo, y
  `RecepcionesMateriaPrimaService.Anular` revisa que deshacer una recepción no la deje en
  negativo — calco de las reglas equivalentes de Inventario.
- **Genera cuenta por pagar en Finanzas cuando viene de un proveedor** (2026-09-12): calco de
  `EntradaInventario`/`EntradasInventarioService`. `RecepcionMateriaPrima.Tipo`
  (`TipoRecepcionMateriaPrima`: `CompraProveedor`, `CompraExterna`, `OtroOrigen`) reemplaza el
  antiguo "todo es sin proveedor". Las dos primeras exigen proveedor (del padrón, o uno nuevo dado
  de alta al vuelo si es "externo"), número de documento, precio por línea y vencimiento, y
  `RecepcionesMateriaPrimaService.Registrar` crea la `FacturaProveedor` correspondiente —factura
  antes que recepción, mismo orden que `EntradasInventarioService.Registrar`, por el mismo motivo
  (es la operación que puede rechazar). `Anular` deshace también esa factura, si sigue pendiente, y
  se niega si ya está pagada — mismo criterio que `EntradasInventarioService.Anular`.
  `OtroOrigen` es el único que no depende de Finanzas: cubre aporte de un socio, cosecha propia,
  maquila, etc., y es a lo que se migraron todas las recepciones que ya existían antes de este
  cambio (no habían tenido proveedor nunca). `Referencia` (guía/remito/orden) se conserva tal cual,
  independiente de `NumeroDocumento` (el papel de la compra, que alimenta la factura).
- **Sin campo de destino en Salidas**, a propósito: `SalidaMateriaPrima` no tiene el equivalente
  de `SalidaInventario.Destino` — es lo primero que hay que decidir con el negocio real (a quién o
  a dónde va la materia prima) y agregar cuando se conozca.
- **Los correlativos** (`REC-000123`, `SMP-000123`) siguen el mismo mecanismo que Inventario: el
  servicio asigna "el último + 1" al registrar, con el índice único `(OrganizacionId, Numero)`
  como red.
- **Reutiliza `Controls/PuenteDeDatos.cs`** para las dos grillas de líneas (Recepciones, Salidas),
  igual que Inventario.

## Procesos (2026-09-11)

Dos submódulos: **Producción** (fabricación de producto terminado a partir de materia prima y/o
artículos de inventario) y **Despacho** (catálogo de `Producto` con existencia derivada + los
despachos que la consumen). A diferencia de Materia Prima, este módulo no viene de ningún scaffold
anterior: se construyó de cero contra este armazón.

- **`ProcesoProduccion` es un agregado de DOS niveles**: `LineasIniciales` (lo que se consume al
  iniciar) y `Etapas` (una lista de `EtapaProcesoProduccion`, cada una con sus propias `Lineas`).
  El nombre de cada etapa sale de `EtapaProduccion`, un catálogo reutilizable de la organización
  (Id, Nombre, Orden, Activo) — su comentario de cabecera da "lavado, cuajado, prensado, salado,
  maduración" como ejemplo, que sugiere elaboración de queso, aunque el modelo en sí es genérico.
  Las etapas se agregan de a una mientras el proceso sigue `EnProceso`
  (`ProcesosProduccionService.AgregarEtapa`); no se editan ni se quitan después.
- **Cada línea de consumo (inicial o de etapa) declara su `OrigenMaterial`** (`MateriaPrima` o
  `Articulo`). `ProcesosProduccionService.ConstruirSalidas` arma, agrupando por origen, una
  `SalidaMateriaPrima` y/o una `SalidaInventario` reales y numeradas, enlazadas al proceso
  (`ProcesoProduccionId`/`Numero`) — nunca las inventa: pasan por
  `SalidasMateriaPrimaService.RegistrarSinPermiso`/`SalidasInventarioService.RegistrarSinPermiso`
  sin repetir el permiso, que ya lo exigió `ProcesosProduccion.Crear`/`AgregarEtapa`.
- **Máquina de estados `EnProceso → Terminado`, con `Anulado` alcanzable desde cualquiera de los
  dos.** `Terminar` registra `CantidadProducida`, que puede diferir de `CantidadPlaneada` por una
  merma — no se valida contra la planeada, es justo el dato que interesa. Anular un proceso ya
  `Terminado` revisa EN VIVO que devolver la existencia del producto no la deje en negativo (si ya
  se despachó lo que produjo).
- **Las salidas de materia prima/inventario que generó un proceso NUNCA se revierten al
  anularlo**, haya llegado a `Terminado` o no: representan material que físicamente salió del
  almacén. Devolver material no usado es una Entrada/Recepción de tipo `Ajuste` nueva, igual que ya
  resuelve Inventario.
- **La existencia de `Producto` se deriva igual que en Materia Prima e Inventario**:
  `ProductosService.ExistenciasPorProducto()` suma lo producido por procesos `Terminado` y resta lo
  despachado, sin contar documentos anulados — mismo patrón, con la particularidad de que lo que
  "entra" es un proceso de este mismo módulo, no un documento de otro.
- **Un despacho tipo `Venta` exige cliente, lleva precio por línea y genera su cuenta por cobrar
  automáticamente**: `DespachosService` exige `CuentasPorCobrarService` por constructor, igual que
  `EntradasInventarioService` exige `CuentasPorPagarService`. Un `Ajuste` se salta ese paso porque
  no hay a quién cobrarle — mismo criterio que `TipoEntrada.Ajuste` en Inventario. La factura se
  escribe **antes** que el despacho, por el motivo de siempre: es la operación que puede rechazar.
- **Anular un despacho ya no se permite si su factura fue cobrada**: el dinero ya entró al banco,
  y deshacerlo es cosa de Movimientos, no del almacén — calco de la regla de Inventario con una
  factura ya pagada.
- **Nace `Finanzas · Clientes` y `Finanzas · Cuentas por Cobrar`**, acoplados a Despacho y no como
  módulo independiente: `CuentasPorCobrarService` es un calco de `CuentasPorPagarService` con
  Cliente en vez de Proveedor, y `Finanzas.Cobrar` es un permiso nuevo (Supervisor) paralelo a
  `Finanzas.Pagar`.
- **Los correlativos** (`PRO-000123` los procesos, `DES-000123` los despachos) siguen el mismo
  mecanismo que el resto de los módulos: el servicio asigna "el último + 1" al iniciar/registrar,
  con el índice único `(OrganizacionId, Numero)` como red.
- **Dos migraciones EF**: `AgregarProcesos`, `AgregarClientesYCuentasPorCobrar`.
- **`Producto.PrecioUnitario`** (2026-09-16) es un precio de referencia: se propone en una línea de
  despacho solo si el campo de precio de esa línea está vacío, y se puede cambiar a mano.
- **El costo de producción se deriva, no se guarda** (`CostosProduccionService`, 2026-09-16). Cada
  salida registrada enlazada a un proceso se valora al promedio ponderado de las compras con
  precio > 0 de ese material hasta la fecha de la salida (recepciones de Materia Prima, entradas
  de Inventario). Las entradas a precio 0 (`OtroOrigen`, `Ajuste`) no cuentan; un material sin
  compras con precio queda "sin costo" y marca el proceso como incompleto. La merma suma al costo.
  El margen usa el precio ACTUAL del producto, no uno histórico. Solo materiales: no hay mano de
  obra ni costos indirectos. Se ve en la ficha de detalle del proceso y en Reporte de Procesos ·
  Costos.
- **Lotes y vencimientos** (2026-09-16, migración `AgregarLotesYVencimientos`). Un lote ES un
  proceso Terminado: el código de lote es su `Numero`. Al terminar, el vencimiento se propone con
  `Producto.DiasVidaUtil` (hoy + días), es editable y queda nulo si el producto no vence. La
  existencia por lote se deriva en `ProductosService.Lotes()` (producido menos las líneas de
  despacho que citan ese `ProcesoProduccionId`), en orden FEFO. Las líneas de despacho anteriores
  a esto no tienen lote: se descuentan en memoria de los lotes de su producto en orden FEFO, sin
  tocar la base, para que la suma por lote cuadre con `ExistenciasPorProducto`. Desde ahora el
  lote es **obligatorio** en cada línea de despacho: el editor propone el que vence primero y
  `DespachosService.Validar` comprueba la existencia por lote. Anular un proceso Terminado se niega
  si ya se despachó algo de su lote. "Por vencer" = `LoteProducto.DiasPorVencer` (7 días). Despachar
  un lote vencido NO se bloquea: solo se marca en rojo. La pestaña Despacho · Lotes lista la
  existencia por lote con su estado.
- **Stock mínimo con alertas** (2026-09-16, migración `AgregarMinimoAMateriaPrimaYProducto`).
  `TipoMateriaPrima`/`Producto` ganaron `Minimo`/`BajoMinimo`/`MinimoTexto`, calcados de
  `Articulo.Minimo` (cero = no vigilar). El panel de cada módulo (Inventario, Materia Prima,
  Procesos) suma un indicador "Bajo mínimo", y el panel de Inicio (`InicioViewModel`) trae además
  una tarjeta de alertas —bajo mínimo de los tres catálogos y lotes por vencer, cada una con
  acceso directo al submódulo— calculada fuera del hilo de interfaz, mismo criterio que
  `ModuloDashboardViewModel`.

## Persistencia

Las entidades de dominio persisten en **SQL Server vía EF Core Migrations**.

- **Migraciones** en `ASO_PRODUCS.Desktop/Migrations/`: `Baseline` (generada contra el modelo
  recortado: `Organizacion`, `Usuario`, `PermisoUsuario`, `PeticionCambio`, `Proveedor`,
  `FacturaProveedor`+`FacturaProveedorLinea`, `CuentaBancaria`, `MovimientoBanco`, `Articulo`,
  `EntradaInventario`+`EntradaInventarioLinea`, `SalidaInventario`+`SalidaInventarioLinea`)
  seguida de `AgregarMateriaPrima` (`TipoMateriaPrima`,
  `RecepcionMateriaPrima`+`RecepcionMateriaPrimaLinea`,
  `SalidaMateriaPrima`+`SalidaMateriaPrimaLinea`), `AgregarProcesos` (`EtapaProduccion`,
  `ProcesoProduccion` con sus `LineasIniciales`/`Etapas`, `Producto`),
  `AgregarClientesYCuentasPorCobrar` (`Cliente`, `FacturaCliente`+`FacturaClienteLinea`,
  `Despacho`+`DespachoLinea`), `AgregarProcesoOrigenADespacho` (`ProcesoProduccionId`/`Numero` en
  `DespachoLinea`), `AgregarResultadoYMermaAEtapas` (`Resultado` en `EtapaProcesoProduccion`,
  `Motivo` en `EtapaProcesoProduccionLinea` y en `SalidaMateriaPrima`) y
  `AgregarProveedorARecepcionesMateriaPrima` (`Tipo`/`ProveedorId`/`NumeroDocumento`/
  `FechaVencimiento`/`RecibidoPor`/`Total`/`FacturaProveedorId` en `RecepcionMateriaPrima`,
  `PrecioUnitario`/`Subtotal` en `RecepcionMateriaPrimaLinea` — con un `UPDATE` de datos que migra
  a `OtroOrigen` todas las recepciones que ya existían, para no etiquetarlas como compra a
  proveedor por el valor por defecto de la columna nueva).
- **La cadena de conexión vive solo en `appsettings.local.json`** (por máquina, en `.gitignore`);
  la de `appsettings.json` (clave `ConnectionStrings:AsoProductoresDb`) apunta a LocalDB con un
  `.mdf` en `App_Data`.
- **No hay claves foráneas reales** en las tablas planas: las relaciones son `int` sueltos y la
  integridad es de la aplicación, con snapshots de texto (`…Nombre`) en cada documento.

## Una sola organización por instalación

**Una instalación atiende a una sola organización.** Ningún formulario pregunta a qué
organización pertenece algo: se estampa la de la organización instalada. **No hay ningún nivel
intermedio** — si el negocio real necesita varias plantas o líneas bajo la misma organización,
ese nivel hay que diseñarlo cuando se conozca la necesidad real (ver PROVISIONAL más abajo).

- **`Models/IDeOrganizacion.cs`** marca las entidades sujetas al ámbito.
- **`BD/DbContext.cs` hace todo el trabajo, en dos sitios:**
  - `AplicarFiltroDeOrganizacion` recorre el modelo y pone un `HasQueryFilter` a cada
    `IDeOrganizacion`. Es **fail-closed**: sin ámbito fijado no se ve nada, en vez de verse todo.
  - `SaveChanges()` estampa `OrganizacionId` en toda fila nueva, y en una fila modificada
    conserva el valor original (no el que traiga el formulario) para no dejar filas huérfanas.
- **`Services/Ambito.cs`** guarda la organización activa. La fija `SesionActual.IniciarSesion` a
  partir del usuario y **no cambia mientras dure la sesión**.
- **`IgnoreQueryFilters()`** se usa solo en el login (todavía no hay ámbito fijado), en
  `BD/SqlUsuarioDataSource.cs`.
- **`Organizaciones` no lleva filtro** — es la tabla que define el ámbito. La fila nace en el
  primer arranque (`Views/PrimerArranqueView`) y se corrige después en Administración ·
  Organización.

## Roles y permisos

Cuatro roles genéricos (`Models/Rol.cs`), cada uno con un conjunto base en
`Services/MatrizPermisos.cs` que el administrador ajusta por usuario con `PermisoUsuario`
(concede o revoca; **revocar gana**).

| Rol | Alcance |
|---|---|
| **Operador** | El día a día: crea/edita proveedores, clientes y facturas; mantiene los catálogos de artículos, tipos de materia prima, etapas de producción y productos; registra entradas/salidas de almacén, recepciones/salidas de materia prima, procesos de producción y despachos. No mueve dinero, no anula ni borra nada |
| **Supervisor** | Todo lo de Operador, más registrar pagos y cobros (dispara el asiento en Movimientos), administrar cuentas bancarias, anular documentos de almacén, de materia prima, de producción y de despacho, eliminar, y resolver peticiones de su dominio |
| **AdministradorOrganizacion** | Todo dentro de la organización, salvo crear usuarios Desarrollador |
| **Desarrollador** | Todos los permisos, y es el único que reparte su propio rol |

- **`Services/Permisos.cs`** es el catálogo de cadenas, formato `"Modulo.Accion"`. Los de
  navegación llevan prefijo `Ver.` y se **derivan de la clave del submódulo**.
- **`Rol` se persiste como ORDINAL** (`Usuarios.Rol int`, sin `HasConversion`): los miembros se
  añaden **siempre al final**. Añadir un rol es enum + conjunto en `MatrizPermisos` + el array
  `asignables` de `UsuarioEditorViewModel` (escrito a mano, no `Enum.GetValues`).
- **`SesionActual`** calcula el conjunto efectivo **una vez al entrar** y lo cachea: un cambio de
  rol o de permisos se aplica al volver a iniciar sesión, no en caliente.
- **Contraseñas**: PBKDF2-SHA256 con salt por usuario y 210 000 iteraciones
  (`Services/Passwords.cs`). No hay usuarios sembrados ni contraseñas por defecto: en la primera
  ejecución contra una base sin usuarios, `Views/PrimerArranqueView` pide el nombre de la
  organización, su código, y crea el usuario Desarrollador.

## Peticiones de cambio

Cuando a un rol le falta un permiso **de los sensibles** (`MatrizPermisos.Solicitables`), el botón
puede pedir el motivo con el `MotivoEditorViewModel` de siempre y dejar una `PeticionCambio` en la
bandeja del administrador (módulo fijado **Peticiones**). El mecanismo es genérico y está
completo (`PeticionService`, segregación de funciones, auditoría de la decisión); lo que está
vacío es la lista `Solicitables` — hoy no hay ninguna acción sensible gateada en las pantallas
que ve un Operador. Al agregar un módulo real con acciones sensibles (anular, aprobar, etc.),
sumarlas ahí y cablear el comando con `Services/SolicitudesDeCambio.cs`.

## El sistema visual

Tokens en `Styles/Tokens.xaml`, dos paletas en `Colors.xaml` / `ColorsOscuro.xaml` superpuestas en
caliente por `Services/Tema.cs`, controles de WPF re-estilados en `Styles/Controles.xaml` y
`Componentes.xaml` para que el tema oscuro no deje cajas blancas. Las cuatro reglas de siempre
siguen aplicando al escribir XAML nuevo:

1. Color por `DynamicResource` a una clave de `Colors.xaml`, nunca un hex a mano.
2. Una clave nueva de color va en **las dos** paletas (`Colors.xaml` y `ColorsOscuro.xaml`).
3. No se anima un brush: se anima la opacidad de un velo o borde superpuesto.
4. Sombra solo en lo que flota (`Effect="{DynamicResource SombraFlotante}"`), nunca en una
   tarjeta de datos.
5. **Todo relleno de acento usa `PrimaryButtonBrush` con `OnAccentBrush` encima; `PrimaryBrush`
   es solo color de texto/icono, nunca fondo.** El acento de este scaffold (ámbar) es CLARO, así
   que `PrimaryBrush` (un ámbar oscurecido, solo texto) y `PrimaryButtonBrush` (el ámbar literal,
   solo relleno) son dos claves distintas a propósito — pintar un fondo con `PrimaryBrush` no da
   el color que se espera. Ver el comentario de cabecera de `Colors.xaml` para el detalle de
   contraste de cada escalón de la familia.

## Marca (2026-09-15)

`Colors.xaml`/`ColorsOscuro.xaml` traen la paleta real: **bronce/dorado `#AD8631`** de principal,
**azul `#5279CE`** de complemento, con los contrastes recalculados (ver el comentario de cabecera
de `Colors.xaml` para el detalle de cada escalón y sus ratios WCAG). Ya no es la paleta ámbar/azul
placeholder del scaffold.

`Assets/Logo/` trae el logo real: una vaca de cuerpo completo (silueta, cuernos, ubre, cola en
espiral) tomada de `docs/vecteezy_a-cow-silhouette-logo-art-illustration-design_49947858.eps`
(fuente original, no la lee el proyecto — queda ahí solo como referencia). De ese `.eps` se
extrajo y recortó la variante elegida a dos rasters:

  El `.eps` trae un preview embebido (para verlo sin abrir Illustrator) pero es de muy baja
  calidad — paleta de 6 tonos de gris nada más, se nota a simple vista como bordes dentados en
  cualquier curva. Los rasters de acá salen de **renderizar el PostScript real** con Ghostscript
  (`gswin64c -dEPSCrop -sDEVICE=pngalpha -r100`, instalado para esto — no es una dependencia del
  proyecto en tiempo de ejecución, solo hizo falta para generar estos dos archivos una vez) y
  recortar a mano la variante de arriba a la izquierda. Si hay que retocar el recorte o el color
  del logo más adelante, repetir desde el `.eps`, no desde estos PNG/ICO (se pierde nitidez).

- **`aso-cow-mark.png`** — la vaca de cuerpo completo, transparente, en `OnAccentColor`
  (`#2B1C00`). La usan el sidebar (`Controls/Sidebar.xaml`, badge de 36px) y el login
  (`Views/LoginView.xaml`, badge de 56px) DENTRO del badge de `PrimaryButtonBrush` de siempre (ya
  no el monograma de texto "A"). El `<Image>` lleva
  `RenderOptions.BitmapScalingMode="HighQuality"`: sin eso, WPF reduce el mapa de bits con un
  algoritmo de baja calidad y el trazo se ve sucio aunque el PNG en sí esté perfecto.
- **`aso-icon.ico`** — compuesto autocontenido (badge + logo, no puede usar `DynamicResource`),
  multi-resolución (16/24/32/48/256). Es el `ApplicationIcon` del `.csproj` y el `Icon=` de
  `MainWindow.xaml`, `LoginView.xaml`, `PrimerArranqueView.xaml` y `CrudEditorWindow.xaml` — lo
  que Windows pinta de verdad en la barra de tareas, Alt+Tab y el Administrador de tareas, no el
  ícono del `.exe`. A diferencia de `aso-cow-mark.png`, acá va **solo la cabeza con los cuernos**
  (recorte propio, sin cuerpo/cola/ubre): a 16-24px la vaca completa se volvía una mancha
  ilegible, y una forma con menos elementos —pero más grandes— se reconoce mejor de chica. El
  fondo cuadrado-redondeado del `.ico` se dibuja a 4x de resolución y se reduce con LANCZOS antes
  de exportar los tamaños finales — dibujarlo directo con `ImageDraw` de Pillow no antialiasea
  las esquinas y quedaban con un escalón de píxeles duro.

El texto "ASO Productores" (`Sidebar.xaml`/`LoginView.xaml`/`MainWindow.xaml` `Title`/
`PrimerArranqueView.xaml` `Title`/`ApplicationTitle`+`Product` del `.csproj`/los `MessageBox` de
`App.xaml.cs`) pasó a **"ASO Producs"** (2026-09-16). Sigue siendo placeholder (punto 4 de
PROVISIONAL) — no es la razón social real todavía. `Company` en el `.csproj` sigue sin tocar
("Empresa"). **A propósito NO se tocó** la carpeta `%AppData%\ASO Productores\` (`AppConfig.cs`,
`AjustesStoreJson.cs`): es el mismo criterio que ya separa `ASO_PRODUCS` (técnico) del nombre
visible — cambiar esa carpeta mueve dónde vive el `.mdf` de LocalDB, y como el nombre lógico de
la base (`Database=AsoProductores` en la cadena de conexión) no cambia con la carpeta, la
instalación de LocalDB de esta máquina choca ("database already exists") contra el registro
viejo. Si el nombre visible se vuelve a tocar, la carpeta de datos se deja como está salvo que
haga falta migrarla a propósito.

## Configuración y preferencias

Tres pestañas en `Views/ConfiguracionView`: **Apariencia** (tema, escala), **Mi cuenta** (ficha +
cambiar contraseña) y **Aplicación** (hoy solo "abrir en la última sección"; no lleva permiso
propio porque no decide nada del negocio). Las preferencias NO van a la base de datos: viven en
`%AppData%\ASO Productores\ajustes.json` (`Models/AjustesApp.cs` + `Configuration/AjustesStoreJson.cs`).

## PROVISIONAL — decisiones pendientes, marcadas a propósito

Los módulos de los productores están por definir. Lo siguiente son placeholders deliberados, fáciles
de revisar y cambiar; no son bugs:

1. **Marca**: RESUELTO — paleta (`#AD8631`/`#5279CE`) y logo real ya puestos, ver "Marca" arriba.
   Sigue placeholder el nombre visible "ASO Producs" y `Company` en el `.csproj` (parte del
   punto 4).
2. **Roles genéricos**: `Operador` / `Supervisor` / `AdministradorOrganizacion` / `Desarrollador`
   no están atados a ningún puesto real. Ajustar los nombres y los conjuntos base en
   `Models/Rol.cs` / `Services/MatrizPermisos.cs` en cuanto el negocio real esté definido — son
   *el único* rediseño pendiente, porque los cuatro roles conservan intacto el mecanismo
   (ordinal append-only, permisos por deltas, revocar gana).
3. **Jerarquía física**: hoy es "una organización, sin nivel intermedio". Si el negocio real
   tiene varias plantas o líneas de producción bajo la misma organización, diseñar ese nivel
   cuando se conozca la necesidad real — no está prefigurado en el modelo de datos actual.
4. **Nombre del producto / razón social**: el nombre visible "ASO Producs" (títulos de ventana,
   sidebar, login) y "Empresa" en `Company` del `.csproj` son placeholders. La carpeta
   `%AppData%\ASO Productores\` (dato interno, no lo ve el usuario) se dejó fija a propósito —
   ver la nota en "Marca" arriba sobre por qué moverla choca con LocalDB en esta máquina; si
   hace falta migrarla de verdad algún día, hacerlo aparte, no como consecuencia automática de
   cambiar el nombre visible.
5. **Módulos de negocio**: además de los tres heredados del scaffold (Finanzas, Inventario,
   Materia Prima), ya se agregó **Procesos** (Producción y Despacho) construido de cero con la
   receta de "Cómo se agrega un submódulo" — ver la sección "Procesos" arriba. Sigue sin haber
   certeza de que este sea el módulo definitivo que necesita el negocio real (roles, marca y
   nombre siguen siendo placeholders); si hace falta otro módulo específico, Finanzas sirve para
   los cuatro patrones del framework, Inventario para uno construido de cero, Materia Prima para
   uno de existencias desacoplado de Finanzas, y Procesos para uno que dispara efectos en más de
   un módulo existente a la vez.
6. **`SalidaMateriaPrima` sin destino real**: a diferencia de `SalidaInventario.Destino`, no dice a
   quién o a dónde va la materia prima que sale — ver "Materia Prima" arriba. Definir con el
   negocio real si hace falta. (La otra mitad de este punto, que `RecepcionMateriaPrima` no decía
   de quién venía ni si había que pagarle, ya se resolvió — ver "Genera cuenta por pagar..." en
   "Materia Prima" arriba.)
7. **`Solicitables` vacío**: no hay ninguna petición de cambio configurada todavía — ver
   "Peticiones de cambio" arriba. Los candidatos naturales ya existen: `EntradasInventario.Anular`,
   `SalidasInventario.Anular`, `RecepcionesMateriaPrima.Anular`, `SalidasMateriaPrima.Anular`,
   `ProcesosProduccion.Anular` y `Despachos.Anular`, que hoy un Operador simplemente no ve.
8. **Pantalla de datos de la organización**: `Administración · Organización` solo pide nombre y
   código; si el negocio real necesita más datos de la organización (dirección, RIF, etc.),
   agregarlos ahí.

## Próximo paso sugerido

1. **Confirmar con el negocio real si Procesos (Producción/Despacho) es el módulo que hace
   falta**, o si hay que ajustarlo o agregar otro específico de los productores locales; Finanzas,
   Inventario, Materia Prima y ahora Procesos ya dejan los patrones y los almacenes de los que
   tirarán los demás.
2. **Decidir los roles reales** del negocio y reemplazar los cuatro genéricos.
3. ~~Elegir el color de marca y el logo real~~ — hecho, ver "Marca" arriba (2026-09-15). Queda
   el nombre visible "ASO Producs" (punto 4 de PROVISIONAL) si el negocio pide otro.
4. **Llevar la comprobación de permisos a los servicios de dominio** de cada módulo nuevo desde
   el principio (no repetir el hueco que tiene hoy el CRUD genérico).
