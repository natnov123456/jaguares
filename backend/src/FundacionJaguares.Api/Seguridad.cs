using System.Collections.Concurrent;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Api;

// Configuración "Seguridad" (appsettings / variables de entorno Seguridad__...)
public class SeguridadOpciones
{
    // Fallos de inicio de sesión de una misma cuenta antes de bloquearla temporalmente
    public int IntentosPorCuenta { get; set; } = 5;
    // Fallos desde una misma IP (contra cualquier cuenta) antes de bloquear esa IP temporalmente
    public int IntentosPorIp { get; set; } = 20;
    public int MinutosBloqueo { get; set; } = 15;
    // Límites de solicitudes por IP (además del bloqueo por fallos)
    public int LoginPorMinuto { get; set; } = 10;
    public int RegistrosPorHora { get; set; } = 10;
    // Redirige a HTTPS las solicitudes que llegan por HTTP a través del proxy (Render, Caddy)
    public bool ForzarHttps { get; set; }
}

// Protección contra fuerza bruta en el inicio de sesión: cuenta los fallos por cuenta y por IP en una ventana
// de MinutosBloqueo y, al llegar al límite, bloquea ese correo o esa IP durante MinutosBloqueo.
// Vive en memoria: con una sola instancia del backend alcanza; al reiniciar se limpia.
public sealed class ProteccionAccesos(SeguridadOpciones opciones)
{
    private sealed class Contador
    {
        public int Fallos;
        public DateTime Inicio;
        public DateTime? BloqueadoHasta;
    }

    private readonly ConcurrentDictionary<string, Contador> _contadores = new();
    private int _operaciones;

    private TimeSpan Ventana => TimeSpan.FromMinutes(Math.Max(1, opciones.MinutosBloqueo));
    private static string Cuenta(string? email) => "c:" + (email ?? "").Trim().ToLowerInvariant();
    private static string Ip(string? ip) => "i:" + (string.IsNullOrWhiteSpace(ip) ? "desconocida" : ip);

    // Tiempo que falta para poder intentar de nuevo (null = no está bloqueado)
    public TimeSpan? Bloqueo(string? email, string? ip, DateTime? ahora = null)
    {
        var t = ahora ?? DateTime.UtcNow;
        TimeSpan? Resta(string clave) =>
            _contadores.TryGetValue(clave, out var c) && c.BloqueadoHasta is { } hasta && hasta > t ? hasta - t : null;
        var a = Resta(Cuenta(email));
        var b = Resta(Ip(ip));
        return a is null ? b : b is null ? a : (a > b ? a : b);
    }

    // Devuelve true si este fallo provocó un bloqueo nuevo (de la cuenta o de la IP)
    public bool RegistrarFallo(string? email, string? ip, DateTime? ahora = null)
    {
        var t = ahora ?? DateTime.UtcNow;
        if (Interlocked.Increment(ref _operaciones) % 500 == 0) Limpiar(t);
        var nuevaCuenta = Sumar(Cuenta(email), opciones.IntentosPorCuenta, t);
        var nuevaIp = Sumar(Ip(ip), opciones.IntentosPorIp, t);
        return nuevaCuenta || nuevaIp;
    }

    // Un inicio de sesión correcto reinicia el contador de esa cuenta (no el de la IP)
    public void RegistrarExito(string? email) => _contadores.TryRemove(Cuenta(email), out _);

    private bool Sumar(string clave, int limite, DateTime t)
    {
        var c = _contadores.GetOrAdd(clave, _ => new Contador { Inicio = t });
        lock (c)
        {
            if (c.BloqueadoHasta is { } hasta && hasta > t) return false;
            if (c.BloqueadoHasta is not null || t - c.Inicio > Ventana) { c.Fallos = 0; c.Inicio = t; c.BloqueadoHasta = null; }
            c.Fallos++;
            if (c.Fallos < Math.Max(1, limite)) return false;
            c.BloqueadoHasta = t + Ventana;
            return true;
        }
    }

    private void Limpiar(DateTime t)
    {
        foreach (var (clave, c) in _contadores)
            if ((c.BloqueadoHasta is { } h ? h : c.Inicio + Ventana) < t) _contadores.TryRemove(clave, out _);
    }
}

// Aviso por correo a la Dirección cuando se bloquea una cuenta o una IP por intentos fallidos.
// Se envía en segundo plano para no demorar la respuesta; si falla, queda en el log.
public sealed class AlertasSeguridad(IServiceScopeFactory scopes, ILogger<AlertasSeguridad> log)
{
    public void AvisarBloqueo(string? email, string? ip, int minutos)
    {
        log.LogWarning("[SEGURIDAD] Inicio de sesión bloqueado {Minutos} min por intentos fallidos. Cuenta: {Email} · IP: {Ip}",
            minutos, email, ip);
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var correo = scope.ServiceProvider.GetRequiredService<IEmailAdapter>();
                var contacto = scope.ServiceProvider.GetRequiredService<FundacionJaguares.Application.Services.ContactoOpciones>();
                var destino = await db.PaginaInstitucional.Where(p => p.Id == 1).Select(p => p.EmailContacto).FirstOrDefaultAsync()
                              ?? contacto.EmailDestino;
                if (string.IsNullOrWhiteSpace(destino)) return;
                await correo.EnviarAsync(destino, "Alerta de seguridad: intentos fallidos de inicio de sesión",
                    $"Se bloquearon temporalmente ({minutos} minutos) los inicios de sesión por varios intentos fallidos seguidos.\n\n" +
                    $"Cuenta intentada: {email}\nIP de origen: {ip}\nFecha: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n" +
                    "Si no fuiste tú ni alguien del equipo, revisa Panel → Gestión → Registro de actividad (filtro \"Sospechosos\").");
            }
            catch (Exception ex)
            {
                log.LogError(ex, "[SEGURIDAD] No se pudo enviar el aviso de bloqueo");
            }
        });
    }
}

public static class CabecerasSeguridad
{
    // Cabeceras de seguridad para todas las respuestas de la API y redirección a HTTPS detrás del proxy.
    // Va antes de UseForwardedHeaders para leer la cabecera X-Forwarded-Proto tal como la manda el proxy.
    public static IApplicationBuilder UseCabecerasDeSeguridad(this IApplicationBuilder app, bool forzarHttps)
    {
        return app.Use(async (ctx, siguiente) =>
        {
            var protocolo = ctx.Request.Headers["X-Forwarded-Proto"].ToString();
            var ruta = ctx.Request.Path;
            // El chequeo de salud interno del proxy llega por HTTP y no debe redirigirse
            if (forzarHttps && protocolo.Equals("http", StringComparison.OrdinalIgnoreCase) && !ruta.StartsWithSegments("/health"))
            {
                ctx.Response.StatusCode = StatusCodes.Status308PermanentRedirect;
                ctx.Response.Headers.Location = $"https://{ctx.Request.Host}{ctx.Request.PathBase}{ruta}{ctx.Request.QueryString}";
                return;
            }
            var https = ctx.Request.IsHttps || protocolo.Equals("https", StringComparison.OrdinalIgnoreCase);
            ctx.Response.OnStarting(() =>
            {
                var h = ctx.Response.Headers;
                h["X-Content-Type-Options"] = "nosniff";
                h["X-Frame-Options"] = "DENY";
                h["Referrer-Policy"] = "strict-origin-when-cross-origin";
                h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                h["Cross-Origin-Opener-Policy"] = "same-origin";
                // La API solo devuelve datos (JSON, PDF, imágenes): nada de scripts ni marcos. Swagger necesita los suyos.
                if (!ruta.StartsWithSegments("/swagger"))
                    h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
                if (https) h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
                // Los datos personales no se guardan en cachés intermedias ni del navegador
                if (ruta.StartsWithSegments("/api") && !h.ContainsKey("Cache-Control")) h.CacheControl = "no-store";
                h.Remove("X-Powered-By");
                return Task.CompletedTask;
            });
            await siguiente();
        });
    }
}
