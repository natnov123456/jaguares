using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using FundacionJaguares.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FundacionJaguares.Application.Services;

// JAG-65 / FUJ-119: cuando el personal reprograma o cancela una actividad, todos los inscritos reciben un aviso.
// Al reprogramar se borra la marca de recordatorio: el recordatorio se vuelve a enviar 24 h antes de la fecha nueva.
public interface ICambiosActividadService
{
    Task<CambioActividadDto> ReprogramarEventoAsync(Guid eventoId, ReprogramarEventoDto dto);
    Task<CambioActividadDto> CancelarEventoAsync(Guid eventoId, string? motivo);
    // docenteId != null: solo puede modificar sus propios cursos
    Task<CambioActividadDto> ReprogramarCursoAsync(Guid cursoId, ReprogramarCursoDto dto, Guid? docenteId);
    Task<CambioActividadDto> CancelarCursoAsync(Guid cursoId, string? motivo, Guid? docenteId);
}

public class CambiosActividadService : ICambiosActividadService
{
    public const int MaxMotivo = 300;
    private static readonly string[] EstadosModificables = ["PUBLICADO", "EN_CURSO"];

    private readonly AppDbContext _db;
    private readonly IEmailAdapter _email;
    private readonly ListaEsperaRepository _espera;
    private readonly AppOpciones _app;
    private readonly ILogger<CambiosActividadService> _log;

    public CambiosActividadService(AppDbContext db, IEmailAdapter email, ListaEsperaRepository espera, AppOpciones app,
                                   ILogger<CambiosActividadService> log)
    { _db = db; _email = email; _espera = espera; _app = app; _log = log; }

    private string Url(string ruta) => $"{_app.FrontendUrl.TrimEnd('/')}{ruta}";

    // ---------- Eventos ----------

    public async Task<CambioActividadDto> ReprogramarEventoAsync(Guid eventoId, ReprogramarEventoDto dto)
    {
        var motivo = Motivo(dto.Motivo);
        var e = await _db.Eventos.FindAsync(eventoId) ?? throw new KeyNotFoundException("Evento no encontrado");
        ValidarModificable(e.Estado, "evento");

        var inicio = DateTime.SpecifyKind(dto.FechaInicio.ToUniversalTime(), DateTimeKind.Utc);
        DateTime? fin = dto.FechaFin is { } f ? DateTime.SpecifyKind(f.ToUniversalTime(), DateTimeKind.Utc) : null;
        if (inicio <= DateTime.UtcNow) throw new ArgumentException("La nueva fecha debe ser posterior a este momento");
        if (fin is not null && fin < inicio) throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio");
        var lugar = string.IsNullOrWhiteSpace(dto.Lugar) ? e.Lugar : dto.Lugar.Trim();
        if (lugar?.Length > 200) throw new ArgumentException("El lugar no puede superar 200 caracteres");
        if (inicio == e.FechaInicio && fin == e.FechaFin && lugar == e.Lugar)
            throw new ArgumentException("No hay cambios: indica una fecha o un lugar distintos");

        var anterior = new ActividadCorreo("EVENTO", e.Titulo, PlantillasCorreo.CuandoEvento(e.FechaInicio, e.FechaFin), e.Lugar);
        e.FechaInicio = inicio;
        e.FechaFin = fin;
        e.Lugar = lugar;
        await _db.SaveChangesAsync();
        await _db.InscripcionesEvento.Where(i => i.EventoId == eventoId && i.Estado == "CONFIRMADA")
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.RecordatorioEnviadoEn, (DateTime?)null));

        var nueva = new ActividadCorreo("EVENTO", e.Titulo, PlantillasCorreo.CuandoEvento(inicio, fin), lugar);
        var notificados = await AvisarAsync(await InscritosEventoAsync(eventoId),
            nombre => PlantillasCorreo.Reprogramada(nombre, anterior, nueva, motivo, Url("/mi-cuenta")));
        return new CambioActividadDto(e.Id, e.Estado, notificados);
    }

    public async Task<CambioActividadDto> CancelarEventoAsync(Guid eventoId, string? motivo)
    {
        var m = Motivo(motivo);
        var e = await _db.Eventos.FindAsync(eventoId) ?? throw new KeyNotFoundException("Evento no encontrado");
        ValidarModificable(e.Estado, "evento");
        if ((e.FechaFin ?? e.FechaInicio) < DateTime.UtcNow) throw new InvalidOperationException("El evento ya se realizó");

        // Condicional: si dos personas cancelan a la vez, solo una envía los avisos
        var filas = await _db.Eventos.Where(x => x.Id == eventoId && EstadosModificables.Contains(x.Estado))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, "CANCELADO"));
        if (filas == 0) throw new InvalidOperationException("El evento ya no se puede cancelar");

        var actividad = new ActividadCorreo("EVENTO", e.Titulo, PlantillasCorreo.CuandoEvento(e.FechaInicio, e.FechaFin), e.Lugar);
        var notificados = await AvisarAsync(await InscritosEventoAsync(eventoId),
            nombre => PlantillasCorreo.Cancelada(nombre, actividad, m, Url("/calendario")));
        return new CambioActividadDto(e.Id, "CANCELADO", notificados);
    }

    // ---------- Cursos ----------

    public async Task<CambioActividadDto> ReprogramarCursoAsync(Guid cursoId, ReprogramarCursoDto dto, Guid? docenteId)
    {
        var motivo = Motivo(dto.Motivo);
        var c = await _db.Cursos.FindAsync(cursoId) ?? throw new KeyNotFoundException("Curso no encontrado");
        if (docenteId is not null && c.DocenteId != docenteId)
            throw new UnauthorizedAccessException("Solo puedes modificar tus propios cursos");
        ValidarModificable(c.Estado, "curso");

        var hoy = DateOnly.FromDateTime(PlantillasCorreo.ABolivia(DateTime.UtcNow));
        if (dto.FechaInicio < hoy) throw new ArgumentException("La nueva fecha de inicio no puede estar en el pasado");
        if (dto.FechaFin < dto.FechaInicio) throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio");
        if (dto.FechaInicio == c.FechaInicio && dto.FechaFin == c.FechaFin)
            throw new ArgumentException("No hay cambios: indica fechas distintas");

        var anterior = new ActividadCorreo("CURSO", c.Titulo, PlantillasCorreo.CuandoCurso(c.FechaInicio, c.FechaFin), null);
        c.FechaInicio = dto.FechaInicio;
        c.FechaFin = dto.FechaFin;
        await _db.SaveChangesAsync();
        await _db.Inscripciones.Where(i => i.CursoId == cursoId && i.Estado == "CONFIRMADA")
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.RecordatorioEnviadoEn, (DateTime?)null));

        var nueva = new ActividadCorreo("CURSO", c.Titulo, PlantillasCorreo.CuandoCurso(c.FechaInicio, c.FechaFin), null);
        var destinatarios = (await InscritosCursoAsync(cursoId)).Concat(await EnEsperaCursoAsync(cursoId)).ToList();
        var notificados = await AvisarAsync(destinatarios,
            nombre => PlantillasCorreo.Reprogramada(nombre, anterior, nueva, motivo, Url("/mi-cuenta")));
        return new CambioActividadDto(c.Id, c.Estado, notificados);
    }

    public async Task<CambioActividadDto> CancelarCursoAsync(Guid cursoId, string? motivo, Guid? docenteId)
    {
        var m = Motivo(motivo);
        var c = await _db.Cursos.FindAsync(cursoId) ?? throw new KeyNotFoundException("Curso no encontrado");
        if (docenteId is not null && c.DocenteId != docenteId)
            throw new UnauthorizedAccessException("Solo puedes modificar tus propios cursos");
        ValidarModificable(c.Estado, "curso");

        // Se leen antes de cerrar la lista de espera: también se les avisa
        var destinatarios = (await InscritosCursoAsync(cursoId)).Concat(await EnEsperaCursoAsync(cursoId)).ToList();
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            var filas = await _db.Cursos.Where(x => x.Id == cursoId && EstadosModificables.Contains(x.Estado))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, "CANCELADO"));
            if (filas == 0) throw new InvalidOperationException("El curso ya no se puede cancelar");
            await _espera.RetirarTodosAsync(cursoId);
            await tx.CommitAsync();
        }

        var actividad = new ActividadCorreo("CURSO", c.Titulo, PlantillasCorreo.CuandoCurso(c.FechaInicio, c.FechaFin), null);
        var notificados = await AvisarAsync(destinatarios,
            nombre => PlantillasCorreo.Cancelada(nombre, actividad, m, Url("/calendario")));
        return new CambioActividadDto(c.Id, "CANCELADO", notificados);
    }

    // ---------- auxiliares ----------

    private static string? Motivo(string? motivo)
    {
        var m = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        if (m?.Length > MaxMotivo) throw new ArgumentException($"El motivo no puede superar {MaxMotivo} caracteres");
        return m;
    }

    private static void ValidarModificable(string estado, string tipo)
    {
        if (estado == "CANCELADO") throw new InvalidOperationException($"El {tipo} ya fue cancelado");
        if (estado == "FINALIZADO") throw new InvalidOperationException($"El {tipo} ya finalizó");
        if (!EstadosModificables.Contains(estado)) throw new InvalidOperationException($"El {tipo} no está publicado");
    }

    private async Task<List<(string Email, string Nombre)>> InscritosEventoAsync(Guid eventoId) =>
        (await (from i in _db.InscripcionesEvento
                join u in _db.Usuarios on i.ParticipanteId equals u.Id
                where i.EventoId == eventoId && i.Estado == "CONFIRMADA"
                select new { u.Email, u.NombreCompleto }).ToListAsync())
        .Select(x => (x.Email, x.NombreCompleto)).ToList();

    private async Task<List<(string Email, string Nombre)>> InscritosCursoAsync(Guid cursoId) =>
        (await (from i in _db.Inscripciones
                join u in _db.Usuarios on i.ParticipanteId equals u.Id
                where i.CursoId == cursoId && i.Estado == "CONFIRMADA"
                select new { u.Email, u.NombreCompleto }).ToListAsync())
        .Select(x => (x.Email, x.NombreCompleto)).ToList();

    private async Task<List<(string Email, string Nombre)>> EnEsperaCursoAsync(Guid cursoId) =>
        (await (from l in _db.ListaEspera
                join u in _db.Usuarios on l.ParticipanteId equals u.Id
                where l.CursoId == cursoId && l.Estado == "EN_ESPERA"
                select new { u.Email, u.NombreCompleto }).ToListAsync())
        .Select(x => (x.Email, x.NombreCompleto)).ToList();

    // El cambio ya está guardado: si un aviso falla se registra y se sigue con los demás
    private async Task<int> AvisarAsync(List<(string Email, string Nombre)> destinatarios, Func<string, CorreoPlantilla> plantilla)
    {
        var enviados = 0;
        foreach (var (email, nombre) in destinatarios.DistinctBy(d => d.Email.ToLowerInvariant()))
        {
            var correo = plantilla(nombre);
            try
            {
                await _email.EnviarAsync(email, correo.Asunto, correo.Texto, html: correo.Html);
                enviados++;
            }
            catch (Exception ex) { _log.LogWarning(ex, "No se pudo avisar del cambio de actividad a {Email}", email); }
        }
        return enviados;
    }
}
