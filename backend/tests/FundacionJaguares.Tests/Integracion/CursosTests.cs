using System.Net;
using System.Text.Json;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// Cursos (FUJ-101), lista de espera (FUJ-111), certificados (FUJ-103/104/115) y encuesta al finalizar (FUJ-120)
[Collection(ColeccionApi.Nombre)]
public class CursosTests(ApiFixture api)
{
    private static object Curso(string titulo, int cupo = 10, int diasInicio = 1, int diasFin = 10, Guid? plantillaId = null) => new
    {
        titulo, descripcion = "Curso de prueba",
        fechaInicio = DateOnly.FromDateTime(DateTime.Today.AddDays(diasInicio)),
        fechaFin = DateOnly.FromDateTime(DateTime.Today.AddDays(diasFin)),
        cupoMaximo = cupo, asistenciaMinimaPct = 80, plantillaId
    };

    private async Task<JsonElement> CrearCursoAsync(HttpClient c, string titulo, int cupo = 10) =>
        await (await c.PostJsonAsync("/api/Cursos", Curso(titulo, cupo))).JsonAsync();

    [Fact]
    public async Task Crear_curso_valida_fechas_y_cupo()
    {
        var docente = await api.DocenteAsync();
        await (await docente.PostJsonAsync("/api/Cursos", Curso("Malo", diasInicio: 5, diasFin: 1))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await docente.PostJsonAsync("/api/Cursos", Curso("Malo", cupo: 0))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await docente.PostJsonAsync("/api/Cursos", Curso("Malo", plantillaId: Guid.NewGuid()))).EsperarAsync(HttpStatusCode.BadRequest);

        var curso = await CrearCursoAsync(docente, "Taller de reciclaje");
        Assert.Equal("PUBLICADO", curso.Str("estado"));

        var publicos = await (await api.Anonimo().GetAsync("/api/Cursos")).JsonAsync();
        Assert.Contains(publicos.EnumerateArray(), c => c.Id() == curso.Id());

        var gestionDocente = await (await docente.GetAsync("/api/Cursos/gestion")).JsonAsync();
        Assert.Contains(gestionDocente.EnumerateArray(), c => c.Id() == curso.Id());
        var gestionDirectora = await (await (await api.DirectoraAsync()).GetAsync("/api/Cursos/gestion")).JsonAsync();
        Assert.Contains(gestionDirectora.EnumerateArray(), c => c.Id() == curso.Id());
    }

    [Fact]
    public async Task Inscripcion_con_cupos_lista_de_espera_y_promocion_al_cancelar()
    {
        var docente = await api.DocenteAsync();
        var curso = await CrearCursoAsync(docente, "Curso con un cupo", cupo: 1);
        var id = curso.Id();
        var (ana, _, _) = await api.NuevoParticipanteAsync("Ana");
        var (beto, _, _) = await api.NuevoParticipanteAsync("Beto");

        // Con cupo: no se puede ir a la lista de espera, hay que inscribirse
        await (await beto.PostAsync($"/api/Cursos/{id}/lista-espera", null)).EsperarAsync(HttpStatusCode.BadRequest);

        await (await ana.PostAsync($"/api/Cursos/{id}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);
        await (await ana.PostAsync($"/api/Cursos/{id}/inscribir", null)).EsperarAsync(HttpStatusCode.BadRequest); // duplicada
        await (await beto.PostAsync($"/api/Cursos/{id}/inscribir", null)).EsperarAsync(HttpStatusCode.BadRequest); // sin cupos
        await (await beto.PostAsync($"/api/Cursos/{Guid.NewGuid()}/inscribir", null)).EsperarAsync(HttpStatusCode.NotFound);

        var espera = await (await beto.PostAsync($"/api/Cursos/{id}/lista-espera", null)).JsonAsync();
        Assert.Equal(1, espera.GetProperty("posicion").GetInt32());
        await (await beto.PostAsync($"/api/Cursos/{id}/lista-espera", null)).EsperarAsync(HttpStatusCode.BadRequest); // ya está
        await (await ana.PostAsync($"/api/Cursos/{id}/lista-espera", null)).EsperarAsync(HttpStatusCode.BadRequest);  // ya inscrita

        var miEspera = await (await beto.GetAsync("/api/Cursos/mi-lista-espera")).JsonAsync();
        Assert.Contains(miEspera.EnumerateArray(), e => e.GetProperty("cursoId").GetGuid() == id);

        var gestion = await (await docente.GetAsync($"/api/Cursos/{id}/lista-espera")).JsonAsync();
        Assert.Equal("Beto", gestion[0].Str("nombre"));

        // Al cancelar Ana, Beto pasa automáticamente de la lista de espera a inscrito
        await (await ana.DeleteAsync($"/api/Cursos/{id}/inscripcion")).EsperarAsync(HttpStatusCode.NoContent);
        var inscBeto = await (await beto.GetAsync("/api/Cursos/mis-inscripciones")).JsonAsync();
        Assert.Contains(inscBeto.EnumerateArray(), i => i.GetProperty("cursoId").GetGuid() == id && i.Str("estadoInscripcion") == "CONFIRMADA");
        Assert.Empty((await (await beto.GetAsync("/api/Cursos/mi-lista-espera")).JsonAsync()).EnumerateArray());

        await (await ana.DeleteAsync($"/api/Cursos/{Guid.NewGuid()}/inscripcion")).EsperarAsync(HttpStatusCode.NotFound);
        await (await ana.DeleteAsync($"/api/Cursos/{id}/lista-espera")).EsperarAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Salir_de_la_lista_de_espera()
    {
        var curso = await CrearCursoAsync(await api.DirectoraAsync(), "Curso lleno", cupo: 1);
        var (ana, _, _) = await api.NuevoParticipanteAsync();
        var (beto, _, _) = await api.NuevoParticipanteAsync();
        await (await ana.PostAsync($"/api/Cursos/{curso.Id()}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);
        await (await beto.PostAsync($"/api/Cursos/{curso.Id()}/lista-espera", null)).EsperarAsync(HttpStatusCode.OK);

        await (await beto.DeleteAsync($"/api/Cursos/{curso.Id()}/lista-espera")).EsperarAsync(HttpStatusCode.NoContent);
        Assert.Empty((await (await beto.GetAsync("/api/Cursos/mi-lista-espera")).JsonAsync()).EnumerateArray());

        // El docente no ve la lista de espera de un curso que no es suyo
        await (await (await api.DocenteAsync()).GetAsync($"/api/Cursos/{curso.Id()}/lista-espera")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await (await api.DirectoraAsync()).GetAsync($"/api/Cursos/{Guid.NewGuid()}/lista-espera")).EsperarAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Finalizar_curso_emite_certificados_verificables_y_envia_encuesta()
    {
        var directora = await api.DirectoraAsync();
        var docente = await api.DocenteAsync();

        // Plantilla propia (FUJ-115) asignada al curso
        var plantilla = await (await directora.PostJsonAsync("/api/Certificados/plantillas", new
        {
            nombre = "Plantilla " + Guid.NewGuid().ToString("N")[..6], tipoActividad = "TALLER", titulo = "Certificado de participación",
            texto = "Se certifica que {participante} participó en {actividad} el {fecha}.",
            firmaUnoNombre = "María", firmaUnoCargo = "Directora", firmaDosNombre = (string?)null, firmaDosCargo = (string?)null
        })).JsonAsync();

        var curso = await CrearCursoAsync(docente, "Curso a finalizar");
        var id = curso.Id();
        await (await docente.PutJsonAsync($"/api/Cursos/{id}/plantilla", new { plantillaId = plantilla.Id() })).EsperarAsync(HttpStatusCode.NoContent);
        await (await docente.PutJsonAsync($"/api/Cursos/{id}/plantilla", new { plantillaId = Guid.NewGuid() })).EsperarAsync(HttpStatusCode.BadRequest);

        var (ana, _, _) = await api.NuevoParticipanteAsync("Ana Certificada");
        await (await ana.PostAsync($"/api/Cursos/{id}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);

        // Encuesta inválida: el curso no se finaliza
        await (await docente.PostJsonAsync($"/api/Cursos/{id}/finalizar", new { preguntas = new[] { new { texto = "Solo una", tipo = "ESCALA" } } }))
            .EsperarAsync(HttpStatusCode.BadRequest);

        var fin = await (await docente.PostAsync($"/api/Cursos/{id}/finalizar", null)).JsonAsync();
        Assert.Equal(1, fin.GetProperty("certificados_generados").GetInt32());
        Assert.Equal(1, fin.GetProperty("encuestas_enviadas").GetInt32());
        await (await docente.PostAsync($"/api/Cursos/{id}/finalizar", null)).EsperarAsync(HttpStatusCode.BadRequest); // ya finalizado
        await (await docente.PutJsonAsync($"/api/Cursos/{id}/plantilla", new { plantillaId = (Guid?)null })).EsperarAsync(HttpStatusCode.BadRequest);
        await (await ana.DeleteAsync($"/api/Cursos/{id}/inscripcion")).EsperarAsync(HttpStatusCode.BadRequest); // ya terminó

        // Certificado: listado, PDF y verificación pública (FUJ-104)
        var certs = await (await ana.GetAsync("/api/Certificados/mios")).JsonAsync();
        var cert = certs.EnumerateArray().Single(c => c.Str("nombreCurso") == "Curso a finalizar");
        var pdf = await ana.GetAsync($"/api/Certificados/{cert.Id()}/pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var (otro, _, _) = await api.NuevoParticipanteAsync();
        await (await otro.GetAsync($"/api/Certificados/{cert.Id()}/pdf")).EsperarAsync(HttpStatusCode.NotFound);

        var verif = await (await api.Anonimo().GetAsync($"/api/Certificados/verificar/{cert.Str("codigoUnico")}")).JsonAsync();
        Assert.True(verif.GetProperty("valido").GetBoolean());
        Assert.Equal("Ana Certificada", verif.Str("nombreParticipante"));
        var falso = await (await api.Anonimo().GetAsync("/api/Certificados/verificar/NO-EXISTE")).JsonAsync();
        Assert.False(falso.GetProperty("valido").GetBoolean());

        // La plantilla en uso por un curso finalizado sí se puede borrar (solo bloquean los cursos activos)
        // y la encuesta pendiente aparece en el panel de la participante
        var pendientes = await (await ana.GetAsync("/api/Encuestas/mis-pendientes")).JsonAsync();
        var token = pendientes.EnumerateArray().Single(p => p.Str("actividad") == "Curso a finalizar").Str("token");

        var encuesta = await (await api.Anonimo().GetAsync($"/api/Encuestas/responder/{token}")).JsonAsync();
        Assert.False(encuesta.GetProperty("yaRespondida").GetBoolean());
        var preguntas = encuesta.GetProperty("preguntas").EnumerateArray().ToList();

        // Respuesta incompleta o con puntaje fuera de rango: 400
        await (await api.Anonimo().PostJsonAsync($"/api/Encuestas/responder/{token}", new { respuestas = Array.Empty<object>() }))
            .EsperarAsync(HttpStatusCode.BadRequest);
        var fueraDeRango = preguntas.Select(p => new { preguntaId = p.Id(), valor = p.Str("tipo") == "ESCALA" ? 9 : (int?)null, texto = "x" });
        await (await api.Anonimo().PostJsonAsync($"/api/Encuestas/responder/{token}", new { respuestas = fueraDeRango }))
            .EsperarAsync(HttpStatusCode.BadRequest);

        var respuestas = preguntas.Select(p => new
        {
            preguntaId = p.Id(),
            valor = p.Str("tipo") == "ESCALA" ? 5 : (int?)null,
            texto = p.Str("tipo") == "TEXTO" ? "Muy bueno" : null
        });
        await (await api.Anonimo().PostJsonAsync($"/api/Encuestas/responder/{token}", new { respuestas })).EsperarAsync(HttpStatusCode.NoContent);
        await (await api.Anonimo().PostJsonAsync($"/api/Encuestas/responder/{token}", new { respuestas })).EsperarAsync(HttpStatusCode.Conflict);
        await (await api.Anonimo().GetAsync("/api/Encuestas/responder/token-falso")).EsperarAsync(HttpStatusCode.NotFound);
        Assert.DoesNotContain((await (await ana.GetAsync("/api/Encuestas/mis-pendientes")).JsonAsync()).EnumerateArray(),
            p => p.Str("actividad") == "Curso a finalizar");

        // Resultados: la directora y el docente del curso los ven
        var lista = await (await directora.GetAsync("/api/Encuestas")).JsonAsync();
        var resumen = lista.EnumerateArray().Single(e => e.Str("actividad") == "Curso a finalizar");
        Assert.Equal(1, resumen.GetProperty("respuestas").GetInt32());
        var detalle = await (await docente.GetAsync($"/api/Encuestas/{resumen.Id()}")).JsonAsync();
        Assert.Equal(5m, detalle.GetProperty("resumen").GetProperty("promedioSatisfaccion").GetDecimal());
        await (await directora.GetAsync($"/api/Encuestas/{Guid.NewGuid()}")).EsperarAsync(HttpStatusCode.NotFound);
        var porDefecto = await (await docente.GetAsync("/api/Encuestas/preguntas-por-defecto")).JsonAsync();
        Assert.True(porDefecto.GetArrayLength() >= 3);
    }

    [Fact]
    public async Task Docente_no_puede_finalizar_ni_cambiar_cursos_ajenos()
    {
        var curso = await CrearCursoAsync(await api.DirectoraAsync(), "Curso de la directora");
        var docente = await api.DocenteAsync();
        await (await docente.PostAsync($"/api/Cursos/{curso.Id()}/finalizar", null)).EsperarAsync(HttpStatusCode.Forbidden);
        await (await docente.PutJsonAsync($"/api/Cursos/{curso.Id()}/plantilla", new { plantillaId = (Guid?)null })).EsperarAsync(HttpStatusCode.Forbidden);
        await (await docente.PostAsync($"/api/Cursos/{Guid.NewGuid()}/finalizar", null)).EsperarAsync(HttpStatusCode.NotFound);
        await (await docente.PutJsonAsync($"/api/Cursos/{Guid.NewGuid()}/plantilla", new { plantillaId = (Guid?)null })).EsperarAsync(HttpStatusCode.NotFound);
    }
}
