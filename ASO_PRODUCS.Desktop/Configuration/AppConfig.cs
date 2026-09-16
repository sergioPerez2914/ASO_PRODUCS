using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace ASO_PRODUCS.Desktop.Configuration;

/// <summary>
/// Punto unico de acceso a la configuracion de la aplicacion.
/// Carga appsettings.json (valores por defecto, en el repo) y superpone
/// appsettings.local.json (por maquina, en .gitignore) si existe.
/// </summary>
public static class AppConfig
{
    private static readonly IConfigurationRoot _config = Build();

    private static IConfigurationRoot Build()
    {
        // La cadena por defecto usa LocalDB con |DataDirectory|, que SqlClient resuelve contra
        // esta ruta. Es un archivo por maquina (ver .gitignore): cada quien tiene el suyo, nadie
        // depende de la PC de otro para poder conectarse.
        Directory.CreateDirectory(CarpetaDatos);
        AppDomain.CurrentDomain.SetData("DataDirectory", CarpetaDatos);

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
            .Build();
    }

    /// <summary>
    /// Carpeta del archivo LocalDB: %AppData%\ASO Productores\App_Data, igual carpeta base que
    /// <see cref="AjustesStoreJson"/> usa para las preferencias (por máquina y por usuario de
    /// Windows). Antes se calculaba subiendo tres niveles desde <c>AppContext.BaseDirectory</c>,
    /// asumiendo el layout de "dotnet run"/F5 en Debug (bin/Debug/netX.0-windows); eso se rompía
    /// en cuanto la app corría empaquetada (Velopack la instala en
    /// <c>%LocalAppData%\&lt;PackId&gt;\current\</c>, sin ese layout de tres niveles) — ver
    /// CLAUDE.md "Distribución".
    /// </summary>
    private static string CarpetaDatos => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ASO Productores",
        "App_Data");

    /// <summary>Cadena de conexion a SQL Server (clave ConnectionStrings:AsoProductoresDb).</summary>
    public static string ConnectionString =>
        _config.GetConnectionString("AsoProductoresDb")
        ?? throw new InvalidOperationException(
            "No se encontro la cadena de conexion 'AsoProductoresDb'. " +
            "Revisa appsettings.json o crea appsettings.local.json a partir de appsettings.local.example.json.");

    /// <summary>
    /// Dónde busca actualizaciones Velopack: una ruta local o de red (p. ej. <c>\\servidor\carpeta</c>)
    /// con el feed que genera <c>vpk pack</c>. Vacía por defecto — sin ella,
    /// <c>App.xaml.cs</c> simplemente no revisa nada, para que el scaffold siga arrancando antes
    /// de que el cliente tenga una carpeta de actualizaciones configurada.
    /// </summary>
    public static string RutaActualizaciones => _config["Actualizaciones:Ruta"] ?? string.Empty;
}
