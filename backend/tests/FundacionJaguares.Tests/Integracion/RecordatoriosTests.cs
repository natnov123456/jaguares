using System.Net;
using System.Text.Json;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Tests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace FundacionJaguares.Tests.Integracion;

// JAG-38 / FUJ-119: recordatorio 24 h antes, reprogramación y cancelación con aviso a los inscritos
[Collection(ColeccionApi.Nombre)]
public class RecordatoriosTests(ApiFixture api)
{
    private async Task<JsonElement> EventoAsync(string titulo, DateTime inicio, DateTime? fin = null) =>
        await (await (await api.EncargadoAsync()).PostJsonAsync("/api/Eventos", new
        {
            titulo, descripcion = (string?)null, lugar = "Plaza Murillo",
            fechaInicio = Http.Z(inicio), fechaFin = Http.Z(fin ?? inicio.AddHours(2))
        })).JsonAsync();

    private async Task<JsonElement> CursoAsync(HttpClient c, string titulo, DateOnly inicio, int cupo = 10) =>
        await (await c.PostJsonAsync("/api/Cursos", new
        {
            titulo, descripcion = (string?)null, fechaInicio = inicio, fechaFin = inicio.AddDays(5), cupoMaximo = cupo, asistenciaMinimaPct = 80
        })).JsonAsync();

    private async Task<ResultadoRecordatoriosDto> EjecutarTareaAsync(DateTime ahora)
    {
        using var scope = api.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRecordatorioService>().EnviarPendientesAsync(ahora);
    }

    private List<CorreoEnviado> Recordatorios(string email, string titulo) =>
        api.Correos.Para(email).Where(c => c.Asunto == $"Recordatorio: {titulo} es mañana").ToList();

    [Fact]
    public async Task Evento_recibe_un_solo_recordatorio_24h_antes_con_su_QR()
    {
        var titulo = "Feria recordatorio " + Guid.NewGuid().ToString("N")[..5];
        var inicio = DateTime.UtcNow.AddHours(30);
        var evento = await EventoAsync(titulo, inicio);
        var (ana, _, email) = await api.NuevoParticipanteAsync("Ana Recordada");
        var insc = await (await ana.PostAsync($"/api/Eventos/{evento.Id()}/inscribir", null)).JsonAsync();
        var (beto, _, emailBeto) = await api.NuevoParticipanteAsync();
        await (await beto.PostAsync($"/api/Eventos/{evento.Id()}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);
        await (await beto.DeleteAsync($"/api/Eventos/{evento.Id()}/inscripcion")).EsperarAsync(HttpStatusCode.NoContent);

        // Faltan 30 h: todavía no
        await EjecutarTareaAsync(DateTime.UtcNow);
        Assert.Empty(Recordatorios(email, titulo));

        // Faltan 23 h: se envía, y en las siguientes ejecuciones ya no se repite
        var ahora = inicio.AddHours(-23);
        await EjecutarTareaAsync(ahora);
        await EjecutarTareaAsync(ahora.AddHours(1));
        await EjecutarTareaAsync(ahora.AddHours(2));
        var correo = Assert.Single(Recordatorios(email, titulo));

        Assert.Contains("Plaza Murillo", correo.Texto);
        Assert.Contains(insc.Str("codigoQr"), correo.Texto);
        Assert.Contains("cid:" + PlantillasCorreo.ContentIdQr, correo.Html);
        Assert.Equal(PlantillasCorreo.ContentIdQr, correo.Adjunto!.ContentId);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, correo.Adjunto.Contenido[..4]); // PNG
        Assert.Empty(Recordatorios(emailBeto, titulo)); // canceló su inscripción

        // Ya empezado el evento no se envía nada
        await EjecutarTareaAsync(inicio.AddHours(1));
        Assert.Single(Recordatorios(email, titulo));
    }

    [Fact]
    public async Task Curso_recibe_recordatorio_el_dia_anterior_a_las_8()
    {
        var inicio = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4).AddDays(3));
        var titulo = "Curso recordatorio " + Guid.NewGuid().ToString("N")[..5];
        var curso = await CursoAsync(await api.DocenteAsync(), titulo, inicio);
        var (ana, _, email) = await api.NuevoParticipanteAsync("Ana Curso");
        await (await ana.PostAsync($"/api/Cursos/{curso.Id()}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);

        var inicioUtc = RecordatorioService.InicioCursoUtc(inicio);
        Assert.Equal(12, inicioUtc.Hour); // 08:00 en Bolivia

        await EjecutarTareaAsync(inicioUtc.AddHours(-25));
        Assert.Empty(Recordatorios(email, titulo));
        await EjecutarTareaAsync(inicioUtc.AddHours(-24));
        await EjecutarTareaAsync(inicioUtc.AddHours(-23));
        var correo = Assert.Single(Recordatorios(email, titulo));
        Assert.Null(correo.Adjunto); // los cursos no usan QR
        Assert.Contains(titulo, correo.Html);
    }

    [Fact]
    public async Task Reprogramar_evento_avisa_y_vuelve_a_programar_el_recordatorio()
    {
        var titulo = "Evento reprogramado " + Guid.NewGuid().ToString("N")[..5];
        var inicio = DateTime.UtcNow.AddHours(20);
        var evento = await EventoAsync(titulo, inicio);
        var (ana, _, email) = await api.NuevoParticipanteAsync("Ana Reprogramada");
        await (await ana.PostAsync($"/api/Eventos/{evento.Id()}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);
        await EjecutarTareaAsync(DateTime.UtcNow);
        Assert.Single(Recordatorios(email, titulo));

        var enc = await api.EncargadoAsync();
        var url = $"/api/Eventos/{evento.Id()}/reprogramar";
        object Datos(DateTime ini, DateTime? fin = null, string? lugar = null, string? motivo = null) =>
            new { fechaInicio = Http.Z(ini), fechaFin = fin is null ? null : Http.Z(fin.Value), lugar, motivo };

        // Validaciones
        await (await enc.PutJsonAsync(url, Datos(DateTime.UtcNow.AddHours(-1)))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await enc.PutJsonAsync(url, Datos(inicio.AddDays(3), inicio.AddDays(2)))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await enc.PutJsonAsync(url, Datos(inicio, inicio.AddHours(2)))).EsperarAsync(HttpStatusCode.BadRequest); // sin cambios
        await (await enc.PutJsonAsync(url, Datos(inicio.AddDays(3), motivo: new string('x', 301)))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await enc.PutJsonAsync($"/api/Eventos/{Guid.NewGuid()}/reprogramar", Datos(inicio.AddDays(3)))).EsperarAsync(HttpStatusCode.NotFound);
        await (await ana.PutJsonAsync(url, Datos(inicio.AddDays(3)))).EsperarAsync(HttpStatusCode.Forbidden);

        var nuevoInicio = inicio.AddDays(3);
        var r = await (await enc.PutJsonAsync(url, Datos(nuevoInicio, nuevoInicio.AddHours(3), "Coliseo Cerrado", "Lluvia"))).JsonAsync();
        Assert.Equal(1, r.GetProperty("notificados").GetInt32());

        var aviso = Assert.Single(api.Correos.Para(email), c => c.Asunto == $"Cambio de fecha: {titulo}");
        Assert.Contains("Coliseo Cerrado", aviso.Texto);
        Assert.Contains("Lluvia", aviso.Html);

        // El recordatorio se envía de nuevo 24 h antes de la fecha nueva
        await EjecutarTareaAsync(nuevoInicio.AddHours(-23));
        Assert.Equal(2, Recordatorios(email, titulo).Count);
    }

    [Fact]
    public async Task Cancelar_evento_avisa_y_ya_no_admite_inscripciones_ni_checkin()
    {
        var titulo = "Evento cancelado " + Guid.NewGuid().ToString("N")[..5];
        var evento = await EventoAsync(titulo, DateTime.UtcNow.AddHours(2));
        var (ana, _, email) = await api.NuevoParticipanteAsync("Ana Cancelada");
        var insc = await (await ana.PostAsync($"/api/Eventos/{evento.Id()}/inscribir", null)).JsonAsync();
        var dir = await api.DirectoraAsync();

        var r = await (await dir.PostJsonAsync($"/api/Eventos/{evento.Id()}/cancelar", new { motivo = "Falta de quórum" })).JsonAsync();
        Assert.Equal("CANCELADO", r.Str("estado"));
        Assert.Equal(1, r.GetProperty("notificados").GetInt32());
        var aviso = Assert.Single(api.Correos.Para(email), c => c.Asunto == $"Actividad cancelada: {titulo}");
        Assert.Contains("Falta de quórum", aviso.Texto);

        await (await dir.PostAsync($"/api/Eventos/{evento.Id()}/cancelar", null)).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PutJsonAsync($"/api/Eventos/{evento.Id()}/reprogramar",
            new { fechaInicio = Http.Z(DateTime.UtcNow.AddDays(5)), fechaFin = (string?)null, lugar = (string?)null, motivo = (string?)null }))
            .EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostAsync($"/api/Eventos/{Guid.NewGuid()}/cancelar", null)).EsperarAsync(HttpStatusCode.NotFound);

        var (beto, _, _) = await api.NuevoParticipanteAsync();
        await (await beto.PostAsync($"/api/Eventos/{evento.Id()}/inscribir", null)).EsperarAsync(HttpStatusCode.BadRequest);
        var checkin = await (await (await api.EncargadoAsync()).PostJsonAsync($"/api/Eventos/{evento.Id()}/checkin",
            new { codigo = insc.Str("codigoQr") })).JsonAsync(HttpStatusCode.BadRequest);
        Assert.Contains("cancelado", checkin.Str("mensaje"));

        // Un evento cancelado no genera recordatorios
        await EjecutarTareaAsync(DateTime.UtcNow);
        Assert.Empty(Recordatorios(email, titulo));
        var publicos = await (await api.Anonimo().GetAsync("/api/Eventos")).JsonAsync();
        Assert.DoesNotContain(publicos.EnumerateArray(), e => e.Id() == evento.Id());
    }

    [Fact]
    public async Task No_se_cancela_un_evento_que_ya_se_realizo()
    {
        var evento = await EventoAsync("Evento pasado " + Guid.NewGuid().ToString("N")[..5], DateTime.UtcNow.AddDays(-2));
        await (await (await api.EncargadoAsync()).PostAsync($"/api/Eventos/{evento.Id()}/cancelar", null)).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reprogramar_y_cancelar_curso_avisa_a_inscritos_y_lista_de_espera()
    {
        var docente = await api.DocenteAsync();
        var inicio = DateOnly.FromDateTime(DateTime.Today.AddDays(10));
        var titulo = "Curso cambios " + Guid.NewGuid().ToString("N")[..5];
        var curso = await CursoAsync(docente, titulo, inicio, cupo: 1);
        var (ana, _, emailAna) = await api.NuevoParticipanteAsync("Ana");
        var (beto, _, emailBeto) = await api.NuevoParticipanteAsync("Beto");
        await (await ana.PostAsync($"/api/Cursos/{curso.Id()}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);
        await (await beto.PostAsync($"/api/Cursos/{curso.Id()}/lista-espera", null)).EsperarAsync(HttpStatusCode.OK);

        var url = $"/api/Cursos/{curso.Id()}/reprogramar";
        object Fechas(DateOnly ini, DateOnly fin) => new { fechaInicio = ini, fechaFin = fin, motivo = (string?)null };
        await (await docente.PutJsonAsync(url, Fechas(DateOnly.FromDateTime(DateTime.Today.AddDays(-3)), inicio))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await docente.PutJsonAsync(url, Fechas(inicio.AddDays(5), inicio))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await docente.PutJsonAsync(url, Fechas(inicio, inicio.AddDays(5)))).EsperarAsync(HttpStatusCode.BadRequest); // sin cambios
        await (await docente.PutJsonAsync($"/api/Cursos/{Guid.NewGuid()}/reprogramar", Fechas(inicio, inicio))).EsperarAsync(HttpStatusCode.NotFound);

        var r = await (await docente.PutJsonAsync(url, Fechas(inicio.AddDays(7), inicio.AddDays(12)))).JsonAsync();
        Assert.Equal(2, r.GetProperty("notificados").GetInt32());
        Assert.Single(api.Correos.Para(emailAna), c => c.Asunto == $"Cambio de fecha: {titulo}");
        Assert.Single(api.Correos.Para(emailBeto), c => c.Asunto == $"Cambio de fecha: {titulo}");

        var cancelado = await (await docente.PostJsonAsync($"/api/Cursos/{curso.Id()}/cancelar", new { motivo = "Sin docente" })).JsonAsync();
        Assert.Equal(2, cancelado.GetProperty("notificados").GetInt32());
        Assert.Single(api.Correos.Para(emailBeto), c => c.Asunto == $"Actividad cancelada: {titulo}");
        Assert.Empty((await (await beto.GetAsync("/api/Cursos/mi-lista-espera")).JsonAsync()).EnumerateArray());
        await (await docente.PostAsync($"/api/Cursos/{curso.Id()}/cancelar", null)).EsperarAsync(HttpStatusCode.BadRequest);
        var (caro, _, _) = await api.NuevoParticipanteAsync();
        await (await caro.PostAsync($"/api/Cursos/{curso.Id()}/inscribir", null)).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Docente_no_modifica_cursos_ajenos()
    {
        var inicio = DateOnly.FromDateTime(DateTime.Today.AddDays(10));
        var curso = await CursoAsync(await api.DirectoraAsync(), "Curso de la directora " + Guid.NewGuid().ToString("N")[..5], inicio);
        var docente = await api.DocenteAsync();
        await (await docente.PutJsonAsync($"/api/Cursos/{curso.Id()}/reprogramar",
            new { fechaInicio = inicio.AddDays(1), fechaFin = inicio.AddDays(2), motivo = (string?)null })).EsperarAsync(HttpStatusCode.Forbidden);
        await (await docente.PostAsync($"/api/Cursos/{curso.Id()}/cancelar", null)).EsperarAsync(HttpStatusCode.Forbidden);
        await (await docente.PostAsync($"/api/Cursos/{Guid.NewGuid()}/cancelar", null)).EsperarAsync(HttpStatusCode.NotFound);
    }
}
