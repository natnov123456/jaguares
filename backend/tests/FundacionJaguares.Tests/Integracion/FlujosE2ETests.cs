using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Tests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace FundacionJaguares.Tests.Integracion;

// JAG-75: pruebas de regresión de punta a punta. Cada prueba recorre un flujo completo como lo haría la Fundación,
// usando solo la API (y la tarea programada de recordatorios). Donde el flujo necesita que pase el tiempo
// (llega el día de la clase o empieza el evento) se adelantan las fechas en la base de datos.
[Collection(ColeccionApi.Nombre)]
public class FlujosE2ETests(ApiFixture api)
{
    private async Task EjecutarRecordatoriosAsync(DateTime ahoraUtc)
    {
        using var scope = api.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRecordatorioService>().EnviarPendientesAsync(ahoraUtc);
    }

    private async Task ResponderEncuestaAsync(HttpClient participante, string actividad, int puntaje)
    {
        var pendientes = await (await participante.GetAsync("/api/Encuestas/mis-pendientes")).JsonAsync();
        var token = pendientes.EnumerateArray().Single(p => p.Str("actividad") == actividad).Str("token");
        var encuesta = await (await api.Anonimo().GetAsync($"/api/Encuestas/responder/{token}")).JsonAsync();
        var respuestas = encuesta.GetProperty("preguntas").EnumerateArray().Select(p => new
        {
            preguntaId = p.Id(), valor = p.Str("tipo") == "ESCALA" ? puntaje : (int?)null, texto = p.Str("tipo") == "TEXTO" ? "Excelente actividad" : null
        });
        await (await api.Anonimo().PostJsonAsync($"/api/Encuestas/responder/{token}", new { respuestas })).EsperarAsync(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Curso_inscripcion_recordatorio_asistencia_certificado_y_encuesta()
    {
        var titulo = "E2E Curso " + Guid.NewGuid().ToString("N")[..5];
        var hoyBo = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4));
        var inicio = hoyBo.AddDays(1);
        var dir = await api.DirectoraAsync();
        var docente = await api.DocenteAsync();

        // 1) La Directora crea el curso y lo asigna al docente
        var curso = await (await dir.PostJsonAsync("/api/Cursos", new
        {
            titulo, descripcion = "Flujo completo", fechaInicio = inicio, fechaFin = inicio.AddDays(4), cupoMaximo = 5,
            asistenciaMinimaPct = 50, docenteId = await api.DocenteIdAsync()
        })).JsonAsync();
        var id = curso.Id();

        // 2) Una persona nueva se registra e inscribe (CP-01) y recibe la confirmación (CP-10)
        var (ana, anaId, email) = await api.NuevoParticipanteAsync("Ana Flujo Completo");
        await (await ana.PostAsync($"/api/Cursos/{id}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);
        Assert.Contains(api.Correos.Para(email), c => c.Asunto == "Inscripción confirmada");
        var publico = await (await api.Anonimo().GetAsync("/api/Cursos")).JsonAsync();
        Assert.Equal(4, publico.EnumerateArray().Single(c => c.Id() == id).GetProperty("cupoDisponible").GetInt32());

        // 3) 24 h antes recibe el recordatorio (FUJ-119)
        await EjecutarRecordatoriosAsync(RecordatorioService.InicioCursoUtc(inicio).AddHours(-23));
        Assert.Single(api.Correos.Para(email), c => c.Asunto == $"Recordatorio: {titulo} es mañana");

        // 4) Llega el día de la clase: el docente pasa lista y Ana ve su porcentaje
        await api.EnBdAsync(async db =>
        {
            var c = await db.Cursos.FindAsync(id);
            c!.FechaInicio = hoyBo.AddDays(-1);
            c.FechaFin = hoyBo.AddDays(3);
        });
        await (await docente.PostJsonAsync($"/api/Cursos/{id}/asistencia", new
        {
            fecha = hoyBo, registros = new[] { new { participanteId = anaId, presente = true } }
        })).EsperarAsync(HttpStatusCode.OK);
        var mia = await (await ana.GetAsync($"/api/Cursos/{id}/mi-asistencia")).JsonAsync();
        Assert.Equal(100, mia.GetProperty("porcentaje").GetDouble());

        // 5) El docente finaliza: se emite el certificado (CP-03) y se verifica en público (CP-04)
        var fin = await (await docente.PostAsync($"/api/Cursos/{id}/finalizar", null)).JsonAsync();
        Assert.Equal(1, fin.GetProperty("certificados_generados").GetInt32());
        var certs = await (await ana.GetAsync("/api/Certificados/mios")).JsonAsync();
        var codigo = certs.EnumerateArray().Single(c => c.Str("nombreCurso") == titulo).Str("codigoUnico");
        var verificado = await (await api.Anonimo().GetAsync($"/api/Certificados/verificar/{codigo}")).JsonAsync();
        Assert.Equal("Ana Flujo Completo", verificado.Str("nombreParticipante"));

        // 6) Ana responde la encuesta y el docente ve el resultado (FUJ-120)
        await ResponderEncuestaAsync(ana, titulo, 5);
        var encuestas = await (await docente.GetAsync("/api/Encuestas")).JsonAsync();
        var resumen = encuestas.EnumerateArray().Single(e => e.Str("actividad") == titulo);
        Assert.Equal(1, resumen.GetProperty("respuestas").GetInt32());
        Assert.Equal(5, resumen.GetProperty("promedioSatisfaccion").GetDecimal());

        // 7) El reporte del curso refleja todo el flujo
        var reporte = await (await docente.GetAsync($"/api/Reportes/inscritos-curso?actividadId={id}&formato=json")).JsonAsync();
        Assert.Equal("100 %", reporte.GetProperty("filas")[0][5].GetString());
    }

    [Fact]
    public async Task Evento_inscripcion_QR_recordatorio_checkin_cierre_y_encuesta()
    {
        var titulo = "E2E Evento " + Guid.NewGuid().ToString("N")[..5];
        var enc = await api.EncargadoAsync();
        var evento = await (await enc.PostJsonAsync("/api/Eventos", new
        {
            titulo, descripcion = (string?)null, lugar = "Plaza Murillo",
            fechaInicio = Http.Z(DateTime.UtcNow.AddHours(2)), fechaFin = Http.Z(DateTime.UtcNow.AddHours(4))
        })).JsonAsync();
        var id = evento.Id();

        // Inscripción con QR único (FUJ-113)
        var (beto, _, email) = await api.NuevoParticipanteAsync("Beto Flujo Evento");
        var insc = await (await beto.PostAsync($"/api/Eventos/{id}/inscribir", null)).JsonAsync();
        var codigo = insc.Str("codigoQr");

        // Recordatorio con su QR adjunto
        await EjecutarRecordatoriosAsync(DateTime.UtcNow);
        var recordatorio = Assert.Single(api.Correos.Para(email), c => c.Asunto == $"Recordatorio: {titulo} es mañana");
        Assert.NotNull(recordatorio.Adjunto);

        // El día del evento el encargado escanea el QR: registra una sola vez (FUJ-113 / CP-02)
        var r1 = await (await enc.PostJsonAsync($"/api/Eventos/{id}/checkin", new { codigo = "JAGUARES-EV:" + codigo })).JsonAsync();
        Assert.Equal("REGISTRADO", r1.Str("resultado"));
        var r2 = await (await enc.PostJsonAsync($"/api/Eventos/{id}/checkin", new { codigo })).JsonAsync(HttpStatusCode.Conflict);
        Assert.Equal("YA_REGISTRADO", r2.Str("resultado"));

        // El evento ya empezó: el encargado cierra la asistencia y se envía la encuesta
        await api.EnBdAsync(async db => { (await db.Eventos.FindAsync(id))!.FechaInicio = DateTime.UtcNow.AddMinutes(-30); });
        var cierre = await (await enc.PostAsync($"/api/Eventos/{id}/cerrar-asistencia", null)).JsonAsync();
        Assert.Equal(1, cierre.GetProperty("asistentes").GetInt32());
        await ResponderEncuestaAsync(beto, titulo, 4);

        // La asistencia sale en el reporte exportable (FUJ-124)
        var xlsx = await enc.GetAsync($"/api/Reportes/asistencia-evento?actividadId={id}&formato=xlsx");
        using var libro = new XLWorkbook(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync()));
        Assert.Contains(libro.Worksheet(1).RowsUsed(), f => f.Cell(1).GetString() == "Beto Flujo Evento" && f.Cell(5).GetString() == "Asistió");
    }

    [Fact]
    public async Task Donacion_en_linea_comprobante_bitacora_reporte_mensual_y_transparencia()
    {
        var marca = Guid.NewGuid().ToString("N")[..6];
        var correo = $"donante.{marca}@correo.com";
        var hoy = DateTime.UtcNow;
        var (contador, _, _) = await api.PersonalAsync("CONTADOR", "Contadora Flujo");

        // 1) Donación en línea sin cuenta (CP-09)
        var d = await (await api.Anonimo().PostJsonAsync("/api/Donaciones/en-linea", new
        {
            monto = 345.5m, medio = "QR", referencia = $"E2E-{marca}", donanteId = (Guid?)null,
            donanteNombre = $"Donante Privado {marca}", donanteEmail = correo, aceptaTerminos = true, aceptaPrivacidad = true
        })).JsonAsync();
        var numero = d.GetProperty("numeroComprobante").GetInt64();

        // 2) Comprobante PDF por correo (FUJ-116)
        var comprobante = Assert.Single(api.Correos.Para(correo));
        Assert.Equal("application/pdf", comprobante.Adjunto!.TipoContenido);

        // 3) Queda en la bitácora de auditoría (FUJ-125)
        var bitacora = await (await contador.GetAsync($"/api/Auditoria/donaciones?q={numero}")).JsonAsync();
        Assert.Equal("ALTA", bitacora.GetProperty("registros")[0].Str("accion"));

        // 4) Aparece en el reporte financiero mensual (CP-06) y en su exportación
        var reporte = await (await contador.GetAsync($"/api/Donaciones/reporte/{hoy.Year}/{hoy.Month}")).JsonAsync();
        Assert.Contains(reporte.GetProperty("detalle").EnumerateArray(), x => x.GetProperty("numeroComprobante").GetInt64() == numero);
        var total = reporte.GetProperty("total").GetDecimal();
        var exportado = await (await contador.GetAsync($"/api/Reportes/donaciones?formato=json&q=E2E-{marca}")).JsonAsync();
        Assert.Equal("Bs. 345,50", exportado.GetProperty("filas")[0][7].GetString());

        // 5) Se publica la transparencia del mes (FUJ-121) y el sitio muestra totales sin datos personales
        await (await contador.PutJsonAsync($"/api/Transparencia/{hoy.Year}/{hoy.Month}", new
        {
            destinos = new[] { new { categoria = "Cursos", monto = Math.Min(total, 100m), descripcion = (string?)"Materiales" } },
            nota = "Gracias a todos los donantes"
        })).EsperarAsync(HttpStatusCode.OK);
        var publicoTexto = await (await api.Anonimo().GetAsync($"/api/Transparencia/{hoy.Year}/{hoy.Month}")).Content.ReadAsStringAsync();
        var publico = JsonDocument.Parse(publicoTexto).RootElement;
        Assert.Equal(total, publico.GetProperty("totalRecaudado").GetDecimal());
        Assert.DoesNotContain(correo, publicoTexto);
        Assert.DoesNotContain($"Donante Privado {marca}", publicoTexto);
        Assert.DoesNotContain($"E2E-{marca}", publicoTexto);
    }
}
