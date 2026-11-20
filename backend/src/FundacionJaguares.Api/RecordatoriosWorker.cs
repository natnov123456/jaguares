using FundacionJaguares.Application.Services;

namespace FundacionJaguares.Api;

// Configuración "Recordatorios" (appsettings / user-secrets)
public class RecordatoriosOpciones
{
    public bool Habilitado { get; set; } = true;
    public int IntervaloMinutos { get; set; } = 60;
}

// JAG-65 / FUJ-119: tarea programada que corre cada hora (al iniciar la API y luego cada IntervaloMinutos)
// y envía los recordatorios de las actividades que empiezan en las próximas 24 h.
public class RecordatoriosWorker(IServiceScopeFactory scopes, RecordatoriosOpciones opciones, ILogger<RecordatoriosWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opciones.Habilitado)
        {
            log.LogInformation("[RECORDATORIOS] Tarea programada deshabilitada (Recordatorios:Habilitado = false)");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, opciones.IntervaloMinutos)));
        do
        {
            try
            {
                // Un scope por ejecución: el DbContext es scoped y no debe vivir entre corridas
                using var scope = scopes.CreateScope();
                var servicio = scope.ServiceProvider.GetRequiredService<IRecordatorioService>();
                await servicio.EnviarPendientesAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // Un error (p. ej. la base no disponible) no debe detener la tarea: se reintenta en la próxima hora
                log.LogError(ex, "[RECORDATORIOS] Error al enviar recordatorios");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
