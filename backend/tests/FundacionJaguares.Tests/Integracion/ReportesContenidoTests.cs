using System.Net;
using System.Text.Json;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// Evaluación: noticias, galería y transparencia se exportan con filtros combinados y permisos
[Collection(ColeccionApi.Nombre)]
public class ReportesContenidoTests(ApiFixture api)
{
    private static List<string[]> Filas(JsonElement t) =>
        t.GetProperty("filas").EnumerateArray().Select(f => f.EnumerateArray().Select(c => c.GetString()!).ToArray()).ToList();

    [Fact]
    public async Task Noticias_publicadas_y_borradores_con_filtros()
    {
        var dir = await api.DirectoraAsync();
        var marca = Guid.NewGuid().ToString("N")[..6];
        foreach (var (titulo, publicada) in new[] { ($"Feria solidaria {marca}", true), ($"Borrador campaña {marca}", false) })
            await (await dir.PostJsonAsync("/api/Contenido/noticias", new
            {
                titulo, resumen = "Resumen", contenido = "Contenido de la noticia", imagenUrl = (string?)null, publicada
            })).EsperarAsync(HttpStatusCode.OK);

        var admin = await (await dir.GetAsync("/api/Contenido/noticias/admin")).JsonAsync();
        Assert.Equal(2, admin.EnumerateArray().Count(n => n.Str("titulo").EndsWith(marca)));
        var publicas = await (await api.Anonimo().GetAsync("/api/Contenido/noticias")).JsonAsync();
        Assert.DoesNotContain(publicas.EnumerateArray(), n => n.Str("titulo") == $"Borrador campaña {marca}");

        var borradores = Filas(await (await dir.GetAsync($"/api/Reportes/noticias?formato=json&q={marca}&estado=BORRADOR")).JsonAsync());
        Assert.Equal($"Borrador campaña {marca}", Assert.Single(borradores)[0]);
        Assert.Equal(2, Filas(await (await dir.GetAsync($"/api/Reportes/noticias?formato=json&q={marca}")).JsonAsync()).Count);
        await (await dir.GetAsync($"/api/Reportes/noticias?formato=pdf&q={marca}")).EsperarAsync(HttpStatusCode.OK);
        await (await (await api.EncargadoAsync()).GetAsync("/api/Contenido/noticias/admin")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await (await api.EncargadoAsync()).GetAsync("/api/Reportes/noticias?formato=pdf")).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Galeria_y_transparencia_se_exportan()
    {
        var dir = await api.DirectoraAsync();
        var marca = Guid.NewGuid().ToString("N")[..6];
        await (await dir.PostJsonAsync("/api/Galeria/albumes", new { titulo = $"Album {marca}", descripcion = "Fotos", publicado = false }))
            .EsperarAsync(HttpStatusCode.OK);
        var ocultos = Filas(await (await dir.GetAsync($"/api/Reportes/galeria?formato=json&q={marca}&estado=OCULTO")).JsonAsync());
        Assert.Equal("Oculto", Assert.Single(ocultos)[2]);
        await (await dir.GetAsync($"/api/Reportes/galeria?formato=xlsx&q={marca}&estado=PUBLICADO")).EsperarAsync(HttpStatusCode.UnprocessableEntity);

        var (contador, _, _) = await api.PersonalAsync("CONTADOR");
        await (await contador.GetAsync("/api/Reportes/transparencia?formato=json&desde=1990-01-01&hasta=1990-12-31")).EsperarAsync(HttpStatusCode.OK);
        await (await contador.GetAsync("/api/Reportes/transparencia?formato=pdf&desde=1990-01-01&hasta=1990-12-31")).EsperarAsync(HttpStatusCode.UnprocessableEntity);
        await (await contador.GetAsync("/api/Reportes/galeria?formato=pdf")).EsperarAsync(HttpStatusCode.Forbidden);
    }
}
