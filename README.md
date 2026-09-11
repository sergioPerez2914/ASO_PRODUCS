# ASO Productores

Aplicación de gestión de escritorio para **productores locales de distinta índole**. Arranca
desde el scaffold **ASO Genérico**: el armazón técnico y estético de **ASO** (autenticación,
roles y permisos, aislamiento multi-organización, framework CRUD/MVVM, tema claro/oscuro,
peticiones de cambio) más tres módulos heredados — **Finanzas** (Cuentas por Pagar, Movimientos y
Proveedores), **Inventario** y **Materia Prima** — que sirven de plantilla viva para construir los
módulos propios de los productores. Ver `CLAUDE.md` para el detalle completo de qué trae, cómo
está organizado y cómo se agrega un módulo nuevo.

## Requisitos

- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11

## Ejecutar

```bash
cd ASO_PRODUCS.Desktop
dotnet run
```

O abrir `ASO_PRODUCS.slnx` en Visual Studio 2022.

La base de datos es un archivo **SQL Server LocalDB** (`ASO_PRODUCS.Desktop/App_Data/AsoProductores.mdf`),
no un servidor aparte: no hace falta configurar nada para arrancar. Hace falta tener **LocalDB**
instalado (viene con Visual Studio; si no, se instala aparte con el instalador liviano
"SqlLocalDB.msi" de SQL Server Express). La primera vez, aplica el esquema:

```bash
cd ASO_PRODUCS.Desktop
dotnet ef database update
```

Esto crea `App_Data/AsoProductores.mdf` (no se sube al repo — cada máquina tiene el suyo, ver
`.gitignore`). En el primer arranque contra una base sin usuarios, la aplicación pide el nombre
de la organización y crea el usuario desarrollador. No hay usuarios ni contraseñas por defecto.

Si en cambio quieres apuntar a un SQL Server real (compartido o remoto) en vez de tu LocalDB
local, copia `ASO_PRODUCS.Desktop/appsettings.local.example.json` como `appsettings.local.json` y
pon ahí tu cadena de conexión — no se sube al repo, así que cada quien puede tener la suya.

## Estructura

```
ASO_PRODUCS/
├── ASO_PRODUCS.slnx
└── ASO_PRODUCS.Desktop/      # Aplicación WPF (MVVM ligero)
    ├── Models/           # Entidades del dominio
    ├── Services/         # Servicios de dominio, sesión/permisos y contratos de datos
    ├── ViewModels/       # Lógica de presentación
    ├── Views/            # Pantallas y editores
    ├── Navigation/       # Catálogo de módulos y submódulos (fuente única)
    ├── Configuration/    # Configuración y composición de fuentes de datos
    ├── BD/               # EF Core / SQL Server (DbContext + fuentes Sql…)
    ├── Migrations/       # Migraciones EF Core (una sola: Baseline)
    ├── Controls/         # Sidebar y componentes reutilizables
    └── Styles/           # Paleta y estilos
```

## Módulos

| Módulo | Submódulos | Estado |
|---|---|---|
| Finanzas | Cuentas por Pagar · Movimientos · Proveedores | heredado del scaffold, funcional |
| Inventario | Almacén · Entradas · Salidas | heredado del scaffold, funcional |
| Materia Prima | Existencias · Recepciones · Salidas | heredado de ASO_RTR, funcional |
| *Módulos de productores* | — | por definir |

Además hay **cuatro secciones fijas** según el rol: **Inicio**, **Peticiones** (bandeja de
solicitudes de cambio), **Administración** (usuarios y permisos, y los datos de la organización)
y **Configuración**, esta última anclada al pie del menú lateral: tema claro/oscuro, escala de la
interfaz, cambio de la propia contraseña y las preferencias de la máquina. Se guardan en
`%AppData%\ASO Productores\ajustes.json`, no en la base de datos.

**Inventario** lleva el almacén de insumos: un catálogo de artículos cuya existencia **no se
teclea**, se calcula de lo que entró menos lo que salió. Registrar una entrada por compra deja
sola su cuenta por pagar en Finanzas, y las salidas se emiten como boleto con destino, quién
retira y quién autoriza.

**Materia Prima** sigue el mismo patrón de existencia calculada, pero sin tocar Finanzas: sus
recepciones no generan cuenta por pagar, porque el módulo no asume que lo recibido se compró.

Los módulos propios de los productores se agregan siguiendo la receta de "Cómo se agrega un
submódulo" en `CLAUDE.md`, usando Finanzas e Inventario como arquetipo.

## Roles

Cuatro roles genéricos heredados del scaffold, todavía sin atar a ningún puesto real — ver
"PROVISIONAL" en `CLAUDE.md`.

| Rol | Qué puede |
|---|---|
| **Operador** | El día a día: registra facturas de proveedor y da de alta proveedores; mantiene los catálogos del almacén y de materia prima, y registra entradas/salidas de ambos. No mueve dinero, no anula ni borra nada: para eso levanta una petición |
| **Supervisor** | Todo lo de Operador, más registrar pagos (que asientan el movimiento en el libro), administrar cuentas bancarias, y anular documentos de almacén y de materia prima. Resuelve peticiones de su dominio |
| **Administrador de organización** | Todo dentro de la organización. Lo único que no puede es crear otros usuarios Desarrollador |
| **Desarrollador** | Todo, y es el único que puede crear otros usuarios Desarrollador |

**Una sola organización trabaja por instalación.** Ningún formulario pregunta a qué organización
pertenece algo: se estampa la de la instalación.

## Marca

Todavía no trae logo ni color de marca propios — `Assets/Logo/` está vacía y el sidebar/login
muestran un monograma de texto ("A") como placeholder. `Styles/Colors.xaml`/`ColorsOscuro.xaml`
traen la paleta ámbar/azul del scaffold, también como placeholder. Ver la sección "Marca" de
`CLAUDE.md` para qué tocar cuando se definan.

## Estado del proyecto

Recién copiado de ASO Genérico (2026-09-11): el armazón y los dos módulos de ejemplo compilan y
funcionan igual que en el scaffold. Lo pendiente —módulos de productores, roles reales, color de
marca, nombre definitivo— está en la sección "PROVISIONAL" de `CLAUDE.md`.
