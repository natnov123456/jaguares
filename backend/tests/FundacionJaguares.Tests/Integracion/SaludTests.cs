using System.Net;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// JAG-28 / RNF-04: chequeos de salud que consulta el monitor de disponibilidad
[Collection(ColeccionApi.Nombre)]
public class SaludTests(ApiFixture api)
{
    [Fact]
    public async Task Live_y_ready_responden_sin_sesion_y_sin_datos_internos()
    {
        var live = await api.Anonimo().GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("Healthy", (await live.JsonAsync()).Str("estado"));

        var ready = await api.Anonimo().GetAsync("/health/ready");
        var json = await ready.JsonAsync();
        Assert.Equal("Healthy", json.Str("estado"));
        var chequeos = json.GetProperty("chequeos").EnumerateArray().ToDictionary(c => c.Str("nombre"), c => c.Str("estado"));
        Assert.Equal("Healthy", chequeos["base-de-datos"]);
        Assert.Equal("Healthy", chequeos["cache"]);
        Assert.Equal("no-store", ready.Headers.CacheControl!.ToString());

        var texto = await (await api.Anonimo().GetAsync("/health/ready")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Password", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=", texto, StringComparison.OrdinalIgnoreCase);
    }
}
