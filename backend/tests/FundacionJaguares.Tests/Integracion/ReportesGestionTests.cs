using System.Net;
using System.Text.Json;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// Evaluación: cada pantalla de gestión exporta con sus filtros combinados y respeta lo que cada rol puede ver
[Collection(ColeccionApi.Nombre)]
public class ReportesGestionTests(ApiFixture api)
{
    private static List<string[]> Filas(JsonElement t) =>
        t.GetProperty("filas").EnumerateArray().Select(f => f.EnumerateArray().Select(c => c.GetString()!).ToArray()).ToList();

    [Fact]
    public async Task Cursos_el_docente_exporta_solo_los_suyos_y_filtra_por_texto_estado_y_fecha()
    {
        var marca = Guid.NewGuid().ToString("N")[..6];
        var dir = await api.DirectoraAsync();
        async Task Crear(string titulo, Guid docenteId, int dias) =>
            await (await dir.PostJsonAsync("/api/Cursos", new
            {
                titulo, descripcion = (string?)null, fechaInicio = DateOnly.FromDateTime(DateTime.Today.AddDays(dias)),
                fechaFin = DateOnly.FromDateTime(DateTime.Today.AddDays(dias + 5)), cupoMaximo = 10, asistenciaMinimaPct = 80, docenteId
            })).EsperarAsync(HttpStatusCode.OK);
        await Crear($"Pintura {marca}", await api.DocenteIdAsync(), 10);
        await Crear($"Música {marca}", await api.DocenteIdAsync(), 40);
        await Crear($"Ajeno {marca}", await api.OtroDocenteIdAsync(), 10);

        var docente = await api.DocenteAsync();
        var mios = Filas(await (await docente.GetAsync($"/api/Reportes/cursos?formato=json&q={marca}")).JsonAsync());
        Assert.Equal(2, mios.Count);
        Assert.DoesNotContain(mios, f => f[0].StartsWith("Ajeno"));
        var todos = Filas(await (await dir.GetAsync($"/api/Reportes/cursos?formato=json&q={marca}")).JsonAsync());
        Assert.Equal(3, todos.Count);

        var hasta = DateTime.Today.AddDays(20).ToString("yyyy-MM-dd");
        var pronto = Filas(await (await dir.GetAsync($"/api/Reportes/cursos?formato=json&q={marca}&estado=PUBLICADO&hasta={hasta}")).JsonAsync());
        Assert.Equal([$"Ajeno {marca}", $"Pintura {marca}"], pronto.Select(f => f[0]).Order());
        Assert.Contains(pronto, f => f[1] == "Otro Docente");
        await (await docente.GetAsync($"/api/Reportes/cursos?formato=pdf&q={marca}&estado=FINALIZADO")).EsperarAsync(HttpStatusCode.UnprocessableEntity);
        await (await docente.GetAsync($"/api/Reportes/eventos?formato=pdf")).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Eventos_voluntarios_asignaciones_y_encuestas_se_exportan_con_permisos()
    {
        var encargado = await api.EncargadoAsync();
        var marca = Guid.NewGuid().ToString("N")[..6];
        await (await encargado.PostJsonAsync("/api/Eventos", new
        {
            titulo = $"Feria {marca}", descripcion = (string?)null, lugar = "Plaza Murillo",
            fechaInicio = Http.Z(DateTime.UtcNow.AddDays(3)), fechaFin = Http.Z(DateTime.UtcNow.AddDays(3).AddHours(2))
        })).EsperarAsync(HttpStatusCode.OK);
        var ev = Filas(await (await encargado.GetAsync($"/api/Reportes/eventos?formato=json&q=murillo {marca}")).JsonAsync());
        Assert.Empty(ev); // el texto busca la frase completa en un mismo campo
        ev = Filas(await (await encargado.GetAsync($"/api/Reportes/eventos?formato=json&q={marca}&estado=PUBLICADO")).JsonAsync());
        Assert.Equal("Plaza Murillo", Assert.Single(ev)[3]);
        var xlsx = await encargado.GetAsync($"/api/Reportes/eventos?formato=xlsx&q={marca}");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);

        foreach (var clave in new[] { "voluntarios", "asignaciones-voluntariado", "encuestas" })
            await (await encargado.GetAsync($"/api/Reportes/{clave}?formato=json")).EsperarAsync(HttpStatusCode.OK);
        await (await (await api.DocenteAsync()).GetAsync("/api/Reportes/voluntarios?formato=json")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await encargado.GetAsync("/api/Reportes/encuesta-resultados?formato=json")).EsperarAsync(HttpStatusCode.BadRequest);
        await (await encargado.GetAsync($"/api/Reportes/encuesta-resultados?formato=json&actividadId={Guid.NewGuid()}")).EsperarAsync(HttpStatusCode.Forbidden);
    }
}
