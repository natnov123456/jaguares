using FundacionJaguares.Application.Importacion;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;

// JAG-76: carga de datos iniciales desde planillas CSV.
//   dotnet run --project tools/FundacionJaguares.Importador -- <carpeta> [--aplicar]
// Sin --aplicar solo VERIFICA (no escribe nada): úsalo primero y revisa los errores.
// Conexión: variable JAGUARES_CONEXION (por defecto la base local de desarrollo).
// Las cuentas internas se crean sin contraseña utilizable: la Directora les asigna una desde "Usuarios y roles".
// La contraseña de la PRIMERA Directora se asigna desde el servidor (la plataforma no envía correos):
//   dotnet run --project tools/FundacionJaguares.Importador -- contrasena <correo>
// La contraseña se lee de la variable JAGUARES_CONTRASENA o se pide por consola sin mostrarla.
if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Uso: FundacionJaguares.Importador <carpeta-con-csv> [--aplicar]");
    Console.WriteLine("     FundacionJaguares.Importador contrasena <correo>   (asigna la contraseña de una cuenta del personal)");
    return 1;
}
var conexion = Environment.GetEnvironmentVariable("JAGUARES_CONEXION")
               ?? "Host=localhost;Port=5432;Database=fundacion_jaguares;Username=postgres;Password=postgres";

var opciones = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(conexion, npg => npg
        .MapEnum<Rol>("rol_enum", nameTranslator: new NpgsqlNullNameTranslator())
        .MapEnum<MedioPago>("medio_pago_enum", nameTranslator: new NpgsqlNullNameTranslator()))
    .UseSnakeCaseNamingConvention()
    .Options;
await using var db = new AppDbContext(opciones);

var usuarios = new UsuarioService(db);

if (args[0] == "contrasena")
{
    if (args.Length < 2) { Console.WriteLine("Indica el correo de la cuenta: contrasena <correo>"); return 1; }
    var password = Environment.GetEnvironmentVariable("JAGUARES_CONTRASENA");
    if (string.IsNullOrEmpty(password))
    {
        password = LeerOculto("Contraseña nueva (8+ caracteres, letras y números): ");
        if (LeerOculto("Repite la contraseña: ") != password) { Console.WriteLine("Las contraseñas no coinciden."); return 1; }
    }
    try
    {
        var cuenta = await usuarios.AsignarContrasenaPorCorreoAsync(args[1], password);
        Console.WriteLine($"Listo: {cuenta.NombreCompleto} ({cuenta.Rol}) ya puede iniciar sesión con {cuenta.Email}.");
        return 0;
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
    {
        Console.WriteLine($"Error: {ex.Message}");
        return 2;
    }
}

var carpeta = args[0];
var aplicar = args.Contains("--aplicar");

Console.WriteLine($"{(aplicar ? "IMPORTANDO" : "VERIFICANDO (sin escribir)")} datos de {Path.GetFullPath(carpeta)}");
if (aplicar)
    Console.WriteLine("Aviso: las cuentas internas se crean sin contraseña. La Directora les asigna una desde \"Usuarios y roles\".");

var resultado = await new ImportadorDatos(db, usuarios).ImportarAsync(carpeta, aplicar);
Console.WriteLine();
Console.WriteLine($"{"Archivo",-22} {"Origen",7} {"Válidas",8} {"Dupl.",6} {"Ya había",9} {"Importadas",11} {"En base",8}  Conteo");
foreach (var a in resultado.Archivos)
{
    Console.WriteLine($"{a.Archivo,-22} {a.FilasOrigen,7} {a.Validas,8} {a.DuplicadasEnArchivo,6} {a.YaExistian,9} {a.Importadas,11} {a.EnBaseDespues,8}  " +
                      (resultado.Aplicado ? (a.ConteoCoincide ? "OK" : "NO COINCIDE") : "-"));
    foreach (var e in a.Errores) Console.WriteLine($"    ✗ {e}");
}
Console.WriteLine();
Console.WriteLine(resultado.Exito ? "Resultado: sin errores." : "Resultado: hay filas con errores. Corrige la planilla y vuelve a ejecutar (lo ya cargado no se duplica).");
return resultado.Exito ? 0 : 2;

// Lee una línea de la consola sin mostrar lo que se escribe
static string LeerOculto(string mensaje)
{
    Console.Write(mensaje);
    if (Console.IsInputRedirected) { var linea = Console.ReadLine() ?? ""; Console.WriteLine(); return linea; }
    var texto = new System.Text.StringBuilder();
    while (true)
    {
        var tecla = Console.ReadKey(intercept: true);
        if (tecla.Key == ConsoleKey.Enter) break;
        if (tecla.Key == ConsoleKey.Backspace) { if (texto.Length > 0) texto.Length--; continue; }
        if (!char.IsControl(tecla.KeyChar)) texto.Append(tecla.KeyChar);
    }
    Console.WriteLine();
    return texto.ToString();
}
