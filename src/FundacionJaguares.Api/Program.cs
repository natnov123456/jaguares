using System.Text;
using System.Text.Json.Serialization;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using FundacionJaguares.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddScoped<EventoRepository>();
builder.Services.AddScoped<CertificadoRepository>();
builder.Services.AddScoped<DonacionRepository>();
builder.Services.AddScoped<VoluntarioRepository>();
builder.Services.AddScoped<ContenidoRepository>();

// 3) Adapters
builder.Services.AddScoped<IEmailAdapter, EmailAdapter>();
builder.Services.AddScoped<IWhatsAppAdapter, WhatsAppAdapter>();
builder.Services.AddScoped<IPaymentAdapter, PaymentAdapter>();

// 4) Services
var cfg = builder.Configuration;
builder.Services.AddScoped<IAuthService>(sp =>
    new AuthService(sp.GetRequiredService<AppDbContext>(),
        cfg["Jwt:Secret"]!, cfg["Jwt:Issuer"]!, cfg["Jwt:Audience"]!));
builder.Services.AddScoped<ICursoService, CursoService>();
builder.Services.AddScoped<IEventoService, EventoService>();
builder.Services.AddScoped<ICertificadoService, CertificadoService>();
builder.Services.AddScoped<IDonacionService, DonacionService>();
builder.Services.AddScoped<IVoluntarioService, VoluntarioService>();
builder.Services.AddScoped<IContenidoService, ContenidoService>();

// 5) JWT Auth
var jwtSecret = cfg["Jwt:Secret"]!;
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
    });
builder.Services.AddAuthorization();

// 6) CORS
builder.Services.AddCors(o => o.AddPolicy("front", p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

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

// Enums como texto en JSON ("EFECTIVO", "DIRECTORA") en lugar de números
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("front");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();