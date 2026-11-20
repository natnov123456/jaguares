using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Reportes;

// JAG-86 / FUJ-124: inscritos y asistencia por curso, asistencia por evento y horas de voluntariado por rango de fechas
public class ReportesActividades(AppDbContext db) : IProveedorReportes
{
    public const string InscritosCurso = "inscritos-curso", AsistenciaEvento = "asistencia-evento",
                        HorasVoluntariado = "horas-voluntariado", Exportaciones = "exportaciones";

    public IEnumerable<DefinicionReporte> Reportes() =>
    [
        new(InscritosCurso, "Inscritos y asistencia del curso", ["DIRECTORA", "DOCENTE", "ENCARGADO_EVENTOS"], InscritosCursoAsync),
        new(AsistenciaEvento, "Inscritos y asistencia del evento", ["DIRECTORA", "ENCARGADO_EVENTOS"], AsistenciaEventoAsync),
        new(HorasVoluntariado, "Horas de voluntariado", ["DIRECTORA", "ENCARGADO_EVENTOS", "COORDINADOR_VOLUNTARIADO"], HorasVoluntariadoAsync),
        new(Exportaciones, "Registro de exportaciones", ["DIRECTORA"], ExportacionesAsync),
    ];

    private async Task<TablaReporte> InscritosCursoAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        if (f.ActividadId is null) throw new ArgumentException("Selecciona el curso que quieres exportar");
        var curso = await db.Cursos.FindAsync(f.ActividadId.Value) ?? throw new KeyNotFoundException("Curso no encontrado");
        // El docente solo exporta los cursos que le asignaron
        if (s.Rol == "DOCENTE" && curso.DocenteId != s.UsuarioId)
            throw new UnauthorizedAccessException("Solo puedes exportar los cursos que te asignaron");

        var docente = curso.DocenteId is null ? null
            : await db.Usuarios.Where(u => u.Id == curso.DocenteId).Select(u => u.NombreCompleto).FirstOrDefaultAsync();
        var inscritos = await (from i in db.Inscripciones
                               join u in db.Usuarios on i.ParticipanteId equals u.Id
                               where i.CursoId == curso.Id
                               orderby u.NombreCompleto
                               select new { u.Id, u.NombreCompleto, u.Email, i.Estado, i.FechaInscripcion }).ToListAsync();
        var dias = await db.AsistenciasCurso.Where(a => a.CursoId == curso.Id).Select(a => a.Fecha).Distinct().CountAsync();
        var presentes = await db.AsistenciasCurso.Where(a => a.CursoId == curso.Id && a.Presente)
            .GroupBy(a => a.ParticipanteId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);

        double? Pct(Guid id) => dias == 0 ? null : Math.Round(presentes.GetValueOrDefault(id) * 100.0 / dias, 1);
        var filtrados = FiltroCombinado.Aplicar(inscritos, f,
            texto: x => [x.NombreCompleto, x.Email], estado: x => x.Estado, fecha: x => x.FechaInscripcion).ToList();

        var confirmados = filtrados.Where(x => x.Estado == "CONFIRMADA").ToList();
        var promedio = dias == 0 || confirmados.Count == 0 ? (double?)null : Math.Round(confirmados.Average(x => Pct(x.Id) ?? 0), 1);

        return new TablaReporte(
            $"Inscritos y asistencia: {curso.Titulo}",
            $"Curso del {Fmt.Fecha(curso.FechaInicio)} al {Fmt.Fecha(curso.FechaFin)} · Docente: {docente ?? "sin asignar"} · " +
            $"Asistencia mínima para certificar: {curso.AsistenciaMinimaPct} % · {dias} clase(s) registrada(s)",
            [new("Participante", 2.2f), new("Correo", 2.4f), new("Estado", 1.1f), new("Inscrito el", 1.1f),
             new("Clases presente", 1.1f, true), new("Asistencia", 1f, true)],
            filtrados.Select(x => new[]
            {
                x.NombreCompleto, x.Email, Fmt.Estado(x.Estado), Fmt.Fecha(x.FechaInscripcion),
                dias == 0 ? "—" : $"{presentes.GetValueOrDefault(x.Id)} de {dias}", Fmt.Pct(Pct(x.Id))
            }).ToList(),
            FiltroCombinado.Describir(f, curso.Titulo, Fmt.Estado),
            [new("Inscritos confirmados", Fmt.Entero(confirmados.Count)), new("Asistencia promedio", Fmt.Pct(promedio)),
             new("Alcanzan el mínimo", Fmt.Entero(confirmados.Count(x => (Pct(x.Id) ?? -1) >= curso.AsistenciaMinimaPct)))]);
    }

    private async Task<TablaReporte> AsistenciaEventoAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        if (f.ActividadId is null) throw new ArgumentException("Selecciona el evento que quieres exportar");
        var ev = await db.Eventos.FindAsync(f.ActividadId.Value) ?? throw new KeyNotFoundException("Evento no encontrado");

        var inscritos = await (from i in db.InscripcionesEvento
                               join u in db.Usuarios on i.ParticipanteId equals u.Id
                               where i.EventoId == ev.Id
                               orderby u.NombreCompleto
                               select new { u.Id, u.NombreCompleto, u.Email, i.Estado, i.InscritoEn, i.CheckinEn }).ToListAsync();
        // Asistencia manual (FUJ-102) o por QR (FUJ-113)
        var manuales = await db.AsistenciasEvento.Where(a => a.EventoId == ev.Id)
            .ToDictionaryAsync(a => a.ParticipanteId, a => a.RegistradoEn);

        var filas = inscritos.Select(x =>
        {
            var hora = x.CheckinEn ?? (manuales.TryGetValue(x.Id, out var m) ? m : (DateTime?)null);
            return new { x.NombreCompleto, x.Email, x.Estado, x.InscritoEn, Asistio = hora is not null, Hora = hora };
        });
        var filtrados = FiltroCombinado.Aplicar(filas, f,
            texto: x => [x.NombreCompleto, x.Email], estado: x => x.Estado, fecha: x => x.InscritoEn).ToList();
        var validos = filtrados.Where(x => x.Estado != "CANCELADA").ToList();
        var asistentes = validos.Count(x => x.Asistio);

        return new TablaReporte(
            $"Inscritos y asistencia: {ev.Titulo}",
            $"Evento del {Fmt.FechaHora(ev.FechaInicio)}{(ev.Lugar is null ? "" : " · " + ev.Lugar)} · Estado: {Fmt.Estado(ev.Estado)}",
            [new("Participante", 2.2f), new("Correo", 2.4f), new("Inscripción", 1.1f), new("Inscrito el", 1.1f),
             new("Asistencia", 1f), new("Hora de ingreso", 1.3f)],
            filtrados.Select(x => new[]
            {
                x.NombreCompleto, x.Email, Fmt.Estado(x.Estado), Fmt.Fecha(x.InscritoEn),
                x.Estado == "CANCELADA" ? "—" : x.Asistio ? "Asistió" : "No asistió", Fmt.FechaHora(x.Hora)
            }).ToList(),
            FiltroCombinado.Describir(f, ev.Titulo, Fmt.Estado),
            [new("Inscritos (sin cancelados)", Fmt.Entero(validos.Count)), new("Asistieron", Fmt.Entero(asistentes)),
             new("Porcentaje de asistencia", Fmt.Pct(validos.Count == 0 ? null : Math.Round(asistentes * 100.0 / validos.Count, 1)))]);
    }

    private async Task<TablaReporte> HorasVoluntariadoAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        var participaciones = await (from p in db.ParticipacionesVoluntario
                                     join v in db.Voluntarios on p.VoluntarioId equals v.Id
                                     join e in db.Eventos on p.EventoId equals (Guid?)e.Id into ev
                                     from e in ev.DefaultIfEmpty()
                                     join c in db.Cursos on p.CursoId equals (Guid?)c.Id into cu
                                     from c in cu.DefaultIfEmpty()
                                     select new
                                     {
                                         v.Id, v.NombreCompleto, v.Email, v.Estado, p.Fecha, p.Horas,
                                         Actividad = e != null ? e.Titulo : c != null ? c.Titulo : p.Descripcion
                                     }).ToListAsync();
        var enRango = participaciones.Where(p => FiltroCombinado.EnRango(p.Fecha, f.Desde, f.Hasta));
        var porVoluntario = enRango.GroupBy(p => new { p.Id, p.NombreCompleto, p.Email, p.Estado })
            .Select(g => new
            {
                g.Key.NombreCompleto, g.Key.Email, g.Key.Estado,
                Actividades = g.OrderBy(x => x.Fecha).Select(x => x.Actividad ?? "Actividad").Distinct().ToList(),
                Horas = g.Sum(x => x.Horas ?? 0)
            });
        var filtrados = FiltroCombinado.Aplicar(porVoluntario, f,
                texto: x => [x.NombreCompleto, x.Email, string.Join(" ", x.Actividades)], estado: x => x.Estado)
            .OrderByDescending(x => x.Horas).ThenBy(x => x.NombreCompleto).ToList();

        return new TablaReporte(
            "Horas de voluntariado",
            "Actividades en las que participó cada voluntario y horas acumuladas en el período",
            [new("Voluntario", 2f), new("Correo", 2.2f), new("Estado", 0.9f), new("N° de actividades", 1f, true),
             new("Actividades", 4f), new("Horas", 1f, true)],
            filtrados.Select(x => new[]
            {
                x.NombreCompleto, x.Email, Fmt.Estado(x.Estado), Fmt.Entero(x.Actividades.Count),
                string.Join("; ", x.Actividades), Fmt.Horas(x.Horas)
            }).ToList(),
            FiltroCombinado.Describir(f, null, Fmt.Estado),
            [new("Voluntarios", Fmt.Entero(filtrados.Count)), new("Horas totales", Fmt.Horas(filtrados.Sum(x => x.Horas)))]);
    }

    private async Task<TablaReporte> ExportacionesAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        var filas = await (from e in db.Exportaciones
                           join u in db.Usuarios on e.UsuarioId equals u.Id
                           orderby e.CreadoEn descending
                           select new { e, u.NombreCompleto }).Take(2000).ToListAsync();
        var filtrados = FiltroCombinado.Aplicar(filas, f,
                texto: x => [x.e.Reporte, x.NombreCompleto, x.e.Rol], estado: x => x.e.Exito ? "EXITO" : "ERROR", fecha: x => x.e.CreadoEn)
            .Where(x => string.IsNullOrWhiteSpace(f.Accion) || string.Equals(x.e.Formato, f.Accion, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return new TablaReporte("Registro de exportaciones", "Reportes PDF y Excel generados en la plataforma",
            [new("Fecha", 1.3f), new("Usuario", 1.8f), new("Rol", 1.3f), new("Reporte", 1.6f), new("Formato", 0.7f),
             new("Filtros", 3f), new("Filas", 0.6f, true), new("Resultado", 1.4f)],
            filtrados.Select(x => new[]
            {
                Fmt.FechaHora(x.e.CreadoEn), x.NombreCompleto, x.e.Rol, x.e.Reporte, x.e.Formato, x.e.Filtros,
                Fmt.Entero(x.e.Filas), x.e.Exito ? "Generado" : "Error: " + x.e.Error
            }).ToList(),
            FiltroCombinado.Describir(f, null, e => e == "EXITO" ? "Generados" : "Con error"));
    }
}
