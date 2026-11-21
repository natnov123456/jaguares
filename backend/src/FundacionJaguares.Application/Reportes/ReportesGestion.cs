using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;

namespace FundacionJaguares.Application.Reportes;

// Corrección de la evaluación: exportación en las pantallas de gestión de cursos, eventos, voluntariado y encuestas.
// Reutiliza los mismos servicios que alimentan cada pantalla, así el archivo coincide con lo que se ve.
public class ReportesGestion(ICursoService cursos, IEventoService eventos, IVoluntarioService voluntarios,
                             IVoluntariadoService voluntariado, IEncuestaService encuestas) : IProveedorReportes
{
    private static readonly string[] Coordinacion = ["DIRECTORA", "ENCARGADO_EVENTOS", "COORDINADOR_VOLUNTARIADO"];

    public IEnumerable<DefinicionReporte> Reportes() =>
    [
        new("cursos", "Cursos", ["DIRECTORA", "DOCENTE"], CursosAsync),
        new("eventos", "Eventos", ["DIRECTORA", "ENCARGADO_EVENTOS"], EventosAsync),
        new("voluntarios", "Voluntarios", Coordinacion, VoluntariosAsync),
        new("asignaciones-voluntariado", "Asignaciones de voluntariado", Coordinacion, AsignacionesAsync),
        new("encuestas", "Encuestas de satisfacción", ["DIRECTORA", "DOCENTE", "ENCARGADO_EVENTOS"], EncuestasAsync),
        new("encuesta-resultados", "Resultados de la encuesta", ["DIRECTORA", "DOCENTE", "ENCARGADO_EVENTOS"], ResultadosEncuestaAsync),
    ];

    private async Task<TablaReporte> CursosAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        // El docente solo ve (y exporta) los cursos que le asignaron
        var lista = await cursos.ListarGestionAsync(s.Rol == "DIRECTORA" ? null : s.UsuarioId);
        var filtrados = FiltroCombinado.Aplicar(lista, f,
            texto: c => [c.Titulo, c.DocenteNombre], estado: c => c.Estado,
            fecha: c => c.FechaInicio.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc)).ToList();
        return new TablaReporte("Cursos", s.Rol == "DIRECTORA" ? "Todos los cursos de la fundación" : "Cursos asignados",
            [new("Curso", 2.6f), new("Docente", 1.8f), new("Inicio", 1f), new("Fin", 1f), new("Inscritos", 0.9f, true),
             new("Cupo", 0.7f, true), new("En espera", 0.9f, true), new("Estado", 1f)],
            filtrados.Select(c => new[]
            {
                c.Titulo, c.DocenteNombre ?? "Sin asignar", Fmt.Fecha(c.FechaInicio), Fmt.Fecha(c.FechaFin),
                Fmt.Entero(c.Inscritos), Fmt.Entero(c.CupoMaximo), Fmt.Entero(c.EnEspera), Fmt.Estado(c.Estado)
            }).ToList(),
            FiltroCombinado.Describir(f, null, Fmt.Estado),
            [new("Cursos", Fmt.Entero(filtrados.Count)), new("Inscritos", Fmt.Entero(filtrados.Sum(c => c.Inscritos)))]);
    }

    private async Task<TablaReporte> EventosAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        var lista = await eventos.ListarGestionAsync();
        var filtrados = FiltroCombinado.Aplicar(lista, f,
            texto: e => [e.Titulo, e.Lugar, e.Descripcion], estado: e => e.Estado, fecha: e => e.FechaInicio)
            .OrderBy(e => e.FechaInicio).ToList();
        return new TablaReporte("Eventos", "Eventos y actividades de la fundación",
            [new("Evento", 2.6f), new("Inicio", 1.3f), new("Fin", 1.3f), new("Lugar", 1.8f), new("Estado", 1f)],
            filtrados.Select(e => new[]
            {
                e.Titulo, Fmt.FechaHora(e.FechaInicio), Fmt.FechaHora(e.FechaFin), e.Lugar ?? "—", Fmt.Estado(e.Estado)
            }).ToList(),
            FiltroCombinado.Describir(f, null, Fmt.Estado),
            [new("Eventos", Fmt.Entero(filtrados.Count))]);
    }

    private async Task<TablaReporte> VoluntariosAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        var lista = await voluntarios.ListarConHistorialAsync();
        var filtrados = FiltroCombinado.Aplicar(lista, f,
            texto: v => [v.NombreCompleto, v.Email, v.Telefono], estado: v => v.Estado, fecha: v => v.PostuladoEn)
            .OrderBy(v => v.NombreCompleto).ToList();
        return new TablaReporte("Voluntarios", "Registro único de voluntarios con su historial",
            [new("Voluntario", 2f), new("Correo", 2.2f), new("Teléfono", 1.1f), new("Estado", 0.9f), new("Postuló el", 1f),
             new("Actividades", 1f, true), new("Horas", 0.8f, true)],
            filtrados.Select(v => new[]
            {
                v.NombreCompleto, v.Email, v.Telefono ?? "—", Fmt.Estado(v.Estado), Fmt.Fecha(v.PostuladoEn),
                Fmt.Entero(v.Historial.Count), Fmt.Horas(v.TotalHoras)
            }).ToList(),
            FiltroCombinado.Describir(f, null, Fmt.Estado),
            [new("Voluntarios", Fmt.Entero(filtrados.Count)), new("Activos", Fmt.Entero(filtrados.Count(v => v.Estado == "ACTIVO"))),
             new("Horas aportadas", Fmt.Horas(filtrados.Sum(v => v.TotalHoras)))]);
    }

    private async Task<TablaReporte> AsignacionesAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        var lista = await voluntariado.ListarAsync(null, null);
        var filtrados = FiltroCombinado.Aplicar(lista, f,
            texto: a => [a.Voluntario, a.Email, a.Actividad, a.Tarea, a.Lugar], estado: a => a.Estado, fecha: a => a.Inicio)
            .Where(a => f.ActividadId is null || a.ActividadId == f.ActividadId)
            .OrderBy(a => a.Inicio).ThenBy(a => a.Voluntario).ToList();
        return new TablaReporte("Asignaciones de voluntariado", "Voluntarios asignados a cursos y eventos, con sus horas",
            [new("Actividad", 2.4f), new("Tipo", 0.8f), new("Fecha", 1.2f), new("Voluntario", 2f), new("Tarea", 1.8f),
             new("Estado", 1f), new("Horario", 1.4f), new("Horas", 0.9f, true)],
            filtrados.Select(a => new[]
            {
                a.Actividad, a.Tipo == "CURSO" ? "Curso" : "Evento", a.TodoElDia ? Fmt.Fecha(a.Inicio) : Fmt.FechaHora(a.Inicio),
                a.Voluntario, a.Tarea ?? "—", Fmt.Estado(a.Estado), Fmt.Horario(a.HoraInicio, a.HoraFin, a.Dias),
                a.Horas is null ? "—" : Fmt.Horas(a.Horas.Value)
            }).ToList(),
            FiltroCombinado.Describir(f, null, Fmt.Estado),
            [new("Asignaciones", Fmt.Entero(filtrados.Count)), new("Horas registradas", Fmt.Horas(filtrados.Sum(a => a.Horas ?? 0)))]);
    }

    private async Task<TablaReporte> EncuestasAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        // El servicio ya limita lo visible por rol (docente: sus cursos; encargado: eventos)
        var lista = await encuestas.ListarAsync(s.UsuarioId, s.Rol);
        var filtrados = FiltroCombinado.Aplicar(lista, f,
            texto: e => [e.Actividad], estado: e => e.TipoActividad, fecha: e => e.FechaActividad).ToList();
        var conDatos = filtrados.Where(e => e.PromedioSatisfaccion is not null).ToList();
        return new TablaReporte("Encuestas de satisfacción", "Promedio de satisfacción por actividad (escala de 1 a 5)",
            [new("Actividad", 2.8f), new("Tipo", 0.8f), new("Fecha", 1f), new("Invitados", 0.9f, true),
             new("Respuestas", 0.9f, true), new("Satisfacción", 1f, true)],
            filtrados.Select(e => new[]
            {
                e.Actividad, e.TipoActividad == "CURSO" ? "Curso" : "Evento", Fmt.Fecha(e.FechaActividad),
                Fmt.Entero(e.Invitados), Fmt.Entero(e.Respuestas),
                e.PromedioSatisfaccion is null ? "—" : Fmt.Num(e.PromedioSatisfaccion.Value) + " / 5"
            }).ToList(),
            FiltroCombinado.Describir(f, null, t => t == "CURSO" ? "Cursos" : "Eventos"),
            [new("Satisfacción promedio", conDatos.Count == 0 ? "—" : Fmt.Num(Math.Round(conDatos.Average(e => e.PromedioSatisfaccion!.Value), 2)) + " / 5")]);
    }

    private async Task<TablaReporte> ResultadosEncuestaAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        if (f.ActividadId is null) throw new ArgumentException("Selecciona la encuesta que quieres exportar");
        ResultadoEncuestaDto r;
        try { r = await encuestas.ResultadosAsync(f.ActividadId.Value, s.UsuarioId, s.Rol); }
        catch (KeyNotFoundException) { throw new UnauthorizedAccessException("No puedes ver los resultados de esta encuesta"); }

        var filas = r.Preguntas.Where(p => p.Tipo == "ESCALA").Select(p => new[]
            {
                "Pregunta", p.Texto, p.Promedio is null ? "—" : Fmt.Num(p.Promedio.Value) + " / 5", Fmt.Entero(p.Respuestas), ""
            })
            .Concat(r.Comentarios.Select(c => new[] { "Comentario", c.Texto, "", "", Fmt.Fecha(c.RespondidoEn) }));
        var filtradas = filas.Where(x => FiltroCombinado.CoincideTexto(f.Q, x[1])).ToList();
        return new TablaReporte($"Resultados de la encuesta: {r.Resumen.Actividad}",
            $"{(r.Resumen.TipoActividad == "CURSO" ? "Curso" : "Evento")} del {Fmt.Fecha(r.Resumen.FechaActividad)} · " +
            $"{r.Resumen.Respuestas} respuesta(s) de {r.Resumen.Invitados} invitado(s)",
            [new("Tipo", 0.9f), new("Texto", 4.5f), new("Promedio", 0.9f, true), new("Respuestas", 0.9f, true), new("Fecha", 0.9f)],
            filtradas,
            FiltroCombinado.Describir(f with { ActividadId = null }),
            [new("Satisfacción promedio", r.Resumen.PromedioSatisfaccion is null ? "—" : Fmt.Num(r.Resumen.PromedioSatisfaccion.Value) + " / 5")]);
    }
}
