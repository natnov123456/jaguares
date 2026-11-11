using FundacionJaguares.Api;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FundacionJaguares.Tests.Unitarias;

// JAG-38: plantillas de correo (JAG-66) y tarea programada (JAG-65) sin base de datos
public class RecordatoriosUnitTests
{
    private static readonly ActividadCorreo Evento =
        new("EVENTO", "Feria <b>del</b> reciclaje", "martes 12 de noviembre de 2026, 15:30", "Plaza & Parque");

    [Fact]
    public void Recordatorio_incluye_datos_QR_y_escapa_html()
    {
        var c = PlantillasCorreo.Recordatorio("Ana <script>", Evento, "ABC123", "http://localhost:5173/mi-cuenta");

        Assert.Equal("Recordatorio: Feria <b>del</b> reciclaje es mañana", c.Asunto);
        Assert.Contains("Feria &lt;b&gt;del&lt;/b&gt; reciclaje", c.Html);
        Assert.Contains("Ana &lt;script&gt;", c.Html);
        Assert.DoesNotContain("<script>", c.Html);
        Assert.Contains("Plaza &amp; Parque", c.Html);
        Assert.Contains("cid:" + PlantillasCorreo.ContentIdQr, c.Html);
        Assert.Contains("http://localhost:5173/mi-cuenta", c.Html);
        // El texto plano no lleva etiquetas y no deja líneas vacías dobles
        Assert.Contains("Código: ABC123", c.Texto);
        Assert.DoesNotContain("<table", c.Texto);
        Assert.DoesNotContain("\n\n\n", c.Texto);
    }

    [Fact]
    public void Recordatorio_de_curso_sin_QR_ni_lugar()
    {
        var curso = new ActividadCorreo("CURSO", "Taller", "del lunes 1 al viernes 5", null);
        var c = PlantillasCorreo.Recordatorio("Ana", curso, null, null);
        Assert.DoesNotContain("cid:", c.Html);
        Assert.DoesNotContain("Dónde", c.Texto);
        Assert.Contains("el curso", c.Texto);
    }

    [Fact]
    public void Reprogramada_y_cancelada_muestran_antes_ahora_y_motivo()
    {
        var nueva = Evento with { Cuando = "jueves 14 de noviembre de 2026, 10:00", Lugar = "Coliseo" };
        var r = PlantillasCorreo.Reprogramada("Ana", Evento, nueva, "Lluvia", null);
        Assert.Contains("Antes:", r.Texto);
        Assert.Contains("Ahora: jueves 14", r.Texto);
        Assert.Contains("Motivo: Lluvia", r.Texto);
        Assert.Contains("line-through", r.Html);

        var c = PlantillasCorreo.Cancelada("Ana", Evento, null, "http://x/calendario");
        Assert.StartsWith("Actividad cancelada:", c.Asunto);
        Assert.DoesNotContain("Motivo", c.Texto);
        Assert.Contains("http://x/calendario", c.Html);
    }

    [Fact]
    public void Fechas_en_espanol_y_hora_de_Bolivia()
    {
        var inicio = new DateTime(2026, 11, 12, 19, 30, 0, DateTimeKind.Utc); // 15:30 en Bolivia
        Assert.Equal("jueves 12 de noviembre de 2026, 15:30", PlantillasCorreo.FechaHora(inicio));
        Assert.Equal("jueves 12 de noviembre de 2026, 15:30 a 17:30", PlantillasCorreo.CuandoEvento(inicio, inicio.AddHours(2)));
        Assert.Contains(" al ", PlantillasCorreo.CuandoEvento(inicio, inicio.AddDays(2)));
        Assert.Equal(PlantillasCorreo.FechaHora(inicio), PlantillasCorreo.CuandoEvento(inicio, null));
        var dia = new DateOnly(2026, 11, 12);
        Assert.Equal("jueves 12 de noviembre de 2026", PlantillasCorreo.CuandoCurso(dia, dia));
        Assert.StartsWith("del jueves 12", PlantillasCorreo.CuandoCurso(dia, dia.AddDays(3)));
    }

    [Fact]
    public void Inicio_de_curso_y_QR_en_png()
    {
        Assert.Equal(new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc), RecordatorioService.InicioCursoUtc(new DateOnly(2026, 11, 12)));
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, RecordatorioService.QrPng("JAGUARES-EV:abc")[..4]);
    }

    // ---------- Tarea programada ----------

    private sealed class ServicioFalso(bool falla) : IRecordatorioService
    {
        public int Llamadas;
        public Task<ResultadoRecordatoriosDto> EnviarPendientesAsync(DateTime ahoraUtc, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Llamadas);
            return falla ? throw new InvalidOperationException("base caída") : Task.FromResult(new ResultadoRecordatoriosDto(0, 0));
        }
    }

    private static (RecordatoriosWorker Worker, ServicioFalso Servicio) Crear(bool habilitado, bool falla = false)
    {
        var servicio = new ServicioFalso(falla);
        var proveedor = new ServiceCollection().AddSingleton<IRecordatorioService>(servicio).BuildServiceProvider();
        var worker = new RecordatoriosWorker(proveedor.GetRequiredService<IServiceScopeFactory>(),
            new RecordatoriosOpciones { Habilitado = habilitado, IntervaloMinutos = 60 }, NullLogger<RecordatoriosWorker>.Instance);
        return (worker, servicio);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)] // si la base falla, la tarea registra el error y sigue viva
    public async Task Worker_habilitado_ejecuta_al_iniciar(bool falla)
    {
        var (worker, servicio) = Crear(habilitado: true, falla);
        await worker.StartAsync(CancellationToken.None);
        for (var i = 0; i < 50 && servicio.Llamadas == 0; i++) await Task.Delay(20);
        await worker.StopAsync(CancellationToken.None);
        Assert.Equal(1, servicio.Llamadas);
    }

    [Fact]
    public async Task Worker_deshabilitado_no_ejecuta()
    {
        var (worker, servicio) = Crear(habilitado: false);
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);
        Assert.Equal(0, servicio.Llamadas);
    }
}
