using System.Text.Json;
using FundacionJaguares.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FundacionJaguares.Api;

// JAG-28 / RNF-04: chequeos de salud para el monitoreo de disponibilidad (≥ 99,5 % mensual).
//   GET /health/live  → la API responde (el proceso está vivo). Lo usa Docker para reiniciar el contenedor.
//   GET /health/ready → además la base de datos y la caché responden. Lo consulta el monitor cada minuto.
// Las respuestas no exponen cadenas de conexión ni mensajes internos: solo estado y duración de cada chequeo.
public class BaseDeDatosHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(ct)
                ? HealthCheckResult.Healthy("PostgreSQL responde")
                : HealthCheckResult.Unhealthy("PostgreSQL no responde");
        }
        catch (Exception ex) { return HealthCheckResult.Unhealthy("PostgreSQL no responde", ex); }
    }
}

public class CacheHealthCheck(IDistributedCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(2));
            await cache.SetStringAsync("salud:ping", DateTime.UtcNow.ToString("O"),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) }, limite.Token);
            return HealthCheckResult.Healthy("Caché disponible");
        }
        // La caché solo acelera el panel de indicadores: si falla, la plataforma sigue funcionando (degradada)
        catch (Exception ex) { return HealthCheckResult.Degraded("Caché no disponible", ex); }
    }
}

public static class Salud
{
    public static IServiceCollection AgregarChequeosDeSalud(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<BaseDeDatosHealthCheck>("base-de-datos", HealthStatus.Unhealthy, ["ready"])
            .AddCheck<CacheHealthCheck>("cache", HealthStatus.Degraded, ["ready"]);
        return services;
    }

    public static void MapearChequeosDeSalud(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = EscribirAsync });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains("ready"),
            ResponseWriter = EscribirAsync,
            // Degradado (sin caché) sigue respondiendo 200: la plataforma está disponible
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        });
    }

    private static Task EscribirAsync(HttpContext ctx, HealthReport r)
    {
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.Headers.CacheControl = "no-store";
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            estado = r.Status.ToString(),
            duracionMs = Math.Round(r.TotalDuration.TotalMilliseconds, 1),
            fecha = DateTime.UtcNow,
            chequeos = r.Entries.Select(e => new { nombre = e.Key, estado = e.Value.Status.ToString(), detalle = e.Value.Description,
                                                   duracionMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1) })
        }));
    }
}
