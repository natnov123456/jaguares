using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FundacionJaguares.Application.Services;

// FUJ-118: el coordinador asigna voluntarios a eventos o cursos publicados, cada voluntario es
// notificado y ve la asignación en su portal; al terminar la actividad se registran las horas,
// que se acumulan en su historial.
public interface IVoluntariadoService
{
    Task<List<ActividadAsignableDto>> ActividadesAsignablesAsync();
    Task<ResultadoAsignacionDto> AsignarAsync(AsignarVoluntariosDto dto, Guid asignadoPor);
    Task<List<AsignacionDto>> ListarAsync(Guid? eventoId, Guid? cursoId);
    Task CancelarAsync(Guid asignacionId);
    Task<AsignacionDto> RegistrarHorasAsync(Guid asignacionId, decimal horas, Guid registradoPor);
    Task<List<AsignacionDto>> MisAsignacionesAsync(Guid usuarioId, string? email);
}

public class VoluntariadoService : IVoluntariadoService
{
    public const decimal MaxHoras = 200m;
    public const int MaxPorAsignacion = 50;
    private const int MaxTarea = 200;
    private static readonly string[] EstadosAbiertos = ["PUBLICADO", "EN_CURSO"];

    private readonly AppDbContext _db;
    private readonly IEmailAdapter _email;
    private readonly IWhatsAppAdapter _whatsApp;
    private readonly ILogger<VoluntariadoService> _log;

    public VoluntariadoService(AppDbContext db, IEmailAdapter email, IWhatsAppAdapter whatsApp, ILogger<VoluntariadoService> log)
    { _db = db; _email = email; _whatsApp = whatsApp; _log = log; }

    public static void ValidarHoras(decimal horas)
    {
        if (horas <= 0 || horas > MaxHoras)
            throw new ArgumentException($"Las horas deben ser mayores a 0 y como máximo {MaxHoras}");
        if (decimal.Round(horas, 2) != horas)
            throw new ArgumentException("Las horas admiten como máximo 2 decimales (ej. 3.5)");
    }

    // ---------- Actividades ----------

    private record Actividad(string Tipo, Guid Id, string Titulo, string? Lugar, DateTime Inicio, DateTime? Fin,
                             bool TodoElDia, string Estado)
    {
        private DateOnly FinCurso => DateOnly.FromDateTime(Fin ?? Inicio);

        public bool Finalizada(DateTime ahora) => Tipo == "CURSO"
            ? Estado == "FINALIZADO" || FinCurso < DateOnly.FromDateTime(ahora)
            : Estado == "FINALIZADO" || (Fin ?? Inicio) <= ahora;

        // Se asigna a actividades publicadas que todavía no terminaron
        public bool Asignable(DateTime ahora) => EstadosAbiertos.Contains(Estado) && !Finalizada(ahora);
    }

    private static Actividad DeEvento(Evento e) =>
        new("EVENTO", e.Id, e.Titulo, e.Lugar, e.FechaInicio, e.FechaFin, false, e.Estado);

    private static Actividad DeCurso(Curso c) =>
        new("CURSO", c.Id, c.Titulo, null, c.FechaInicio.ToDateTime(TimeOnly.MinValue),
            c.FechaFin.ToDateTime(TimeOnly.MinValue), true, c.Estado);

    public async Task<List<ActividadAsignableDto>> ActividadesAsignablesAsync()
    {
        var ahora = DateTime.UtcNow;
        var hoy = DateOnly.FromDateTime(ahora);
        var eventos = (await _db.Eventos
                .Where(e => EstadosAbiertos.Contains(e.Estado) && (e.FechaFin ?? e.FechaInicio) > ahora).ToListAsync())
            .Select(DeEvento);
        var cursos = (await _db.Cursos
                .Where(c => EstadosAbiertos.Contains(c.Estado) && c.FechaFin >= hoy).ToListAsync())
            .Select(DeCurso);
        var actividades = eventos.Concat(cursos).ToList();

        var ids = actividades.Select(a => a.Id).ToList();
        var conteo = (await _db.AsignacionesVoluntario
                .Where(a => a.Estado != "CANCELADA" &&
                            ((a.EventoId != null && ids.Contains(a.EventoId.Value)) || (a.CursoId != null && ids.Contains(a.CursoId.Value))))
                .Select(a => a.EventoId ?? a.CursoId)
                .ToListAsync())
            .GroupBy(id => id!.Value).ToDictionary(g => g.Key, g => g.Count());

        return actividades
            .OrderBy(a => a.Inicio)
            .Select(a => new ActividadAsignableDto(a.Tipo, a.Id, a.Titulo, a.Lugar, a.Inicio, a.Fin, a.TodoElDia,
                conteo.GetValueOrDefault(a.Id)))
            .ToList();
    }

    private async Task<Actividad> ActividadDeAsync(Guid? eventoId, Guid? cursoId)
    {
        if ((eventoId is null) == (cursoId is null))
            throw new ArgumentException("Indica un evento o un curso (solo uno)");
        if (eventoId is not null)
            return DeEvento(await _db.Eventos.FindAsync(eventoId.Value) ?? throw new KeyNotFoundException("Evento no encontrado"));
        return DeCurso(await _db.Cursos.FindAsync(cursoId!.Value) ?? throw new KeyNotFoundException("Curso no encontrado"));
    }

    // ---------- Asignar ----------

    public async Task<ResultadoAsignacionDto> AsignarAsync(AsignarVoluntariosDto dto, Guid asignadoPor)
    {
        var ids = (dto.VoluntarioIds ?? []).Distinct().ToList();
        if (ids.Count == 0) throw new ArgumentException("Selecciona al menos un voluntario");
        if (ids.Count > MaxPorAsignacion) throw new ArgumentException($"Puedes asignar hasta {MaxPorAsignacion} voluntarios a la vez");
        var tarea = string.IsNullOrWhiteSpace(dto.Tarea) ? null : dto.Tarea.Trim();
        if (tarea?.Length > MaxTarea) throw new ArgumentException($"La tarea no puede superar {MaxTarea} caracteres");

        var actividad = await ActividadDeAsync(dto.EventoId, dto.CursoId);
        if (!actividad.Asignable(DateTime.UtcNow))
            throw new InvalidOperationException("Solo se puede asignar voluntarios a actividades publicadas que aún no terminaron");

        var voluntarios = await _db.Voluntarios.Where(v => ids.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
        var vigentes = _db.AsignacionesVoluntario.Where(a => ids.Contains(a.VoluntarioId) && a.Estado != "CANCELADA");
        vigentes = actividad.Tipo == "EVENTO"
            ? vigentes.Where(a => a.EventoId == actividad.Id)
            : vigentes.Where(a => a.CursoId == actividad.Id);
        var yaAsignados = (await vigentes.Select(a => a.VoluntarioId).ToListAsync()).ToHashSet();

        var omitidas = new List<AsignacionOmitidaDto>();
        var nuevas = new List<AsignacionVoluntario>();
        foreach (var id in ids)
        {
            if (!voluntarios.TryGetValue(id, out var v)) { omitidas.Add(new(id, "Voluntario no encontrado")); continue; }
            if (v.Estado != "ACTIVO") { omitidas.Add(new(id, $"{v.NombreCompleto} no está activo/a")); continue; }
            if (yaAsignados.Contains(id)) { omitidas.Add(new(id, $"{v.NombreCompleto} ya está asignado/a a esta actividad")); continue; }
            nuevas.Add(new AsignacionVoluntario
            {
                VoluntarioId = id,
                EventoId = actividad.Tipo == "EVENTO" ? actividad.Id : null,
                CursoId = actividad.Tipo == "CURSO" ? actividad.Id : null,
                Tarea = tarea,
                AsignadoPor = asignadoPor
            });
        }

        if (nuevas.Count > 0)
        {
            _db.AsignacionesVoluntario.AddRange(nuevas);
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                // Otro coordinador asignó a alguno al mismo tiempo (índice único): no se guarda nada a medias
                throw new InvalidOperationException("Alguno de los voluntarios acaba de ser asignado a esta actividad. Vuelve a intentarlo.");
            }

            foreach (var a in nuevas) await NotificarAsignacionAsync(voluntarios[a.VoluntarioId], actividad, tarea);
        }

        var ahora = DateTime.UtcNow;
        return new ResultadoAsignacionDto(nuevas.Select(a => ADto(a, voluntarios[a.VoluntarioId], actividad, ahora)).ToList(), omitidas);
    }

    private async Task NotificarAsignacionAsync(Voluntario v, Actividad act, string? tarea)
    {
        var cuando = act.TodoElDia
            ? $"del {act.Inicio:dd/MM/yyyy} al {(act.Fin ?? act.Inicio):dd/MM/yyyy}"
            : $"el {ABolivia(act.Inicio):dd/MM/yyyy 'a las' HH:mm}";
        var tipo = act.Tipo == "EVENTO" ? "el evento" : "el curso";
        var mensaje = $"Hola {v.NombreCompleto}, fuiste asignado/a como voluntario/a en {tipo} \"{act.Titulo}\" {cuando}" +
                      (act.Lugar is null ? "" : $" en {act.Lugar}") +
                      (tarea is null ? "." : $". Tu tarea: {tarea}.") +
                      " Puedes ver el detalle en tu Portal del Voluntario. ¡Gracias por tu apoyo!";
        // La asignación ya está guardada: si un aviso falla se registra y se sigue con los demás
        try { await _email.EnviarAsync(v.Email, $"Nueva asignación de voluntariado: {act.Titulo}", mensaje); }
        catch (Exception ex) { _log.LogWarning(ex, "No se pudo enviar el correo de asignación a {Email}", v.Email); }
        if (!string.IsNullOrWhiteSpace(v.Telefono))
        {
            try { await _whatsApp.EnviarAsync(v.Telefono, mensaje); }
            catch (Exception ex) { _log.LogWarning(ex, "No se pudo enviar el WhatsApp de asignación a {Tel}", v.Telefono); }
        }
    }

    // Bolivia (UTC-4, sin horario de verano): las horas de los eventos se guardan en UTC
    private static DateTime ABolivia(DateTime utc) => utc.AddHours(-4);

    // ---------- Consultar ----------

    public async Task<List<AsignacionDto>> ListarAsync(Guid? eventoId, Guid? cursoId)
    {
        var q = _db.AsignacionesVoluntario.Where(a => a.Estado != "CANCELADA");
        if (eventoId is not null) q = q.Where(a => a.EventoId == eventoId);
        if (cursoId is not null) q = q.Where(a => a.CursoId == cursoId);
        return await ConstruirAsync(await q.ToListAsync());
    }

    public async Task<List<AsignacionDto>> MisAsignacionesAsync(Guid usuarioId, string? email)
    {
        var voluntario = await _db.Voluntarios
            .FirstOrDefaultAsync(v => v.UsuarioId == usuarioId || (email != null && v.Email == email));
        if (voluntario is null) return [];
        return await ConstruirAsync(await _db.AsignacionesVoluntario
            .Where(a => a.VoluntarioId == voluntario.Id && a.Estado != "CANCELADA").ToListAsync());
    }

    private async Task<List<AsignacionDto>> ConstruirAsync(List<AsignacionVoluntario> asignaciones)
    {
        var volIds = asignaciones.Select(a => a.VoluntarioId).Distinct().ToList();
        var evIds = asignaciones.Where(a => a.EventoId != null).Select(a => a.EventoId!.Value).Distinct().ToList();
        var cuIds = asignaciones.Where(a => a.CursoId != null).Select(a => a.CursoId!.Value).Distinct().ToList();

        var voluntarios = await _db.Voluntarios.Where(v => volIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
        var actividades = (await _db.Eventos.Where(e => evIds.Contains(e.Id)).ToListAsync()).Select(DeEvento)
            .Concat((await _db.Cursos.Where(c => cuIds.Contains(c.Id)).ToListAsync()).Select(DeCurso))
            .ToDictionary(a => a.Id);

        var ahora = DateTime.UtcNow;
        return asignaciones
            .Where(a => voluntarios.ContainsKey(a.VoluntarioId) && actividades.ContainsKey((a.EventoId ?? a.CursoId)!.Value))
            .Select(a => ADto(a, voluntarios[a.VoluntarioId], actividades[(a.EventoId ?? a.CursoId)!.Value], ahora))
            .OrderBy(a => a.Inicio).ThenBy(a => a.Voluntario)
            .ToList();
    }

    private static AsignacionDto ADto(AsignacionVoluntario a, Voluntario v, Actividad act, DateTime ahora) =>
        new(a.Id, v.Id, v.NombreCompleto, v.Email, act.Tipo, act.Id, act.Titulo, act.Lugar, act.Inicio, act.Fin,
            act.TodoElDia, act.Finalizada(ahora), a.Tarea, a.Estado, a.Horas, a.AsignadoEn);

    // ---------- Cancelar ----------

    public async Task CancelarAsync(Guid asignacionId)
    {
        var a = await _db.AsignacionesVoluntario.FindAsync(asignacionId)
                ?? throw new KeyNotFoundException("Asignación no encontrada");
        if (a.Estado != "ASIGNADA")
            throw new InvalidOperationException(a.Estado == "COMPLETADA"
                ? "La asignación ya tiene horas registradas: no se puede cancelar"
                : "La asignación ya estaba cancelada");

        var actividad = await ActividadDeAsync(a.EventoId, a.CursoId);
        a.Estado = "CANCELADA";
        await _db.SaveChangesAsync();

        var v = await _db.Voluntarios.FindAsync(a.VoluntarioId);
        if (v is null || actividad.Finalizada(DateTime.UtcNow)) return;
        try
        {
            await _email.EnviarAsync(v.Email, $"Asignación cancelada: {actividad.Titulo}",
                $"Hola {v.NombreCompleto}, tu asignación como voluntario/a en \"{actividad.Titulo}\" fue cancelada por el coordinador. " +
                "Gracias por tu disponibilidad.");
        }
        catch (Exception ex) { _log.LogWarning(ex, "No se pudo avisar la cancelación a {Email}", v.Email); }
    }

    // ---------- Horas ----------

    // La primera vez marca la asignación como COMPLETADA y crea la fila del historial; si ya tenía
    // horas, las corrige en ambos lugares (no se duplica: participaciones.asignacion_id es único).
    public async Task<AsignacionDto> RegistrarHorasAsync(Guid asignacionId, decimal horas, Guid registradoPor)
    {
        ValidarHoras(horas);
        var a = await _db.AsignacionesVoluntario.FindAsync(asignacionId)
                ?? throw new KeyNotFoundException("Asignación no encontrada");
        if (a.Estado == "CANCELADA")
            throw new InvalidOperationException("La asignación fue cancelada: no se pueden registrar horas");

        var actividad = await ActividadDeAsync(a.EventoId, a.CursoId);
        var ahora = DateTime.UtcNow;
        if (!actividad.Finalizada(ahora))
            throw new InvalidOperationException("Las horas se registran cuando la actividad terminó");

        var v = await _db.Voluntarios.FindAsync(a.VoluntarioId)
                ?? throw new KeyNotFoundException("Voluntario no encontrado");

        a.Horas = horas;
        a.Estado = "COMPLETADA";
        a.HorasRegistradasPor = registradoPor;
        a.HorasRegistradasEn = ahora;

        var p = await _db.ParticipacionesVoluntario.FirstOrDefaultAsync(x => x.AsignacionId == a.Id);
        if (p is null)
        {
            p = new ParticipacionVoluntario
            {
                VoluntarioId = a.VoluntarioId,
                EventoId = a.EventoId,
                CursoId = a.CursoId,
                AsignacionId = a.Id,
                // La fecha del historial es la de la actividad, no la del registro. Los cursos no tienen
                // hora: se usa el mediodía UTC para que en Bolivia (UTC-4) se vea el mismo día.
                Fecha = actividad.TodoElDia
                    ? DateTime.SpecifyKind((actividad.Fin ?? actividad.Inicio).AddHours(12), DateTimeKind.Utc)
                    : DateTime.SpecifyKind(actividad.Fin ?? actividad.Inicio, DateTimeKind.Utc)
            };
            _db.ParticipacionesVoluntario.Add(p);
        }
        p.Horas = horas;
        p.Descripcion = a.Tarea ?? $"Voluntariado en {actividad.Titulo}";

        await _db.SaveChangesAsync();
        return ADto(a, v, actividad, ahora);
    }
}
