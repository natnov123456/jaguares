using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FundacionJaguares.Application.Services;

// Configuración "App" (appsettings / user-secrets): dirección pública del frontend para armar enlaces
public class AppOpciones
{
    public string FrontendUrl { get; set; } = "http://localhost:5173";
}

// FUJ-120: encuesta de satisfacción post-actividad. Se crea al cerrar la asistencia de un evento
// o al finalizar un curso; cada asistente recibe un enlace personal para responder una sola vez.
// Encuesta ya guardada cuyos enlaces todavía no se enviaron
public record EncuestaPreparada(Guid? EncuestaId, string Actividad, int CantidadPreguntas,
                                List<(string Email, string Nombre, string Token)> Invitados);

public interface IEncuestaService
{
    // Guarda la encuesta y las invitaciones (sin enviar nada). Se llama dentro de la transacción
    // que cierra la actividad: si algo falla, la actividad no queda cerrada sin encuesta.
    Task<EncuestaPreparada> PrepararAsync(string tipo, Guid actividadId, string titulo,
        IReadOnlyCollection<Guid> asistentes, List<PreguntaEncuestaDto>? preguntas, Guid creadaPor);
    // Envía los enlaces. Se llama después del commit: cada enlace que llega ya existe.
    Task NotificarAsync(EncuestaPreparada preparada);
    Task<EncuestaPublicaDto> ObtenerPublicaAsync(string token);
    Task ResponderAsync(string token, ResponderEncuestaDto dto);
    Task<List<EncuestaPendienteDto>> PendientesAsync(Guid participanteId);
    Task<List<ResumenEncuestaDto>> ListarAsync(Guid usuarioId, string rol);
    Task<ResultadoEncuestaDto> ResultadosAsync(Guid encuestaId, Guid usuarioId, string rol);
}

public class EncuestaService : IEncuestaService
{
    public const int MinPreguntas = 3, MaxPreguntas = 5, MaxTextoPregunta = 200, MaxTextoRespuesta = 1000;

    public static readonly IReadOnlyList<PreguntaEncuestaDto> PreguntasPorDefecto =
    [
        new("En general, ¿qué tan satisfecho/a quedaste con la actividad?", "ESCALA"),
        new("¿Cómo calificarías la organización (horario, lugar y materiales)?", "ESCALA"),
        new("¿Qué tan probable es que recomiendes esta actividad a otra persona?", "ESCALA"),
        new("¿Qué podríamos mejorar para las próximas actividades?", "TEXTO"),
    ];

    private readonly AppDbContext _db;
    private readonly IEmailAdapter _email;
    private readonly AppOpciones _app;
    private readonly ILogger<EncuestaService> _log;

    public EncuestaService(AppDbContext db, IEmailAdapter email, AppOpciones app, ILogger<EncuestaService> log)
    { _db = db; _email = email; _app = app; _log = log; }

    // Valida (y normaliza) las preguntas antes de cerrar la actividad, para no cerrarla con una encuesta inválida
    public static List<PreguntaEncuestaDto> ValidarPreguntas(List<PreguntaEncuestaDto>? preguntas)
    {
        if (preguntas is null) return PreguntasPorDefecto.ToList();
        var limpias = preguntas.Select(p => new PreguntaEncuestaDto((p.Texto ?? "").Trim(), (p.Tipo ?? "").Trim().ToUpperInvariant())).ToList();
        if (limpias.Count is < MinPreguntas or > MaxPreguntas)
            throw new ArgumentException($"La encuesta debe tener entre {MinPreguntas} y {MaxPreguntas} preguntas");
        if (limpias.Any(p => p.Texto.Length == 0))
            throw new ArgumentException("Ninguna pregunta puede quedar vacía");
        if (limpias.Any(p => p.Texto.Length > MaxTextoPregunta))
            throw new ArgumentException($"Cada pregunta puede tener como máximo {MaxTextoPregunta} caracteres");
        if (limpias.Any(p => p.Tipo is not ("ESCALA" or "TEXTO")))
            throw new ArgumentException("El tipo de pregunta debe ser ESCALA o TEXTO");
        if (!limpias.Any(p => p.Tipo == "ESCALA"))
            throw new ArgumentException("Incluye al menos una pregunta de puntaje (1 a 5) para medir la satisfacción");
        if (limpias.Select(p => p.Texto.ToLowerInvariant()).Distinct().Count() != limpias.Count)
            throw new ArgumentException("Hay preguntas repetidas");
        return limpias;
    }

    // ---------- Crear y enviar ----------

    public async Task<EncuestaPreparada> PrepararAsync(string tipo, Guid actividadId, string titulo,
        IReadOnlyCollection<Guid> asistentes, List<PreguntaEncuestaDto>? preguntas, Guid creadaPor)
    {
        var lista = ValidarPreguntas(preguntas);
        if (asistentes.Count == 0) return new EncuestaPreparada(null, titulo, lista.Count, []);

        if (tipo is not ("CURSO" or "EVENTO")) throw new ArgumentException("Tipo de actividad no válido");
        var encuesta = tipo == "CURSO"
            ? await _db.Encuestas.FirstOrDefaultAsync(e => e.CursoId == actividadId)
            : await _db.Encuestas.FirstOrDefaultAsync(e => e.EventoId == actividadId);
        if (encuesta is null)
        {
            encuesta = new Encuesta
            {
                CursoId = tipo == "CURSO" ? actividadId : null,
                EventoId = tipo == "EVENTO" ? actividadId : null,
                CreadaPor = creadaPor
            };
            _db.Encuestas.Add(encuesta);
            _db.EncuestaPreguntas.AddRange(lista.Select((p, i) => new EncuestaPregunta
            {
                EncuestaId = encuesta.Id, Orden = (short)(i + 1), Texto = p.Texto, Tipo = p.Tipo
            }));
        }

        // Solo se invita a quien todavía no tiene enlace (cerrar dos veces no duplica correos)
        var yaInvitados = (await _db.EncuestaInvitaciones.Where(i => i.EncuestaId == encuesta.Id)
            .Select(i => i.ParticipanteId).ToListAsync()).ToHashSet();
        var usuarios = await _db.Usuarios.Where(u => asistentes.Contains(u.Id) && u.Activo).ToListAsync();
        var nuevas = usuarios.Where(u => !yaInvitados.Contains(u.Id))
            .Select(u => (Usuario: u, Invitacion: new EncuestaInvitacion
            {
                EncuestaId = encuesta.Id, ParticipanteId = u.Id, Token = AsistenciaService.NuevoCodigo()
            })).ToList();
        _db.EncuestaInvitaciones.AddRange(nuevas.Select(n => n.Invitacion));
        await _db.SaveChangesAsync();

        var cantidad = await _db.EncuestaPreguntas.CountAsync(p => p.EncuestaId == encuesta.Id);
        return new EncuestaPreparada(encuesta.Id, titulo, cantidad,
            nuevas.Select(n => (n.Usuario.Email, n.Usuario.NombreCompleto, n.Invitacion.Token)).ToList());
    }

    public async Task NotificarAsync(EncuestaPreparada preparada)
    {
        foreach (var (email, nombre, token) in preparada.Invitados)
        {
            var enlace = $"{_app.FrontendUrl.TrimEnd('/')}/encuesta/{token}";
            try
            {
                await _email.EnviarAsync(email, $"¿Cómo te fue en {preparada.Actividad}? Cuéntanos en 1 minuto",
                    $"Hola {nombre}, gracias por participar en \"{preparada.Actividad}\". " +
                    $"Queremos mejorar nuestras actividades: responde esta encuesta corta de {preparada.CantidadPreguntas} preguntas en {enlace} . " +
                    "El enlace es personal y se puede usar una sola vez.");
            }
            catch (Exception ex) { _log.LogWarning(ex, "No se pudo enviar la encuesta a {Email}", email); }
        }
    }

    // ---------- Responder (público, con el token) ----------

    private async Task<(EncuestaInvitacion Inv, Encuesta Enc)> InvitacionAsync(string token)
    {
        var t = (token ?? "").Trim();
        var fila = t.Length is 0 or > 40 ? null : await (
            from i in _db.EncuestaInvitaciones
            join e in _db.Encuestas on i.EncuestaId equals e.Id
            where i.Token == t
            select new { i, e }).FirstOrDefaultAsync();
        return fila is null ? throw new KeyNotFoundException("El enlace de la encuesta no es válido") : (fila.i, fila.e);
    }

    public async Task<EncuestaPublicaDto> ObtenerPublicaAsync(string token)
    {
        var (inv, enc) = await InvitacionAsync(token);
        var (tipo, titulo, fecha) = await ActividadAsync(enc);
        var nombre = await _db.Usuarios.Where(u => u.Id == inv.ParticipanteId).Select(u => u.NombreCompleto).FirstOrDefaultAsync() ?? "";
        var preguntas = await _db.EncuestaPreguntas.Where(p => p.EncuestaId == enc.Id).OrderBy(p => p.Orden)
            .Select(p => new PreguntaPublicaDto(p.Id, p.Orden, p.Texto, p.Tipo)).ToListAsync();
        // Solo el primer nombre: el enlace podría reenviarse y no debe exponer más datos
        return new EncuestaPublicaDto(titulo, tipo, fecha, nombre.Split(' ', 2)[0], inv.RespondidaEn != null, preguntas);
    }

    public async Task ResponderAsync(string token, ResponderEncuestaDto dto)
    {
        var (inv, enc) = await InvitacionAsync(token);
        if (inv.RespondidaEn != null) throw new InvalidOperationException("Esta encuesta ya fue respondida. ¡Gracias!");

        var preguntas = await _db.EncuestaPreguntas.Where(p => p.EncuestaId == enc.Id).ToDictionaryAsync(p => p.Id);
        var respuestas = (dto.Respuestas ?? []).ToList();
        if (respuestas.Select(r => r.PreguntaId).Distinct().Count() != respuestas.Count)
            throw new ArgumentException("Hay preguntas respondidas más de una vez");

        var filas = new List<EncuestaRespuesta>();
        foreach (var r in respuestas)
        {
            if (!preguntas.TryGetValue(r.PreguntaId, out var p))
                throw new ArgumentException("Una de las respuestas no corresponde a esta encuesta");
            if (p.Tipo == "ESCALA")
            {
                if (r.Valor is not (>= 1 and <= 5)) throw new ArgumentException($"Elige un puntaje de 1 a 5 en: {p.Texto}");
                filas.Add(new EncuestaRespuesta { InvitacionId = inv.Id, PreguntaId = p.Id, Valor = (short)r.Valor });
            }
            else
            {
                var texto = string.IsNullOrWhiteSpace(r.Texto) ? null : r.Texto.Trim();
                if (texto?.Length > MaxTextoRespuesta)
                    throw new ArgumentException($"El comentario puede tener como máximo {MaxTextoRespuesta} caracteres");
                if (texto is not null) filas.Add(new EncuestaRespuesta { InvitacionId = inv.Id, PreguntaId = p.Id, Texto = texto });
            }
        }
        // Las preguntas de puntaje son obligatorias; los comentarios, opcionales
        var faltante = preguntas.Values.Where(p => p.Tipo == "ESCALA").OrderBy(p => p.Orden)
            .FirstOrDefault(p => filas.All(f => f.PreguntaId != p.Id));
        if (faltante is not null) throw new ArgumentException($"Falta responder: {faltante.Texto}");

        await using var tx = await _db.Database.BeginTransactionAsync();
        // Actualización condicional: si se envía dos veces a la vez, solo una respuesta se guarda
        var marcadas = await _db.EncuestaInvitaciones
            .Where(i => i.Id == inv.Id && i.RespondidaEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.RespondidaEn, DateTime.UtcNow));
        if (marcadas == 0) throw new InvalidOperationException("Esta encuesta ya fue respondida. ¡Gracias!");
        _db.EncuestaRespuestas.AddRange(filas);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<List<EncuestaPendienteDto>> PendientesAsync(Guid participanteId)
    {
        var filas = await (from i in _db.EncuestaInvitaciones
                           join e in _db.Encuestas on i.EncuestaId equals e.Id
                           where i.ParticipanteId == participanteId && i.RespondidaEn == null
                           orderby i.EnviadaEn descending
                           select new { i, e }).ToListAsync();
        var lista = new List<EncuestaPendienteDto>();
        foreach (var f in filas)
        {
            var (tipo, titulo, _) = await ActividadAsync(f.e);
            lista.Add(new EncuestaPendienteDto(f.i.Token, titulo, tipo, f.i.EnviadaEn));
        }
        return lista;
    }

    // ---------- Resultados (administración) ----------

    // Directora: todas. Docente: las de sus cursos. Encargado de eventos: las de eventos.
    private IQueryable<Encuesta> Visibles(Guid usuarioId, string rol) => rol switch
    {
        "DIRECTORA" => _db.Encuestas,
        "DOCENTE" => _db.Encuestas.Where(e => e.CursoId != null && _db.Cursos.Any(c => c.Id == e.CursoId && c.DocenteId == usuarioId)),
        "ENCARGADO_EVENTOS" => _db.Encuestas.Where(e => e.EventoId != null),
        _ => _db.Encuestas.Where(_ => false)
    };

    public async Task<List<ResumenEncuestaDto>> ListarAsync(Guid usuarioId, string rol)
    {
        var encuestas = await Visibles(usuarioId, rol).ToListAsync();
        var lista = new List<ResumenEncuestaDto>();
        foreach (var e in encuestas) lista.Add(await ResumenAsync(e));
        return lista.OrderByDescending(r => r.FechaActividad).ToList();
    }

    public async Task<ResultadoEncuestaDto> ResultadosAsync(Guid encuestaId, Guid usuarioId, string rol)
    {
        var e = await Visibles(usuarioId, rol).FirstOrDefaultAsync(x => x.Id == encuestaId)
                ?? throw new KeyNotFoundException("Encuesta no encontrada");
        var resumen = await ResumenAsync(e);

        var preguntas = await _db.EncuestaPreguntas.Where(p => p.EncuestaId == e.Id).OrderBy(p => p.Orden).ToListAsync();
        var respuestas = await (from r in _db.EncuestaRespuestas
                                join i in _db.EncuestaInvitaciones on r.InvitacionId equals i.Id
                                where i.EncuestaId == e.Id
                                select new { r.PreguntaId, r.Valor, r.Texto, i.RespondidaEn }).ToListAsync();

        var resultado = preguntas.Select(p =>
        {
            var propias = respuestas.Where(r => r.PreguntaId == p.Id).ToList();
            var valores = propias.Where(r => r.Valor != null).Select(r => (int)r.Valor!.Value).ToList();
            var distribucion = Enumerable.Range(1, 5).Select(n => valores.Count(v => v == n)).ToArray();
            return new ResultadoPreguntaDto(p.Id, p.Orden, p.Texto, p.Tipo, propias.Count,
                valores.Count == 0 ? null : Math.Round((decimal)valores.Average(), 2), distribucion);
        }).ToList();

        // Los comentarios se muestran sin el nombre de quien respondió
        var textos = preguntas.ToDictionary(p => p.Id, p => p.Texto);
        var comentarios = respuestas.Where(r => r.Texto != null)
            .OrderByDescending(r => r.RespondidaEn)
            .Select(r => new ComentarioEncuestaDto(textos[r.PreguntaId], r.Texto!, r.RespondidaEn ?? DateTime.UtcNow))
            .ToList();

        return new ResultadoEncuestaDto(resumen, resultado, comentarios);
    }

    private async Task<ResumenEncuestaDto> ResumenAsync(Encuesta e)
    {
        var (tipo, titulo, fecha) = await ActividadAsync(e);
        var invitados = await _db.EncuestaInvitaciones.CountAsync(i => i.EncuestaId == e.Id);
        var respondidas = await _db.EncuestaInvitaciones.CountAsync(i => i.EncuestaId == e.Id && i.RespondidaEn != null);
        // Promedio de satisfacción: media de todas las respuestas de puntaje de la encuesta
        var promedio = await (from r in _db.EncuestaRespuestas
                              join i in _db.EncuestaInvitaciones on r.InvitacionId equals i.Id
                              where i.EncuestaId == e.Id && r.Valor != null
                              select (double?)r.Valor).AverageAsync();
        return new ResumenEncuestaDto(e.Id, tipo, e.CursoId ?? e.EventoId!.Value, titulo, fecha, e.CreadaEn,
            invitados, respondidas, promedio is null ? null : Math.Round((decimal)promedio.Value, 2));
    }

    private async Task<(string Tipo, string Titulo, DateTime Fecha)> ActividadAsync(Encuesta e)
    {
        if (e.CursoId is not null)
        {
            var c = await _db.Cursos.FindAsync(e.CursoId.Value);
            return ("CURSO", c?.Titulo ?? "(curso eliminado)",
                c is null ? e.CreadaEn : DateTime.SpecifyKind(c.FechaFin.ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Utc));
        }
        var ev = await _db.Eventos.FindAsync(e.EventoId!.Value);
        return ("EVENTO", ev?.Titulo ?? "(evento eliminado)", ev?.FechaInicio ?? e.CreadaEn);
    }
}
