using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FundacionJaguares.Tests.Infraestructura;

// Levanta la API completa en memoria contra una base PostgreSQL de pruebas propia de esta ejecución.
// La base se crea con los mismos scripts de la carpeta db/ (01, 02, 03...) y se borra al terminar,
// así las pruebas usan exactamente el mismo esquema que producción y nunca tocan la base de desarrollo.
//
// Servidor: variable de entorno JAGUARES_TEST_PG (cadena de conexión SIN "Database").
// Por defecto: Host=localhost;Port=5432;Username=postgres;Password=postgres
public sealed class ApiFixture : IAsyncLifetime
{
    public const string VariableServidor = "JAGUARES_TEST_PG";
    private const string ServidorPorDefecto = "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    public static readonly JsonSerializerOptions Json = CrearOpcionesJson();

    private readonly Dictionary<string, string> _tokens = new();
    private string _servidor = "";
    private string _nombreBd = "";

    public string CadenaConexion { get; private set; } = "";
    public string CarpetaUploads { get; } = Path.Combine(Path.GetTempPath(), "jaguares-tests", Guid.NewGuid().ToString("N"));
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    private static JsonSerializerOptions CrearOpcionesJson()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }

    public async Task InitializeAsync()
    {
        _servidor = Environment.GetEnvironmentVariable(VariableServidor) ?? ServidorPorDefecto;
        _nombreBd = "jaguares_test_" + Guid.NewGuid().ToString("N")[..12];

        var admin = new NpgsqlConnectionStringBuilder(_servidor) { Database = "postgres" }.ConnectionString;
        try
        {
            await using var cn = new NpgsqlConnection(admin);
            await cn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_nombreBd}\"", cn);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) when (ex is NpgsqlException or System.Net.Sockets.SocketException)
        {
            throw new InvalidOperationException(
                $"No se pudo conectar a PostgreSQL para las pruebas ({ex.Message}). " +
                $"Configura la variable de entorno {VariableServidor} con tu servidor, por ejemplo: " +
                "Host=localhost;Port=5432;Username=postgres;Password=TU_CONTRASEÑA", ex);
        }

        CadenaConexion = new NpgsqlConnectionStringBuilder(_servidor) { Database = _nombreBd }.ConnectionString;
        await EjecutarScriptsAsync();

        Directory.CreateDirectory(CarpetaUploads);
        Factory = new ApiFactory(CadenaConexion, CarpetaUploads);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        var admin = new NpgsqlConnectionStringBuilder(_servidor) { Database = "postgres" }.ConnectionString;
        await using (var cn = new NpgsqlConnection(admin))
        {
            await cn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_nombreBd}\" WITH (FORCE)", cn);
            await cmd.ExecuteNonQueryAsync();
        }
        try { Directory.Delete(CarpetaUploads, recursive: true); } catch (IOException) { }
    }

    // Ejecuta db/*.sql en orden (01_schema, 02_seed, 03...) igual que se indica en el README
    private async Task EjecutarScriptsAsync()
    {
        var carpetaDb = BuscarCarpetaDb();
        await using var cn = new NpgsqlConnection(CadenaConexion);
        await cn.OpenAsync();
        foreach (var archivo in Directory.GetFiles(carpetaDb, "*.sql").Order(StringComparer.Ordinal))
        {
            var sql = await File.ReadAllTextAsync(archivo); // ReadAllText quita el BOM de 01_schema.sql
            await using var cmd = new NpgsqlCommand(sql, cn);
            try { await cmd.ExecuteNonQueryAsync(); }
            catch (PostgresException ex)
            {
                throw new InvalidOperationException($"Falló el script {Path.GetFileName(archivo)}: {ex.MessageText}", ex);
            }
        }
    }

    private static string BuscarCarpetaDb()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidata = Path.Combine(dir.FullName, "db");
            if (File.Exists(Path.Combine(candidata, "01_schema.sql"))) return candidata;
        }
        throw new DirectoryNotFoundException("No se encontró la carpeta db/ con 01_schema.sql");
    }

    // ---------- Clientes HTTP ----------

    public HttpClient Anonimo() => Factory.CreateClient();

    public async Task<HttpClient> ComoAsync(string email, string password)
    {
        if (!_tokens.TryGetValue(email, out var token))
        {
            var r = await Anonimo().PostAsJsonAsync("/api/Auth/login", new { email, password });
            r.EnsureSuccessStatusCode();
            token = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
            _tokens[email] = token;
        }
        var c = Factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    public Task<HttpClient> DirectoraAsync() => ComoAsync(Usuarios.Directora, "Admin123!");
    public Task<HttpClient> DocenteAsync() => ComoAsync(Usuarios.Docente, "Docente123!");
    public Task<HttpClient> EncargadoAsync() => ComoAsync(Usuarios.Encargado, "Eventos123!");
    public Task<HttpClient> VoluntarioAsync() => ComoAsync(Usuarios.Voluntario, "Vol123!");
    public Task<HttpClient> JuanAsync() => ComoAsync(Usuarios.Juan, "Juan123!");

    // Participante nuevo (registro público) para pruebas que necesitan a alguien "sin historial"
    public async Task<(HttpClient Cliente, Guid Id, string Email)> NuevoParticipanteAsync(string? nombre = null)
    {
        var email = $"p{Guid.NewGuid().ToString("N")[..12]}@prueba.bo";
        var r = await Anonimo().PostAsJsonAsync("/api/Auth/registro",
            new { nombreCompleto = nombre ?? "Participante Prueba", email, password = "Clave123!", telefono = (string?)null });
        r.EnsureSuccessStatusCode();
        var cliente = await ComoAsync(email, "Clave123!");
        return (cliente, await IdDeAsync(email), email);
    }

    public async Task<Guid> IdDeAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Usuarios.Single(u => u.Email == email).Id;
    }

    // Acceso directo a servicios/BD para preparar datos que la API no permite crear (p. ej. fechas pasadas)
    public async Task EnBdAsync(Func<AppDbContext, Task> accion)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await accion(db);
        await db.SaveChangesAsync();
    }

    public T Servicio<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    // Correos que la API "envió" durante las pruebas (no sale nada a internet)
    public CorreosCapturados Correos => Factory.Services.GetRequiredService<CorreosCapturados>();

    private sealed class ApiFactory(string cadena, string uploads) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // "Testing": no carga appsettings.Development ni user-secrets de la máquina
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Default", cadena);
            builder.UseSetting("Almacenamiento:RutaLocal", uploads);
            builder.UseSetting("Smtp:Host", "");
            // La tarea programada de recordatorios no corre sola: las pruebas la invocan con la hora que necesitan
            builder.UseSetting("Recordatorios:Habilitado", "false");
            builder.ConfigureLogging(l => l.ClearProviders());
            builder.ConfigureTestServices(s =>
            {
                s.AddSingleton<CorreosCapturados>();
                s.AddScoped<IEmailAdapter>(sp => sp.GetRequiredService<CorreosCapturados>());
            });
        }
    }
}

public static class Usuarios
{
    // Usuarios creados por db/02_seed_usuarios.sql
    public const string Directora = "directora@jaguares.org";
    public const string Docente = "docente@jaguares.org";
    public const string Encargado = "eventos@jaguares.org";
    public const string Voluntario = "voluntario@example.com";
    public const string Juan = "juan@example.com";
}

[CollectionDefinition(Nombre)]
public class ColeccionApi : ICollectionFixture<ApiFixture>
{
    public const string Nombre = "api";
}
