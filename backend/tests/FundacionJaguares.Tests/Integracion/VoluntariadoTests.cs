using System.Net;
using System.Text.Json;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// Registro único de voluntarios (FUJ-107) y asignación con registro de horas (FUJ-118)
[Collection(ColeccionApi.Nombre)]
public class VoluntariadoTests(ApiFixture api)
{
    private async Task<Guid> VoluntarioSeedIdAsync()
    {
        var lista = await (await (await api.DirectoraAsync()).GetAsync("/api/Voluntarios")).JsonAsync();
        return lista.EnumerateArray().Single(v => v.Str("email") == Usuarios.Voluntario).Id();
    }

    private async Task<JsonElement> EventoAsync(string titulo, DateTime inicio) =>
        await (await (await api.EncargadoAsync()).PostJsonAsync("/api/Eventos", new
        {
            titulo, descripcion = (string?)null, lugar = "Parque", fechaInicio = Http.Z(inicio), fechaFin = Http.Z(inicio.AddHours(2))
        })).JsonAsync();

    [Fact]
    public async Task Postulacion_publica_sin_duplicados_y_registro_de_participacion()
    {
        var email = $"v{Guid.NewGuid().ToString("N")[..10]}@correo.com";
        var v = await (await api.Anonimo().PostJsonAsync("/api/Voluntarios/postular",
            new { nombreCompleto = "Vale Voluntaria", email, telefono = "70000001" })).JsonAsync();
        await (await api.Anonimo().PostJsonAsync("/api/Voluntarios/postular",
            new { nombreCompleto = "Otra", email, telefono = (string?)null })).EsperarAsync(HttpStatusCode.BadRequest);

        var enc = await api.EncargadoAsync();
        var evento = await EventoAsync("Limpieza del parque", DateTime.UtcNow.AddDays(-2));
        await (await enc.PostJsonAsync($"/api/Voluntarios/{v.Id()}/participaciones",
            new { eventoId = evento.Id(), descripcion = "Apoyo en logística", horas = 3.5m })).EsperarAsync(HttpStatusCode.OK);
        await (await enc.PostJsonAsync($"/api/Voluntarios/{Guid.NewGuid()}/participaciones",
            new { eventoId = (Guid?)null, descripcion = "x", horas = (decimal?)null })).EsperarAsync(HttpStatusCode.NotFound);
        await (await enc.PostJsonAsync($"/api/Voluntarios/{v.Id()}/participaciones",
            new { eventoId = (Guid?)null, descripcion = "x", horas = -2m })).EsperarAsync(HttpStatusCode.BadRequest);

        var lista = await (await enc.GetAsync("/api/Voluntarios")).JsonAsync();
        var conHistorial = lista.EnumerateArray().Single(x => x.Id() == v.Id());
        Assert.Equal("Limpieza del parque", conHistorial.GetProperty("historial")[0].Str("eventoTitulo"));
        Assert.Equal(3.5m, conHistorial.GetProperty("totalHoras").GetDecimal());
        await (await (await api.JuanAsync()).GetAsync("/api/Voluntarios")).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Asignacion_a_evento_y_registro_de_horas_al_terminar()
    {
        var enc = await api.EncargadoAsync();
        var voluntarioId = await VoluntarioSeedIdAsync();
        var evento = await EventoAsync("Feria con voluntarios", DateTime.UtcNow.AddDays(4));

        var asignables = await (await enc.GetAsync("/api/Voluntarios/actividades-asignables")).JsonAsync();
        Assert.Contains(asignables.EnumerateArray(), a => a.Id() == evento.Id());

        // Validaciones de la asignación
        await (await enc.PostJsonAsync("/api/Voluntarios/asignaciones", new { voluntarioIds = Array.Empty<Guid>(), eventoId = evento.Id() }))
            .EsperarAsync(HttpStatusCode.BadRequest);
        await (await enc.PostJsonAsync("/api/Voluntarios/asignaciones", new { voluntarioIds = new[] { voluntarioId } }))
            .EsperarAsync(HttpStatusCode.BadRequest); // sin actividad
        await (await enc.PostJsonAsync("/api/Voluntarios/asignaciones", new { voluntarioIds = new[] { voluntarioId }, eventoId = Guid.NewGuid() }))
            .EsperarAsync(HttpStatusCode.NotFound);

        var r = await (await enc.PostJsonAsync("/api/Voluntarios/asignaciones",
            new { voluntarioIds = new[] { voluntarioId, Guid.NewGuid() }, eventoId = evento.Id(), tarea = "Mesa de inscripción" })).JsonAsync();
        var asignacion = r.GetProperty("asignadas").EnumerateArray().Single();
        Assert.Single(r.GetProperty("omitidas").EnumerateArray()); // el id inexistente se informa, no rompe la asignación

        // Asignarlo otra vez a la misma actividad se omite
        var repetida = await (await enc.PostJsonAsync("/api/Voluntarios/asignaciones",
            new { voluntarioIds = new[] { voluntarioId }, eventoId = evento.Id() })).JsonAsync();
        Assert.Empty(repetida.GetProperty("asignadas").EnumerateArray());

        var porEvento = await (await enc.GetAsync($"/api/Voluntarios/asignaciones?eventoId={evento.Id()}")).JsonAsync();
        Assert.Single(porEvento.EnumerateArray());
        var mias = await (await (await api.VoluntarioAsync()).GetAsync("/api/Voluntarios/mis-asignaciones")).JsonAsync();
        Assert.Contains(mias.EnumerateArray(), a => a.Id() == asignacion.Id());

        // Antes de que termine no se registran horas
        await (await enc.PutJsonAsync($"/api/Voluntarios/asignaciones/{asignacion.Id()}/horas", new { horas = 4 })).EsperarAsync(HttpStatusCode.BadRequest);

        // Se simula que el evento ya pasó
        await api.EnBdAsync(async db =>
        {
            var e = await db.Eventos.FindAsync(evento.Id());
            e!.FechaInicio = DateTime.UtcNow.AddDays(-1);
            e.FechaFin = DateTime.UtcNow.AddDays(-1).AddHours(2);
        });
        await (await enc.PutJsonAsync($"/api/Voluntarios/asignaciones/{asignacion.Id()}/horas", new { horas = 0 })).EsperarAsync(HttpStatusCode.BadRequest);
        var conHoras = await (await enc.PutJsonAsync($"/api/Voluntarios/asignaciones/{asignacion.Id()}/horas", new { horas = 4.5m })).JsonAsync();
        Assert.Equal(4.5m, conHoras.GetProperty("horas").GetDecimal());
        Assert.Equal("COMPLETADA", conHoras.Str("estado"));

        // Una asignación completada ya no se cancela
        await (await enc.DeleteAsync($"/api/Voluntarios/asignaciones/{asignacion.Id()}")).EsperarAsync(HttpStatusCode.BadRequest);
        await (await enc.PutJsonAsync($"/api/Voluntarios/asignaciones/{Guid.NewGuid()}/horas", new { horas = 1 })).EsperarAsync(HttpStatusCode.NotFound);

        var historial = await (await (await api.VoluntarioAsync()).GetAsync("/api/Voluntarios/mi-historial")).JsonAsync();
        Assert.True(historial.GetProperty("totalHoras").GetDecimal() >= 4.5m);
    }

    [Fact]
    public async Task Asignacion_a_curso_cancelacion_y_actividad_terminada()
    {
        var dir = await api.DirectoraAsync();
        var voluntarioId = await VoluntarioSeedIdAsync();
        var curso = await (await (await api.DocenteAsync()).PostJsonAsync("/api/Cursos", new
        {
            titulo = "Curso con voluntarios", descripcion = (string?)null,
            fechaInicio = DateOnly.FromDateTime(DateTime.Today.AddDays(2)), fechaFin = DateOnly.FromDateTime(DateTime.Today.AddDays(9)),
            cupoMaximo = 5, asistenciaMinimaPct = 80
        })).JsonAsync();

        var r = await (await dir.PostJsonAsync("/api/Voluntarios/asignaciones",
            new { voluntarioIds = new[] { voluntarioId }, cursoId = curso.Id(), tarea = (string?)null })).JsonAsync();
        Assert.True(r.GetProperty("asignadas").GetArrayLength() == 1, r.ToString());
        var asignacion = r.GetProperty("asignadas")[0];
        Assert.Equal("CURSO", asignacion.Str("tipo"));

        await (await dir.DeleteAsync($"/api/Voluntarios/asignaciones/{asignacion.Id()}")).EsperarAsync(HttpStatusCode.NoContent);
        await (await dir.PutJsonAsync($"/api/Voluntarios/asignaciones/{asignacion.Id()}/horas", new { horas = 2 })).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.DeleteAsync($"/api/Voluntarios/asignaciones/{Guid.NewGuid()}")).EsperarAsync(HttpStatusCode.NotFound);

        var pasado = await EventoAsync("Evento terminado", DateTime.UtcNow.AddDays(-5));
        await (await dir.PostJsonAsync("/api/Voluntarios/asignaciones", new { voluntarioIds = new[] { voluntarioId }, eventoId = pasado.Id() }))
            .EsperarAsync(HttpStatusCode.BadRequest);
        var porCurso = await (await dir.GetAsync($"/api/Voluntarios/asignaciones?cursoId={curso.Id()}")).JsonAsync();
        // Las asignaciones canceladas ya no aparecen en el listado de la actividad
        Assert.DoesNotContain(porCurso.EnumerateArray(), x => x.Id() == asignacion.Id());
    }

    [Fact]
    public async Task Mi_historial_de_un_voluntario_que_no_esta_registrado_devuelve_404()
    {
        // Usuario con rol VOLUNTARIO pero sin fila en voluntarios
        var (_, id, email) = await api.NuevoParticipanteAsync();
        await api.EnBdAsync(async db => { (await db.Usuarios.FindAsync(id))!.Rol = Domain.Entities.Rol.VOLUNTARIO; });
        var c = await api.ComoAsync(email, "Clave123!"); // token ya emitido como participante: se pide uno nuevo
        var nuevo = await api.Anonimo().PostJsonAsync("/api/Auth/login", new { email, password = "Clave123!" });
        var token = (await nuevo.JsonAsync()).Str("token");
        c.DefaultRequestHeaders.Authorization = new("Bearer", token);
        await (await c.GetAsync("/api/Voluntarios/mi-historial")).EsperarAsync(HttpStatusCode.NotFound);
    }
}
