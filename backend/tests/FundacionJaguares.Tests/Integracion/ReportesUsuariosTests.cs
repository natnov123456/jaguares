using System.Net;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// JAG-83: el listado de usuarios se exporta con los mismos filtros de la pantalla, solo por la Dirección
[Collection(ColeccionApi.Nombre)]
public class ReportesUsuariosTests(ApiFixture api)
{
    [Fact]
    public async Task Usuarios_se_exportan_filtrando_por_texto_rol_y_estado()
    {
        var (_, id, email) = await api.PersonalAsync("COORDINADOR_VOLUNTARIADO", "Coordina Exportable");
        var dir = await api.DirectoraAsync();
        await (await dir.PutJsonAsync($"/api/Usuarios/{id}/activo", new { activo = false })).EsperarAsync(HttpStatusCode.OK);

        var t = await (await dir.GetAsync("/api/Reportes/usuarios?formato=json&q=exportable&rol=COORDINADOR_VOLUNTARIADO&estado=INACTIVO")).JsonAsync();
        var fila = Assert.Single(t.GetProperty("filas").EnumerateArray());
        Assert.Equal(email, fila[1].GetString());
        Assert.Equal("Coordinador/a de voluntariado", fila[3].GetString());
        Assert.Equal("Desactivada", fila[4].GetString());
        Assert.Contains(t.GetProperty("filtros").EnumerateArray(), x => x.GetProperty("valor").GetString() == "Coordinador/a de voluntariado");
        await (await dir.GetAsync("/api/Reportes/usuarios?formato=pdf&q=exportable&estado=ACTIVO")).EsperarAsync(HttpStatusCode.UnprocessableEntity);
        await (await dir.GetAsync("/api/Reportes/usuarios?formato=xlsx")).EsperarAsync(HttpStatusCode.OK);
        await (await (await api.DocenteAsync()).GetAsync("/api/Reportes/usuarios?formato=pdf")).EsperarAsync(HttpStatusCode.Forbidden);
    }
}
