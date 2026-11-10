using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

// FUJ-112: "Mi panel". Reúne en una sola respuesta los cursos y eventos del participante
// (próximos y pasados), sus certificados y su lista de espera.
public interface IParticipanteService
{
    Task<PanelParticipanteDto> PanelAsync(Guid participanteId);
}

public class ParticipanteService : IParticipanteService
{
    private static readonly string[] EstadosAbiertos = ["PUBLICADO", "EN_CURSO"];

    private readonly AppDbContext _db;
    private readonly ICertificadoService _certificados;
    private readonly ICursoService _cursos;

    public ParticipanteService(AppDbContext db, ICertificadoService certificados, ICursoService cursos)
    { _db = db; _certificados = certificados; _cursos = cursos; }

    public async Task<PanelParticipanteDto> PanelAsync(Guid participanteId)
    {
        var ahora = DateTime.UtcNow;
        var hoy = DateOnly.FromDateTime(ahora);

        var certificados = await _certificados.ListarPorParticipanteAsync(participanteId);
        var listaEspera = await _cursos.MiListaEsperaAsync(participanteId);

        // Certificado válido por curso: se muestra junto al curso pasado para descargarlo desde ahí
        var certPorCurso = (await _db.Certificados
                .Where(c => c.ParticipanteId == participanteId && c.Valido && c.CursoId != null)
                .Select(c => new { CursoId = c.CursoId!.Value, c.CodigoUnico })
                .ToListAsync())
            .GroupBy(c => c.CursoId).ToDictionary(g => g.Key, g => g.First().CodigoUnico);

        // ---- Cursos ----
        var cursos = await (from i in _db.Inscripciones
                            join c in _db.Cursos on i.CursoId equals c.Id
                            where i.ParticipanteId == participanteId && i.Estado != "CANCELADA"
                            select new { i, c }).ToListAsync();

        var actividades = cursos.Select(x =>
        {
            var abierto = EstadosAbiertos.Contains(x.c.Estado) && x.c.FechaFin >= hoy;
            certPorCurso.TryGetValue(x.c.Id, out var codigo);
            return new ActividadPanelDto(x.c.Id, "CURSO", x.c.Titulo, null,
                x.c.FechaInicio.ToDateTime(TimeOnly.MinValue), x.c.FechaFin.ToDateTime(TimeOnly.MinValue), true,
                x.c.Estado, x.i.Estado,
                PuedeCancelar: abierto && x.i.Estado == "CONFIRMADA",
                InscripcionEventoId: null, CodigoCertificado: codigo);
        }).ToList();

        // ---- Eventos ----
        var eventos = await (from i in _db.InscripcionesEvento
                             join e in _db.Eventos on i.EventoId equals e.Id
                             where i.ParticipanteId == participanteId && i.Estado != "CANCELADA"
                             select new { i, e }).ToListAsync();
        // La asistencia también puede registrarse a mano (FUJ-102), sin pasar por el QR
        var asistio = (await _db.AsistenciasEvento
                .Where(a => a.ParticipanteId == participanteId)
                .Select(a => a.EventoId).ToListAsync())
            .ToHashSet();

        actividades.AddRange(eventos.Select(x =>
        {
            var terminado = (x.e.FechaFin ?? x.e.FechaInicio) < ahora || x.e.Estado is "FINALIZADO" or "CANCELADO";
            var estado = x.i.Estado == "ASISTIO" || asistio.Contains(x.e.Id) ? "ASISTIO"
                       : terminado ? "NO_ASISTIO" : x.i.Estado;
            return new ActividadPanelDto(x.e.Id, "EVENTO", x.e.Titulo, x.e.Lugar, x.e.FechaInicio, x.e.FechaFin, false,
                x.e.Estado, estado,
                PuedeCancelar: estado == "CONFIRMADA" && x.e.FechaInicio > ahora && EstadosAbiertos.Contains(x.e.Estado),
                InscripcionEventoId: x.i.Id, CodigoCertificado: null);
        }));

        bool EsProxima(ActividadPanelDto a) => a.Tipo == "CURSO"
            ? EstadosAbiertos.Contains(a.EstadoActividad) && DateOnly.FromDateTime(a.Fin ?? a.Inicio) >= hoy
            : a.EstadoInscripcion == "CONFIRMADA" && (a.Fin ?? a.Inicio) >= ahora;

        // Próximas: la más cercana primero. Pasadas: la más reciente primero.
        var proximas = actividades.Where(EsProxima).OrderBy(a => a.Inicio).ToList();
        var pasadas = actividades.Where(a => !EsProxima(a)).OrderByDescending(a => a.Fin ?? a.Inicio).ToList();

        var resumen = new ResumenPanelDto(
            CursosActivos: proximas.Count(a => a.Tipo == "CURSO"),
            EventosProximos: proximas.Count(a => a.Tipo == "EVENTO"),
            ActividadesCompletadas: pasadas.Count(a => a.EstadoInscripcion == "ASISTIO" || a.CodigoCertificado != null),
            Certificados: certificados.Count(c => c.Valido),
            EnListaEspera: listaEspera.Count);

        return new PanelParticipanteDto(resumen, proximas, pasadas, certificados, listaEspera);
    }
}
