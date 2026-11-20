using FundacionJaguares.Application.Importacion;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql.NameTranslation;

// JAG-76: carga de datos iniciales desde planillas CSV.
//   dotnet run --project tools/FundacionJaguares.Importador -- <carpeta> [--aplicar]
// Sin --aplicar solo VERIFICA (no escribe nada): úsalo primero y revisa los errores.
// Conexión: variable JAGUARES_CONEXION (por defecto la base local de desarrollo).
// Correo para las cuentas internas: variables SMTP_HOST, SMTP_PUERTO, SMTP_USUARIO, SMTP_CLAVE, SMTP_REMITENTE y FRONTEND_URL.
// Sin SMTP las cuentas se crean igual, pero el enlace no se envía: la Directora lo reenvía desde "Usuarios y roles".
if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Uso: FundacionJaguares.Importador <carpeta-con-csv> [--aplicar]");
    return 1;
}
var carpeta = args[0];
var aplicar = args.Contains("--aplicar");
var conexion = Environment.GetEnvironmentVariable("JAGUARES_CONEXION")
               ?? "Host=localhost;Port=5432;Database=fundacion_jaguares;Username=postgres;Password=postgres";

var opciones = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(conexion, npg => npg
        .MapEnum<Rol>("rol_enum", nameTranslator: new NpgsqlNullNameTranslator())
        .MapEnum<MedioPago>("medio_pago_enum", nameTranslator: new NpgsqlNullNameTranslator()))
    .UseSnakeCaseNamingConvention()
    .Options;
await using var db = new AppDbContext(opciones);

var smtp = new SmtpOpciones
{
    Host = Environment.GetEnvironmentVariable("SMTP_HOST"),
    Puerto = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PUERTO"), out var p) ? p : 587,
    Usuario = Environment.GetEnvironmentVariable("SMTP_USUARIO"),
    Password = Environment.GetEnvironmentVariable("SMTP_CLAVE"),
    Remitente = Environment.GetEnvironmentVariable("SMTP_REMITENTE") ?? "no-responder@jaguares.org"
};
var app = new AppOpciones { FrontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:5173" };
var usuarios = new UsuarioService(db, new EmailAdapter(NullLogger<EmailAdapter>.Instance, smtp), app);

Console.WriteLine($"{(aplicar ? "IMPORTANDO" : "VERIFICANDO (sin escribir)")} datos de {Path.GetFullPath(carpeta)}");
if (aplicar && !smtp.Configurado)
    Console.WriteLine("Aviso: SMTP no configurado. Las cuentas internas se crean, pero el enlace para la contraseña no se envía.");

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
