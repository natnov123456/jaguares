using System.Net;
using System.Text.Json;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// Asistencia a cursos: el docente pasa lista cada clase y cada participante ve su porcentaje.
// (En los eventos la asistencia se registra escaneando el QR: ver EventosTests.)
[Collection(ColeccionApi.Nombre)]
public class AsistenciaCursosTests(ApiFixture api)
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    // Curso ya empezado (desde hace 3 días) asignado al docente del seed, o a otro docente
    private async Task<JsonElement> CursoEnMarchaAsync(string titulo, int minimo = 50, bool deOtroDocente = false)
    {
        var docenteId = deOtroDocente ? await api.OtroDocenteIdAsync() : await api.DocenteIdAsync();
        return await (await (await api.DirectoraAsync()).PostJsonAsync("/api/Cursos", new
        {
            titulo, descripcion = (string?)null, fechaInicio = Hoy.AddDays(-3), fechaFin = Hoy.AddDays(5),
            cupoMaximo = 10, asistenciaMinimaPct = minimo, docenteId
        })).JsonAsync();
    }

    private static object Lista(DateOnly fecha, params (Guid Id, bool Presente)[] registros) => new
    {
        fecha, registros = registros.Select(r => new { participanteId = r.Id, presente = r.Presente }).ToArray()
    };

    private static JsonElement Fila(JsonElement dia, Guid participanteId) =>
        dia.GetProperty("participantes").EnumerateArray().Single(p => p.Id("participanteId") == participanteId);

    [Fact]
    public async Task Docente_pasa_lista_y_cada_participante_ve_su_porcentaje()
    {
        var titulo = "Curso con lista " + Guid.NewGuid().ToString("N")[..5];
        var curso = await CursoEnMarchaAsync(titulo, minimo: 50);
        var id = curso.Id();
        var docente = await api.DocenteAsync();
        var (ana, anaId, emailAna) = await api.NuevoParticipanteAsync("Ana Lista");
        var (beto, betoId, _) = await api.NuevoParticipanteAsync("Beto Lista");
        await (await ana.PostAsync($"/api/Cursos/{id}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);
        await (await beto.PostAsync($"/api/Cursos/{id}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);
        var url = $"/api/Cursos/{id}/asistencia";

        // Antes de la primera clase no hay porcentaje
        var vacio = await (await docente.GetAsync($"{url}?fecha={Hoy.AddDays(-2):yyyy-MM-dd}")).JsonAsync();
        Assert.Equal(JsonValueKind.Null, Fila(vacio, anaId).GetProperty("porcentaje").ValueKind);

        // Validaciones: día futuro, antes del inicio, alguien no inscrito, lista vacía
        await (await docente.PostJsonAsync(url, Lista(Hoy.AddDays(1), (anaId, true)))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await docente.PostJsonAsync(url, Lista(Hoy.AddDays(-4), (anaId, true)))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await docente.PostJsonAsync(url, Lista(Hoy, (Guid.NewGuid(), true)))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await docente.PostJsonAsync(url, Lista(Hoy))).EsperarAsync(HttpStatusCode.BadRequest);

        // Tres clases: Ana P, P, A (66.7 %) — Beto A, P, A (33.3 %)
        await (await docente.PostJsonAsync(url, Lista(Hoy.AddDays(-2), (anaId, true), (betoId, false)))).EsperarAsync(HttpStatusCode.OK);
        await (await docente.PostJsonAsync(url, Lista(Hoy.AddDays(-1), (anaId, true), (betoId, true)))).EsperarAsync(HttpStatusCode.OK);
        var dia3 = await (await docente.PostJsonAsync(url, Lista(Hoy, (anaId, false), (betoId, false)))).JsonAsync();
        Assert.Equal(66.7, Fila(dia3, anaId).GetProperty("porcentaje").GetDouble());
        Assert.Equal(33.3, Fila(dia3, betoId).GetProperty("porcentaje").GetDouble());
        Assert.Equal(3, Fila(dia3, anaId).GetProperty("diasRegistrados").GetInt32());

        // La primera clase marca el inicio del curso
        var detalle = await (await docente.GetAsync($"/api/Cursos/{id}/gestion")).JsonAsync();
        Assert.Equal("EN_CURSO", detalle.Str("estado"));
        Assert.Equal(3, detalle.GetProperty("diasConAsistencia").GetArrayLength());

        // Corregir un día solo avisa a quien le cambió el registro
        var avisosAntes = api.Correos.Para(emailAna).Count(c => c.Asunto == $"Asistencia registrada: {titulo}");
        Assert.Equal(3, avisosAntes);
        await (await docente.PostJsonAsync(url, Lista(Hoy, (anaId, true), (betoId, false)))).EsperarAsync(HttpStatusCode.OK);
        await (await docente.PostJsonAsync(url, Lista(Hoy, (anaId, false), (betoId, false)))).EsperarAsync(HttpStatusCode.OK);
        Assert.Equal(5, api.Correos.Para(emailAna).Count(c => c.Asunto == $"Asistencia registrada: {titulo}"));

        // El participante ve su detalle por día y su porcentaje en "Mi panel"
        var mia = await (await ana.GetAsync($"/api/Cursos/{id}/mi-asistencia")).JsonAsync();
        Assert.Equal(66.7, mia.GetProperty("porcentaje").GetDouble());
        Assert.Equal(2, mia.GetProperty("diasPresente").GetInt32());
        Assert.Equal(3, mia.GetProperty("dias").GetArrayLength());
        var panel = await (await ana.GetAsync("/api/Participante/panel")).JsonAsync();
        var enPanel = panel.GetProperty("proximas").EnumerateArray().Concat(panel.GetProperty("pasadas").EnumerateArray())
            .Single(a => a.Id("actividadId") == id);
        Assert.Equal(66.7, enPanel.GetProperty("asistenciaPct").GetDouble());
        var (caro, _, _) = await api.NuevoParticipanteAsync();
        await (await caro.GetAsync($"/api/Cursos/{id}/mi-asistencia")).EsperarAsync(HttpStatusCode.NotFound);

        // No se puede reprogramar dejando fuera clases que ya tienen asistencia
        await (await docente.PutJsonAsync($"/api/Cursos/{id}/reprogramar",
            new { fechaInicio = Hoy.AddDays(1), fechaFin = Hoy.AddDays(6), motivo = (string?)null })).EsperarAsync(HttpStatusCode.BadRequest);
        // Pero sí extender la fecha de fin conservando el inicio
        await (await docente.PutJsonAsync($"/api/Cursos/{id}/reprogramar",
            new { fechaInicio = Hoy.AddDays(-3), fechaFin = Hoy.AddDays(8), motivo = (string?)null })).EsperarAsync(HttpStatusCode.OK);

        // Al finalizar, solo se certifica a quien alcanza el mínimo (50 %): Ana sí, Beto no
        var fin = await (await docente.PostAsync($"/api/Cursos/{id}/finalizar", null)).JsonAsync();
        Assert.Equal(1, fin.GetProperty("certificados_generados").GetInt32());
        await (await docente.PostJsonAsync(url, Lista(Hoy, (anaId, true)))).EsperarAsync(HttpStatusCode.BadRequest); // ya cerrado
    }

    [Fact]
    public async Task Solo_el_docente_asignado_o_la_directora_gestionan_el_curso()
    {
        var curso = await CursoEnMarchaAsync("Curso ajeno " + Guid.NewGuid().ToString("N")[..5], deOtroDocente: true);
        var id = curso.Id();
        var docente = await api.DocenteAsync();
        var (ana, anaId, _) = await api.NuevoParticipanteAsync();
        await (await ana.PostAsync($"/api/Cursos/{id}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);

        await (await docente.GetAsync($"/api/Cursos/{id}/gestion")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await docente.GetAsync($"/api/Cursos/{id}/asistencia?fecha={Hoy:yyyy-MM-dd}")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await docente.PostJsonAsync($"/api/Cursos/{id}/asistencia", Lista(Hoy, (anaId, true)))).EsperarAsync(HttpStatusCode.Forbidden);
        await (await docente.PutJsonAsync($"/api/Cursos/{id}",
            new { titulo = "X", descripcion = (string?)null, cupoMaximo = 10, asistenciaMinimaPct = 80 })).EsperarAsync(HttpStatusCode.Forbidden);
        // El participante no pasa lista
        await (await ana.PostJsonAsync($"/api/Cursos/{id}/asistencia", Lista(Hoy, (anaId, true)))).EsperarAsync(HttpStatusCode.Forbidden);

        // La directora sí, y puede reasignar el curso al docente
        var directora = await api.DirectoraAsync();
        await (await directora.PostJsonAsync($"/api/Cursos/{id}/asistencia", Lista(Hoy, (anaId, true)))).EsperarAsync(HttpStatusCode.OK);
        await (await directora.PutJsonAsync($"/api/Cursos/{id}/docente", new { docenteId = (Guid?)null })).EsperarAsync(HttpStatusCode.BadRequest);
        await (await docente.PutJsonAsync($"/api/Cursos/{id}/docente", new { docenteId = await api.DocenteIdAsync() })).EsperarAsync(HttpStatusCode.Forbidden);
        await (await directora.PutJsonAsync($"/api/Cursos/{id}/docente", new { docenteId = await api.DocenteIdAsync() })).EsperarAsync(HttpStatusCode.NoContent);
        var detalle = await (await docente.GetAsync($"/api/Cursos/{id}/gestion")).JsonAsync();
        Assert.Equal("Carlos Docente", detalle.Str("docenteNombre"));

        // Editar datos: el cupo no baja de los inscritos; las fechas no se tocan aquí (van por "Reprogramar")
        await (await docente.PutJsonAsync($"/api/Cursos/{id}",
            new { titulo = "Curso editado", descripcion = "Nueva", cupoMaximo = 0, asistenciaMinimaPct = 80 })).EsperarAsync(HttpStatusCode.BadRequest);
        var editado = await (await docente.PutJsonAsync($"/api/Cursos/{id}",
            new { titulo = "Curso editado", descripcion = "Nueva", cupoMaximo = 12, asistenciaMinimaPct = 70 })).JsonAsync();
        Assert.Equal("Curso editado", editado.Str("titulo"));
        Assert.Equal(11, editado.GetProperty("cupoDisponible").GetInt32());
        Assert.Equal(curso.Str("fechaInicio"), editado.Str("fechaInicio"));
    }
}
