using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// JAG-86 / FUJ-124 + evaluación: exportación con permisos, filtros combinados, "sin datos", Excel/PDF y registro (log)
[Collection(ColeccionApi.Nombre)]
public class ReportesTests(ApiFixture api)
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<JsonElement> CursoAsync(string titulo, bool deOtroDocente = false) =>
        await (await (await api.DirectoraAsync()).PostJsonAsync("/api/Cursos", new
        {
            titulo, descripcion = (string?)null, fechaInicio = Hoy.AddDays(-2), fechaFin = Hoy.AddDays(5), cupoMaximo = 30,
            asistenciaMinimaPct = 50, docenteId = deOtroDocente ? await api.OtroDocenteIdAsync() : await api.DocenteIdAsync()
        })).JsonAsync();

    private static List<string[]> Filas(JsonElement tabla) =>
        tabla.GetProperty("filas").EnumerateArray().Select(f => f.EnumerateArray().Select(c => c.GetString()!).ToArray()).ToList();

    [Fact]
    public async Task Inscritos_del_curso_con_filtros_combinados_pdf_y_excel()
    {
        var titulo = "Curso exportable " + Guid.NewGuid().ToString("N")[..5];
        var curso = await CursoAsync(titulo);
        var id = curso.Id();
        var docente = await api.DocenteAsync();
        var (ana, anaId, _) = await api.NuevoParticipanteAsync("Ana Exportada");
        var (beto, betoId, _) = await api.NuevoParticipanteAsync("Beto Exportado");
        var (caro, _, _) = await api.NuevoParticipanteAsync("Carolina Anaya");
        // Se inscriben antes de que empiece el curso; luego el curso empieza (hace 2 días)
        await api.MoverInicioDelCursoAsync(id, Hoy.AddDays(1));
        foreach (var c in new[] { ana, beto, caro })
            await (await c.PostAsync($"/api/Cursos/{id}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);
        await api.MoverInicioDelCursoAsync(id, Hoy.AddDays(-2));
        await (await docente.PostJsonAsync($"/api/Cursos/{id}/asistencia", new
        {
            fecha = Hoy, registros = new[] { new { participanteId = anaId, presente = true }, new { participanteId = betoId, presente = false } }
        })).EsperarAsync(HttpStatusCode.OK);

        var url = $"/api/Reportes/inscritos-curso?actividadId={id}";
        var todo = await (await docente.GetAsync(url + "&formato=json")).JsonAsync();
        Assert.Equal(3, Filas(todo).Count);
        Assert.Contains(Filas(todo), f => f[0] == "Ana Exportada" && f[5] == "100 %");
        Assert.Contains(Filas(todo), f => f[0] == "Beto Exportado" && f[5] == "0 %");

        // Combinaciones: texto + estado (al cancelar un curso la inscripción se elimina: solo hay confirmadas)
        var anas = Filas(await (await docente.GetAsync(url + "&formato=json&q=ana")).JsonAsync());
        Assert.Equal(["Ana Exportada", "Carolina Anaya"], anas.Select(f => f[0]));
        var anasConfirmadas = Filas(await (await docente.GetAsync(url + "&formato=json&q=ANA&estado=CONFIRMADA")).JsonAsync());
        Assert.Equal(["Ana Exportada", "Carolina Anaya"], anasConfirmadas.Select(f => f[0]));
        var exportados = Filas(await (await docente.GetAsync(url + "&formato=json&q=exportad&estado=confirmada")).JsonAsync());
        Assert.Equal(["Ana Exportada", "Beto Exportado"], exportados.Select(f => f[0]));
        await (await docente.GetAsync(url + "&formato=xlsx&estado=CANCELADA")).EsperarAsync(HttpStatusCode.UnprocessableEntity);
        var futuro = $"&desde={Hoy.AddDays(3):yyyy-MM-dd}";
        await (await docente.GetAsync(url + "&formato=pdf" + futuro)).EsperarAsync(HttpStatusCode.UnprocessableEntity); // sin datos
        await (await docente.GetAsync(url + $"&formato=json&desde={Hoy:yyyy-MM-dd}&hasta={Hoy.AddDays(-1):yyyy-MM-dd}"))
            .EsperarAsync(HttpStatusCode.BadRequest); // rango invertido

        var pdf = await docente.GetAsync(url + "&formato=pdf&estado=CONFIRMADA");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.Equal("no-store", pdf.Headers.CacheControl!.ToString());
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..4]));

        var xlsx = await docente.GetAsync(url + "&formato=xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        using var libro = new XLWorkbook(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync()));
        Assert.Contains(libro.Worksheet(1).RowsUsed(), r => r.Cell(1).GetString() == "Ana Exportada");

        // Cada exportación queda registrada con usuario, rol, filtros y filas; las vistas previas (json) no
        var docenteId = await api.DocenteIdAsync();
        var registro = Filas(await (await (await api.DirectoraAsync()).GetAsync(
            $"/api/Reportes/exportaciones?formato=json&usuarioId={docenteId}&q=inscritos-curso")).JsonAsync());
        Assert.Contains(registro, f => f[4] == "PDF" && f[5].Replace(" ", "").Contains("\"Estado\":\"CONFIRMADA\"") && f[6] == "3" && f[7] == "Generado");
        Assert.Contains(registro, f => f[4] == "XLSX" && f[6] == "3");
        await (await docente.GetAsync("/api/Reportes/exportaciones?formato=json")).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Permisos_se_verifican_antes_de_exportar()
    {
        var ajeno = await CursoAsync("Curso ajeno " + Guid.NewGuid().ToString("N")[..5], deOtroDocente: true);
        var docente = await api.DocenteAsync();
        var (participante, _, _) = await api.NuevoParticipanteAsync();

        await (await docente.GetAsync($"/api/Reportes/inscritos-curso?actividadId={ajeno.Id()}&formato=pdf")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await participante.GetAsync($"/api/Reportes/inscritos-curso?actividadId={ajeno.Id()}&formato=pdf")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await docente.GetAsync("/api/Reportes/horas-voluntariado?formato=xlsx")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await api.Anonimo().GetAsync("/api/Reportes/horas-voluntariado?formato=xlsx")).EsperarAsync(HttpStatusCode.Unauthorized);
        await (await docente.GetAsync("/api/Reportes/no-existe?formato=pdf")).EsperarAsync(HttpStatusCode.NotFound);
        await (await docente.GetAsync("/api/Reportes/inscritos-curso?formato=pdf")).EsperarAsync(HttpStatusCode.BadRequest); // falta el curso
        await (await docente.GetAsync("/api/Reportes/inscritos-curso?formato=doc")).EsperarAsync(HttpStatusCode.BadRequest);
        // Curso sin inscritos: aviso en lugar de archivo vacío
        await (await (await api.DirectoraAsync()).GetAsync($"/api/Reportes/inscritos-curso?actividadId={ajeno.Id()}&formato=xlsx"))
            .EsperarAsync(HttpStatusCode.UnprocessableEntity);

        var disponibles = await (await docente.GetAsync("/api/Reportes")).JsonAsync();
        var claves = disponibles.EnumerateArray().Select(r => r.Str("clave")).ToList();
        Assert.Contains("inscritos-curso", claves);
        Assert.DoesNotContain("asistencia-evento", claves);
        Assert.DoesNotContain("exportaciones", claves);
    }

    [Fact]
    public async Task CP18_asistencia_de_un_evento_con_20_participantes_en_excel()
    {
        var evento = await (await (await api.EncargadoAsync()).PostJsonAsync("/api/Eventos", new
        {
            titulo = "Feria CP-18 " + Guid.NewGuid().ToString("N")[..5], descripcion = (string?)null, lugar = "Plaza",
            fechaInicio = Http.Z(DateTime.UtcNow.AddHours(3)), fechaFin = Http.Z(DateTime.UtcNow.AddHours(5))
        })).JsonAsync();
        var id = evento.Id();
        for (var i = 0; i < 20; i++)
        {
            var (p, _, _) = await api.NuevoParticipanteAsync($"Asistente {i:00}");
            await (await p.PostAsync($"/api/Eventos/{id}/inscribir", null)).EsperarAsync(HttpStatusCode.OK);
        }
        // 15 ingresaron con su QR
        await api.EnBdAsync(async db =>
        {
            foreach (var insc in db.InscripcionesEvento.Where(x => x.EventoId == id).OrderBy(x => x.InscritoEn).Take(15))
            { insc.Estado = "ASISTIO"; insc.CheckinEn = DateTime.UtcNow; }
            await Task.CompletedTask;
        });

        var encargado = await api.EncargadoAsync();
        var r = await encargado.GetAsync($"/api/Reportes/asistencia-evento?actividadId={id}&formato=xlsx");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("20", r.Headers.GetValues("X-Filas-Exportadas").Single());
        using var libro = new XLWorkbook(new MemoryStream(await r.Content.ReadAsByteArrayAsync()));
        var hoja = libro.Worksheet(1);
        var encabezado = hoja.RowsUsed().First(x => x.Cell(1).GetString() == "Participante");
        Assert.Equal(["Participante", "Correo", "Inscripción", "Inscrito el", "Asistencia", "Hora de ingreso"],
            Enumerable.Range(1, 6).Select(c => encabezado.Cell(c).GetString()));
        Assert.Equal(20, hoja.RowsUsed().Count(x => x.Cell(1).GetString().StartsWith("Asistente ")));
        Assert.Equal(15, hoja.RowsUsed().Count(x => x.Cell(5).GetString() == "Asistió"));

        var asistieron = Filas(await (await encargado.GetAsync($"/api/Reportes/asistencia-evento?actividadId={id}&formato=json&estado=ASISTIO&q=asistente 1")).JsonAsync());
        Assert.All(asistieron, f => Assert.StartsWith("Asistente 1", f[0]));
        await (await (await api.DocenteAsync()).GetAsync($"/api/Reportes/asistencia-evento?actividadId={id}&formato=pdf")).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Horas_de_voluntariado_por_rango_de_fechas()
    {
        var marca = Guid.NewGuid().ToString("N")[..6];
        var v = new Voluntario { NombreCompleto = "Vol " + marca, Email = $"vol{marca}@correo.com" };
        await api.EnBdAsync(async db => { db.Voluntarios.Add(v); await Task.CompletedTask; });
        await api.EnBdAsync(async db =>
        {
            db.ParticipacionesVoluntario.AddRange(
                new ParticipacionVoluntario { VoluntarioId = v.Id, Descripcion = "Feria A", Horas = 3, Fecha = new DateTime(2026, 3, 10, 15, 0, 0, DateTimeKind.Utc) },
                new ParticipacionVoluntario { VoluntarioId = v.Id, Descripcion = "Feria B", Horas = 2.5m, Fecha = new DateTime(2026, 3, 20, 15, 0, 0, DateTimeKind.Utc) },
                new ParticipacionVoluntario { VoluntarioId = v.Id, Descripcion = "Feria C", Horas = 4, Fecha = new DateTime(2026, 5, 1, 15, 0, 0, DateTimeKind.Utc) });
            await Task.CompletedTask;
        });
        var coord = await api.EncargadoAsync();
        var marzo = Filas(await (await coord.GetAsync($"/api/Reportes/horas-voluntariado?formato=json&q={marca}&desde=2026-03-01&hasta=2026-03-31")).JsonAsync());
        var fila = Assert.Single(marzo);
        Assert.Equal("2", fila[3]);
        Assert.Equal("Feria A; Feria B", fila[4]);
        Assert.Equal("5 h 30 min", fila[5]);
        var todo = Filas(await (await coord.GetAsync($"/api/Reportes/horas-voluntariado?formato=json&q={marca}")).JsonAsync());
        Assert.Equal("9 h 30 min", Assert.Single(todo)[5]);
        await (await coord.GetAsync($"/api/Reportes/horas-voluntariado?formato=pdf&q={marca}&desde=2025-01-01&hasta=2025-12-31"))
            .EsperarAsync(HttpStatusCode.UnprocessableEntity);
    }
}

// Evaluación (logging): un error al dibujar el archivo queda registrado con el reporte, el usuario y los filtros
[Collection(ColeccionApi.Nombre)]
public class ReportesErrorRenderTests(ApiFixture api)
{
    private class ProveedorPrueba : FundacionJaguares.Application.Reportes.IProveedorReportes
    {
        public IEnumerable<FundacionJaguares.Application.Reportes.DefinicionReporte> Reportes() =>
        [
            new("prueba-render", "Prueba", ["DIRECTORA"], (_, _) => Task.FromResult(new FundacionJaguares.Application.Reportes.TablaReporte(
                "Prueba", null, [new("Columna")], [["valor"]], [])))
        ];
    }

    [Fact]
    public async Task Error_de_render_se_registra_y_se_informa()
    {
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FundacionJaguares.Infrastructure.AppDbContext>();
        var directoraId = await api.IdDeAsync(Usuarios.Directora);
        var servicio = new FundacionJaguares.Application.Reportes.ReporteService([new ProveedorPrueba()], db,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FundacionJaguares.Application.Reportes.ReporteService>.Instance,
            () => DateTime.UtcNow, (_, _, _) => throw new OutOfMemoryException("fuente no disponible"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.ExportarAsync("prueba-render",
            FundacionJaguares.Application.Reportes.FormatoReporte.PDF, new(directoraId, "DIRECTORA"), new(Q: "busqueda-render")));
        Assert.Contains("quedó registrado", ex.Message);

        var registro = db.Exportaciones.Where(e => e.Reporte == "prueba-render").OrderByDescending(e => e.CreadoEn).First();
        Assert.False(registro.Exito);
        Assert.Contains("fuente no disponible", registro.Error);
        Assert.Contains("busqueda-render", registro.Filtros);
        Assert.Equal(directoraId, registro.UsuarioId);
        Assert.Equal(1, registro.Filas);
    }
}
