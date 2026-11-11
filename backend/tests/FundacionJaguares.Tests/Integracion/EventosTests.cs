using System.Net;
using System.Text.Json;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// Eventos (FUJ-102), check-in con QR (FUJ-113), calendario (FUJ-114) y cierre con encuesta (FUJ-120)
[Collection(ColeccionApi.Nombre)]
public class EventosTests(ApiFixture api)
{
    private async Task<JsonElement> CrearEventoAsync(string titulo, DateTime inicioUtc, DateTime? finUtc = null)
    {
        var enc = await api.EncargadoAsync();
        var r = await enc.PostJsonAsync("/api/Eventos", new
        {
            titulo, descripcion = "Evento de prueba", lugar = "Plaza Murillo",
            fechaInicio = Http.Z(inicioUtc), fechaFin = finUtc is null ? null : Http.Z(finUtc.Value)
        });
        return await r.JsonAsync();
    }

    [Fact]
    public async Task Checkin_QR_valido_repetido_invalido_de_otro_evento_y_fuera_de_fecha()
    {
        var hoy = await CrearEventoAsync("Feria de hoy", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3));
        var futuro = await CrearEventoAsync("Feria futura", DateTime.UtcNow.AddDays(20));
        var enc = await api.EncargadoAsync();
        var (ana, _, _) = await api.NuevoParticipanteAsync("Ana QR");

        var insc = await (await ana.PostAsync($"/api/Eventos/{hoy.Id()}/inscribir", null)).JsonAsync();
        Assert.Matches("^[A-Za-z0-9_-]{22}$", insc.Str("codigoQr"));
        await (await ana.PostAsync($"/api/Eventos/{hoy.Id()}/inscribir", null)).EsperarAsync(HttpStatusCode.BadRequest);
        await (await ana.PostAsync($"/api/Eventos/{Guid.NewGuid()}/inscribir", null)).EsperarAsync(HttpStatusCode.NotFound);
        await (await enc.PostAsync($"/api/Eventos/{hoy.Id()}/inscribir", null)).EsperarAsync(HttpStatusCode.Forbidden);
        var inscFutura = await (await ana.PostAsync($"/api/Eventos/{futuro.Id()}/inscribir", null)).JsonAsync();

        var mias = await (await ana.GetAsync("/api/Eventos/mis-inscripciones")).JsonAsync();
        Assert.Equal(2, mias.GetArrayLength());

        var qr = await ana.GetAsync($"/api/Eventos/mis-inscripciones/{insc.Id()}/qr");
        Assert.Equal("image/svg+xml", qr.Content.Headers.ContentType?.MediaType);
        var (otro, _, _) = await api.NuevoParticipanteAsync();
        await (await otro.GetAsync($"/api/Eventos/mis-inscripciones/{insc.Id()}/qr")).EsperarAsync(HttpStatusCode.NotFound);

        async Task<JsonElement> Checkin(Guid evento, string codigo, HttpStatusCode esperado) =>
            await (await enc.PostJsonAsync($"/api/Eventos/{evento}/checkin", new { codigo })).JsonAsync(esperado);

        var ok = await Checkin(hoy.Id(), "JAGUARES-EV:" + insc.Str("codigoQr"), HttpStatusCode.OK);
        Assert.Equal("REGISTRADO", ok.Str("resultado"));
        Assert.Equal("Ana QR", ok.Str("participante"));
        Assert.Equal("YA_REGISTRADO", (await Checkin(hoy.Id(), insc.Str("codigoQr"), HttpStatusCode.Conflict)).Str("resultado"));
        Assert.Equal("INVALIDO", (await Checkin(hoy.Id(), "inventado", HttpStatusCode.NotFound)).Str("resultado"));
        Assert.Equal("INVALIDO", (await Checkin(hoy.Id(), "  ", HttpStatusCode.NotFound)).Str("resultado"));
        Assert.Equal("OTRO_EVENTO", (await Checkin(hoy.Id(), inscFutura.Str("codigoQr"), HttpStatusCode.BadRequest)).Str("resultado"));
        Assert.Equal("FUERA_DE_FECHA", (await Checkin(futuro.Id(), inscFutura.Str("codigoQr"), HttpStatusCode.BadRequest)).Str("resultado"));

        var inscritos = await (await enc.GetAsync($"/api/Eventos/{hoy.Id()}/inscritos")).JsonAsync();
        Assert.Equal("ASISTIO", inscritos[0].Str("estado"));
        var asistencias = await (await enc.GetAsync($"/api/Eventos/{hoy.Id()}/asistencias")).JsonAsync();
        Assert.Equal(1, asistencias.GetArrayLength());

        // Cancelar la inscripción al evento futuro; el QR deja de servir
        await (await ana.DeleteAsync($"/api/Eventos/{futuro.Id()}/inscripcion")).EsperarAsync(HttpStatusCode.NoContent);
        Assert.Equal("INVALIDO", (await Checkin(futuro.Id(), inscFutura.Str("codigoQr"), HttpStatusCode.NotFound)).Str("resultado"));
        await (await ana.DeleteAsync($"/api/Eventos/{Guid.NewGuid()}/inscripcion")).EsperarAsync(HttpStatusCode.NotFound);
        // Re-inscribirse genera un código nuevo
        var reinsc = await (await ana.PostAsync($"/api/Eventos/{futuro.Id()}/inscribir", null)).JsonAsync();
        Assert.NotEqual(inscFutura.Str("codigoQr"), reinsc.Str("codigoQr"));
    }

    [Fact]
    public async Task No_se_puede_inscribir_a_un_evento_que_ya_termino()
    {
        var pasado = await CrearEventoAsync("Evento pasado", DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(-3).AddHours(2));
        var (ana, _, _) = await api.NuevoParticipanteAsync();
        await (await ana.PostAsync($"/api/Eventos/{pasado.Id()}/inscribir", null)).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Asistencia_manual_y_cierre_con_encuesta()
    {
        var evento = await CrearEventoAsync("Evento a cerrar", DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddHours(2));
        var enc = await api.EncargadoAsync();
        var (ana, anaId, _) = await api.NuevoParticipanteAsync("Ana Encuesta");

        await (await enc.PostJsonAsync($"/api/Eventos/{evento.Id()}/asistencia", new { participanteId = anaId })).EsperarAsync(HttpStatusCode.OK);
        await (await enc.PostJsonAsync($"/api/Eventos/{evento.Id()}/asistencia", new { participanteId = anaId })).EsperarAsync(HttpStatusCode.BadRequest);
        await (await enc.PostJsonAsync($"/api/Eventos/{Guid.NewGuid()}/asistencia", new { participanteId = anaId })).EsperarAsync(HttpStatusCode.NotFound);

        var cierre = await (await enc.PostAsync($"/api/Eventos/{evento.Id()}/cerrar-asistencia", null)).JsonAsync();
        Assert.Equal(1, cierre.GetProperty("asistentes").GetInt32());
        Assert.Equal(1, cierre.GetProperty("encuestasEnviadas").GetInt32());

        await (await enc.PostAsync($"/api/Eventos/{evento.Id()}/cerrar-asistencia", null)).EsperarAsync(HttpStatusCode.BadRequest);
        await (await enc.PostJsonAsync($"/api/Eventos/{evento.Id()}/asistencia", new { participanteId = anaId })).EsperarAsync(HttpStatusCode.BadRequest);
        await (await enc.PostAsync($"/api/Eventos/{Guid.NewGuid()}/cerrar-asistencia", null)).EsperarAsync(HttpStatusCode.NotFound);

        var pendientes = await (await ana.GetAsync("/api/Encuestas/mis-pendientes")).JsonAsync();
        Assert.Contains(pendientes.EnumerateArray(), p => p.Str("actividad") == "Evento a cerrar");

        // El encargado de eventos ve las encuestas de eventos
        var lista = await (await enc.GetAsync("/api/Encuestas")).JsonAsync();
        Assert.Contains(lista.EnumerateArray(), e => e.Str("actividad") == "Evento a cerrar");

        var gestion = await (await enc.GetAsync("/api/Eventos/gestion")).JsonAsync();
        Assert.Contains(gestion.EnumerateArray(), e => e.Id() == evento.Id() && e.Str("estado") == "FINALIZADO");
        var publicos = await (await api.Anonimo().GetAsync("/api/Eventos")).JsonAsync();
        Assert.DoesNotContain(publicos.EnumerateArray(), e => e.Id() == evento.Id());
    }

    [Fact]
    public async Task No_se_cierra_un_evento_que_no_empezo_ni_con_encuesta_invalida()
    {
        var futuro = await CrearEventoAsync("Evento sin empezar", DateTime.UtcNow.AddDays(2));
        var enc = await api.EncargadoAsync();
        await (await enc.PostAsync($"/api/Eventos/{futuro.Id()}/cerrar-asistencia", null)).EsperarAsync(HttpStatusCode.BadRequest);
        await (await enc.PostJsonAsync($"/api/Eventos/{futuro.Id()}/cerrar-asistencia",
            new { preguntas = new[] { new { texto = "x", tipo = "TEXTO" } } })).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Calendario_devuelve_cursos_y_eventos_del_rango_y_valida_parametros()
    {
        var evento = await CrearEventoAsync("Evento calendario", DateTime.UtcNow.AddDays(5));
        var desde = DateOnly.FromDateTime(DateTime.Today.AddDays(-2)).ToString("yyyy-MM-dd");
        var hasta = DateOnly.FromDateTime(DateTime.Today.AddDays(30)).ToString("yyyy-MM-dd");

        var acts = await (await api.Anonimo().GetAsync($"/api/Actividades?desde={desde}&hasta={hasta}")).JsonAsync();
        Assert.Contains(acts.EnumerateArray(), a => a.Str("tipo") == "EVENTO" && a.Id() == evento.Id());

        await (await api.Anonimo().GetAsync("/api/Actividades")).EsperarAsync(HttpStatusCode.BadRequest);
        await (await api.Anonimo().GetAsync("/api/Actividades?desde=2026-01-01&hasta=2026-12-31")).EsperarAsync(HttpStatusCode.BadRequest);
        await (await api.Anonimo().GetAsync("/api/Actividades?desde=2026-02-01&hasta=2026-01-01")).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Panel_del_participante_reune_cursos_eventos_y_certificados()
    {
        var evento = await CrearEventoAsync("Evento del panel", DateTime.UtcNow.AddDays(3));
        var (ana, _, _) = await api.NuevoParticipanteAsync();
        await (await ana.PostAsync($"/api/Eventos/{evento.Id()}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);

        var panel = await (await ana.GetAsync("/api/Participante/panel")).JsonAsync();
        Assert.Contains(panel.GetProperty("proximas").EnumerateArray(), a => a.Str("titulo") == "Evento del panel");
        Assert.True(panel.GetProperty("resumen").GetProperty("eventosProximos").GetInt32() >= 1);
        await (await (await api.EncargadoAsync()).GetAsync("/api/Participante/panel")).EsperarAsync(HttpStatusCode.Forbidden);
    }
}
