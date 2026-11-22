using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FundacionJaguares.Api;
using FundacionJaguares.Application.Reportes;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using FundacionJaguares.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql.NameTranslation;

var builder = WebApplication.CreateBuilder(args);

// 1) DbContext
// Una sola instancia del traductor: si se crea uno nuevo por DbContext, EF Core ve opciones
// distintas en cada request y tras ~20 lanza ManyServiceProvidersCreatedWarning (HTTP 500)
var sinTraduccion = new NpgsqlNullNameTranslator();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"), npg => npg
            .MapEnum<Rol>("rol_enum", nameTranslator: sinTraduccion)
            .MapEnum<MedioPago>("medio_pago_enum", nameTranslator: sinTraduccion))
       .UseSnakeCaseNamingConvention());

// 2) Repositorios
builder.Services.AddScoped<CursoRepository>();
builder.Services.AddScoped<ListaEsperaRepository>();
builder.Services.AddScoped<EventoRepository>();
builder.Services.AddScoped<CertificadoRepository>();
builder.Services.AddScoped<DonacionRepository>();
builder.Services.AddScoped<CampanaRepository>();
builder.Services.AddScoped<VoluntarioRepository>();
builder.Services.AddScoped<ContenidoRepository>();

// 3) Adapters
// Seguridad: bloqueo por intentos fallidos de inicio de sesión y aviso a la Dirección
var seguridad = builder.Configuration.GetSection("Seguridad").Get<SeguridadOpciones>() ?? new SeguridadOpciones();
builder.Services.AddSingleton(seguridad);
builder.Services.AddSingleton<ProteccionAccesos>();
builder.Services.AddSingleton<AlertasSeguridad>();
// No anunciar el servidor web en las respuestas
builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);
// Correo: sección "Smtp" (guárdala con user-secrets, nunca en appsettings.json). Sin Host, solo se registra en el log.
builder.Services.AddSingleton(builder.Configuration.GetSection("Smtp").Get<SmtpOpciones>() ?? new SmtpOpciones());
builder.Services.AddSingleton(builder.Configuration.GetSection("Contacto").Get<ContactoOpciones>() ?? new ContactoOpciones());
// Dirección pública del frontend, para los enlaces de los correos (encuestas FUJ-120)
builder.Services.AddSingleton(builder.Configuration.GetSection("App").Get<AppOpciones>() ?? new AppOpciones());
builder.Services.AddScoped<IEmailAdapter, EmailAdapter>();
builder.Services.AddScoped<IWhatsAppAdapter, WhatsAppAdapter>();
builder.Services.AddScoped<IPaymentAdapter, PaymentAdapter>();

// Archivos subidos (galería): en disco, servidos por la propia API bajo /uploads.
// La carpeta se puede cambiar con "Almacenamiento:RutaLocal" (no se sube al repositorio).
var rutaUploads = builder.Configuration["Almacenamiento:RutaLocal"]
                  ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads");
var almacenamiento = new AlmacenamientoLocal(rutaUploads, "/uploads");
builder.Services.AddSingleton<IAlmacenamientoArchivos>(almacenamiento);

// 4) Services
var cfg = builder.Configuration;
builder.Services.AddScoped<IAuthService>(sp =>
    new AuthService(sp.GetRequiredService<AppDbContext>(),
        cfg["Jwt:Secret"]!, cfg["Jwt:Issuer"]!, cfg["Jwt:Audience"]!));
builder.Services.AddScoped<ICursoService, CursoService>();
builder.Services.AddScoped<IEventoService, EventoService>();
builder.Services.AddScoped<ICertificadoService, CertificadoService>();
builder.Services.AddScoped<IPlantillaService, PlantillaService>();
builder.Services.AddScoped<IDonacionService, DonacionService>();
builder.Services.AddScoped<IVoluntarioService, VoluntarioService>();
builder.Services.AddScoped<IVoluntariadoService, VoluntariadoService>();
builder.Services.AddScoped<IContenidoService, ContenidoService>();
builder.Services.AddScoped<IActividadService, ActividadService>();
builder.Services.AddScoped<IAsistenciaService, AsistenciaService>();
builder.Services.AddScoped<ITransparenciaService, TransparenciaService>();
builder.Services.AddScoped<IGaleriaService, GaleriaService>();
builder.Services.AddScoped<IInstitucionalService, InstitucionalService>();
builder.Services.AddScoped<IContactoService, ContactoService>();
builder.Services.AddScoped<IParticipanteService, ParticipanteService>();
builder.Services.AddScoped<IEncuestaService, EncuestaService>();
// FUJ-122: cuentas del personal y roles
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
// Términos y privacidad aceptados en cada formulario, y registro de actividad (quién entró, cuándo y qué hizo)
builder.Services.AddScoped<IConsentimientoService, ConsentimientoService>();
builder.Services.AddScoped<IRegistroActividadService, RegistroActividadService>();
builder.Services.AddSingleton<FundacionJaguares.Api.ColaRegistroActividad>();
builder.Services.AddHostedService<FundacionJaguares.Api.EscritorRegistroActividad>();
// FUJ-124: reportes exportables (PDF institucional / Excel). Cada módulo aporta sus reportes con un IProveedorReportes.
builder.Services.AddScoped<IReporteService, ReporteService>();
builder.Services.AddScoped<IProveedorReportes, ReportesActividades>();
builder.Services.AddScoped<IProveedorReportes, ReportesGestion>();
// FUJ-125: bitácora de auditoría de donaciones y su reporte exportable
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddScoped<IProveedorReportes, ReportesFinanzas>();
builder.Services.AddScoped<IProveedorReportes, ReportesAdministracion>();
// FUJ-123: panel de indicadores con caché de 10 min. Con "Redis:Conexion" configurado se usa Redis;
// sin él (desarrollo y pruebas) se usa una caché en memoria con la misma interfaz.
var redis = cfg["Redis:Conexion"];
if (!string.IsNullOrWhiteSpace(redis))
    builder.Services.AddStackExchangeRedisCache(o => { o.Configuration = redis; o.InstanceName = "jaguares:"; });
else
    builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<IIndicadorService, IndicadorService>();
builder.Services.AddScoped<IProveedorReportes, ReportesIndicadores>();
builder.Services.AddScoped<IProveedorReportes, ReportesContenido>();
// FUJ-119: recordatorios 24 h antes (tarea programada cada hora) y avisos de reprogramación/cancelación
builder.Services.AddScoped<IRecordatorioService, RecordatorioService>();
builder.Services.AddScoped<ICambiosActividadService, CambiosActividadService>();
builder.Services.AddSingleton(builder.Configuration.GetSection("Recordatorios").Get<RecordatoriosOpciones>() ?? new RecordatoriosOpciones());
builder.Services.AddHostedService<RecordatoriosWorker>();

// 5) JWT Auth
var jwtSecret = cfg["Jwt:Secret"]!;
// JAG-77: en producción no se acepta el secreto de desarrollo que está en appsettings.json ni uno corto
if (builder.Environment.IsProduction() && (jwtSecret == "abcdefghijklmnopqrstuvwxyz123456" || jwtSecret.Length < 32))
    throw new InvalidOperationException("Configura Jwt__Secret (al menos 32 caracteres aleatorios) para producción.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = cfg["Jwt:Issuer"],
            ValidAudience = cfg["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
        // FUJ-122: un token sigue siendo válido hasta 60 min. Si en ese tiempo la Directora desactiva la cuenta
        // o le cambia el rol, el token se rechaza en la siguiente solicitud y la persona debe volver a entrar.
        opt.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var sub = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? ctx.Principal?.FindFirst("sub")?.Value;
                var rol = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                if (!Guid.TryParse(sub, out var id)) { ctx.Fail("Token sin usuario"); return; }
                var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var u = await db.Usuarios.Where(x => x.Id == id).Select(x => new { x.Activo, x.Rol }).FirstOrDefaultAsync();
                if (u is null || !u.Activo || u.Rol.ToString() != rol) ctx.Fail("La cuenta fue desactivada o cambió de rol");
            }
        };
    });
builder.Services.AddAuthorization();

// 6) CORS: en producción solo el frontend publicado (App:FrontendUrl) y los orígenes extra de Cors:Origenes
// (separados por coma) pueden llamar a la API desde el navegador. En desarrollo se acepta cualquiera.
var origenes = new[] { cfg["App:FrontendUrl"] }
    .Concat((cfg["Cors:Origenes"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o!.TrimEnd('/')).Distinct().ToArray();
builder.Services.AddCors(o => o.AddPolicy("front", p =>
{
    if (builder.Environment.IsDevelopment() || origenes.Length == 0) p.AllowAnyOrigin();
    else p.WithOrigins(origenes);
    p.AllowAnyHeader().AllowAnyMethod();
}));

// Donación en línea: endpoint público que envía correos, limitado por IP
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Mensaje claro para la pantalla (sin él, el navegador recibe un 429 vacío)
    o.OnRejected = (ctx, ct) => new ValueTask(ctx.HttpContext.Response.WriteAsJsonAsync(
        new { error = "Demasiadas solicitudes seguidas. Espera un momento y vuelve a intentarlo." }, ct));
    o.AddPolicy("donacion-en-linea", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Max(1, seguridad.DonacionesPorHora), Window = TimeSpan.FromHours(1) }));
    // FUJ-120: el enlace de la encuesta es público (token); se limita para frenar intentos de adivinarlo
    o.AddPolicy("encuesta-publica", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(10) }));
    // "¿Olvidaste tu contraseña?": envía correos, así que se limita por IP para evitar abusos
    o.AddPolicy("recuperar-contrasena", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15) }));
    // Inicio de sesión: frena a quien prueba contraseñas muy rápido (además del bloqueo por fallos)
    o.AddPolicy("inicio-sesion", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Max(1, seguridad.LoginPorMinuto), Window = TimeSpan.FromMinutes(1) }));
    // Registro de cuentas: evita la creación masiva de cuentas falsas desde una misma IP
    o.AddPolicy("registro-cuenta", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Max(1, seguridad.RegistrosPorHora), Window = TimeSpan.FromHours(1) }));
});

// 7) Swagger (compatible con Swashbuckle 10 / Microsoft.OpenApi 2.x)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Fundación Unión de los Jaguares API",
        Version = "v1"
    });

    // Definir el esquema de seguridad (el botón Authorize)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega el token JWT (sin la palabra Bearer)"
    });

    // Aplicar el requerimiento de seguridad (usa OpenApiSecuritySchemeReference)
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// JAG-28 / RNF-04: chequeos de salud para el monitoreo y manejo global de errores
builder.Services.AgregarChequeosDeSalud();
builder.Services.AddProblemDetails();

// Enums como texto en JSON ("EFECTIVO", "DIRECTORA") en lugar de números
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// JAG-77: detrás de Caddy, la IP real del visitante llega en X-Forwarded-For (la usan los límites por IP)
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    if (cfg.GetValue<bool>("Proxy:Confiable"))
    {
        // Solo el proxy de la red interna de Docker puede indicar la IP original
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    }
});

var app = builder.Build();
// Cabeceras de seguridad y redirección a HTTPS (antes de UseForwardedHeaders: lee X-Forwarded-Proto original)
app.UseCabecerasDeSeguridad(seguridad.ForzarHttps);
app.UseForwardedHeaders();

// RNF-04: un error no controlado se registra y responde 500 genérico (sin detalles internos); la API sigue en pie
app.UseExceptionHandler(manejo => manejo.Run(async ctx =>
{
    var error = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(error, "[ERROR] {Metodo} {Ruta}", ctx.Request.Method, ctx.Request.Path);
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await ctx.Response.WriteAsJsonAsync(new { error = "Ocurrió un error inesperado. Ya quedó registrado; intenta de nuevo en unos minutos." });
}));

// La documentación interactiva de la API solo en desarrollo (o si se habilita a propósito con Swagger:Habilitado)
if (app.Environment.IsDevelopment() || cfg.GetValue<bool>("Swagger:Habilitado"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(almacenamiento.Raiz),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        // Los nombres son únicos (GUID): el navegador puede guardarlos en caché por un año
        ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    }
});

app.UseCors("front");
app.UseAuthentication();
// Después de la autenticación (ya se sabe quién es) y antes de la autorización (también quedan los accesos denegados)
app.UseRegistroActividad();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapearChequeosDeSalud();

app.Run();