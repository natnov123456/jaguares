using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRCoder;

namespace FundacionJaguares.Application.Services;

// JAG-65 / FUJ-119: recordatorio por correo 24 h antes de cada curso o evento.
// Lo ejecuta cada hora la tarea programada (RecordatoriosWorker). Cada inscrito recibe UN solo recordatorio:
// antes de enviar se "reserva" la inscripción marcando recordatorio_enviado_en con una actualización condicional,
// así dos ejecuciones simultáneas (o dos servidores) nunca envían el mismo recordatorio.
public interface IRecordatorioService
{
    Task<ResultadoRecordatoriosDto> EnviarPendientesAsync(DateTime ahoraUtc, CancellationToken ct = default);
}

public class RecordatorioService : IRecordatorioService
{
    public static readonly TimeSpan Anticipacion = TimeSpan.FromHours(24);
    // Los cursos solo tienen fecha: se toma como hora de inicio las 08:00 de Bolivia,
    // así el recordatorio llega por la mañana del día anterior y no a medianoche.
    public static readonly TimeOnly HoraInicioCursos = new(8, 0);

    private static readonly string[] EstadosAbiertos = ["PUBLICADO", "EN_CURSO"];

    private readonly AppDbContext _db;
    private readonly IEmailAdapter _email;
    private readonly AppOpciones _app;
    private readonly ILogger<RecordatorioService> _log;

    public RecordatorioService(AppDbContext db, IEmailAdapter email, AppOpciones app, ILogger<RecordatorioService> log)
    { _db = db; _email = email; _app = app; _log = log; }

    // Inicio de un curso en UTC (08:00 de Bolivia = 12:00 UTC)
    public static DateTime InicioCursoUtc(DateOnly fecha) =>
        DateTime.SpecifyKind(fecha.ToDateTime(HoraInicioCursos).AddHours(4), DateTimeKind.Utc);

    public async Task<ResultadoRecordatoriosDto> EnviarPendientesAsync(DateTime ahoraUtc, CancellationToken ct = default)
    {
        var limite = ahoraUtc + Anticipacion;
        int enviados = 0, fallidos = 0;
        var urlPanel = $"{_app.FrontendUrl.TrimEnd('/')}/mi-cuenta";

        // ---- Eventos: con fecha y hora exactas ----
        var eventos = await (from i in _db.InscripcionesEvento
                             join e in _db.Eventos on i.EventoId equals e.Id
                             join u in _db.Usuarios on i.ParticipanteId equals u.Id
                             where i.Estado == "CONFIRMADA" && i.RecordatorioEnviadoEn == null
                                   && EstadosAbiertos.Contains(e.Estado)
                                   && e.FechaInicio > ahoraUtc && e.FechaInicio <= limite
                             select new { i.Id, i.CodigoQr, u.Email, u.NombreCompleto, e.Titulo, e.FechaInicio, e.FechaFin, e.Lugar })
                            .ToListAsync(ct);

        foreach (var x in eventos)
        {
            if (!await ReservarEventoAsync(x.Id, ahoraUtc, ct)) continue;
            var actividad = new ActividadCorreo("EVENTO", x.Titulo, PlantillasCorreo.CuandoEvento(x.FechaInicio, x.FechaFin), x.Lugar);
            var correo = PlantillasCorreo.Recordatorio(x.NombreCompleto, actividad, x.CodigoQr, urlPanel);
            var qr = new AdjuntoCorreo("qr-ingreso.png", QrPng(AsistenciaService.PrefijoQr + x.CodigoQr), "image/png",
                                       PlantillasCorreo.ContentIdQr);
            if (await EnviarAsync(x.Email, correo, qr)) enviados++;
            else { fallidos++; await LiberarEventoAsync(x.Id, ct); }
        }

        // ---- Cursos: solo fecha (se filtra por día y luego por la hora de inicio de referencia) ----
        var desde = DateOnly.FromDateTime(PlantillasCorreo.ABolivia(ahoraUtc));
        var hasta = DateOnly.FromDateTime(PlantillasCorreo.ABolivia(limite));
        var cursos = (await (from i in _db.Inscripciones
                             join c in _db.Cursos on i.CursoId equals c.Id
                             join u in _db.Usuarios on i.ParticipanteId equals u.Id
                             where i.Estado == "CONFIRMADA" && i.RecordatorioEnviadoEn == null
                                   && EstadosAbiertos.Contains(c.Estado)
                                   && c.FechaInicio >= desde && c.FechaInicio <= hasta
                             select new { i.Id, u.Email, u.NombreCompleto, c.Titulo, c.FechaInicio, c.FechaFin })
                            .ToListAsync(ct))
            .Where(x => InicioCursoUtc(x.FechaInicio) > ahoraUtc && InicioCursoUtc(x.FechaInicio) <= limite);

        foreach (var x in cursos)
        {
            if (!await ReservarCursoAsync(x.Id, ahoraUtc, ct)) continue;
            var actividad = new ActividadCorreo("CURSO", x.Titulo, PlantillasCorreo.CuandoCurso(x.FechaInicio, x.FechaFin), null);
            var correo = PlantillasCorreo.Recordatorio(x.NombreCompleto, actividad, null, urlPanel);
            if (await EnviarAsync(x.Email, correo, null)) enviados++;
            else { fallidos++; await LiberarCursoAsync(x.Id, ct); }
        }

        if (enviados + fallidos > 0)
            _log.LogInformation("[RECORDATORIOS] {Enviados} enviados, {Fallidos} con error (se reintentan en la próxima ejecución)",
                enviados, fallidos);
        return new ResultadoRecordatoriosDto(enviados, fallidos);
    }

    public static byte[] QrPng(string contenido)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(datos).GetGraphic(10);
    }

    private async Task<bool> EnviarAsync(string para, CorreoPlantilla correo, AdjuntoCorreo? adjunto)
    {
        try
        {
            await _email.EnviarAsync(para, correo.Asunto, correo.Texto, adjunto: adjunto, html: correo.Html);
            return true;
        }
        catch (Exception ex)
        {
            // Si el correo falla se libera la reserva para reintentarlo en la próxima hora
            _log.LogWarning(ex, "[RECORDATORIOS] No se pudo enviar el recordatorio a {Email}", para);
            return false;
        }
    }

    // Actualización condicional: solo una ejecución consigue marcar la inscripción
    private async Task<bool> ReservarEventoAsync(Guid id, DateTime ahora, CancellationToken ct) =>
        await _db.InscripcionesEvento.Where(i => i.Id == id && i.RecordatorioEnviadoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.RecordatorioEnviadoEn, ahora), ct) == 1;

    private Task LiberarEventoAsync(Guid id, CancellationToken ct) =>
        _db.InscripcionesEvento.Where(i => i.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.RecordatorioEnviadoEn, (DateTime?)null), ct);

    private async Task<bool> ReservarCursoAsync(Guid id, DateTime ahora, CancellationToken ct) =>
        await _db.Inscripciones.Where(i => i.Id == id && i.RecordatorioEnviadoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.RecordatorioEnviadoEn, ahora), ct) == 1;

    private Task LiberarCursoAsync(Guid id, CancellationToken ct) =>
        _db.Inscripciones.Where(i => i.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.RecordatorioEnviadoEn, (DateTime?)null), ct);
}
