using System.Net;
using System.Text.Json;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// Evaluación: donaciones y campañas exportables con filtros combinados y permisos por rol
[Collection(ColeccionApi.Nombre)]
public class ReportesFinanzasTests(ApiFixture api)
{
    private static List<string[]> Filas(JsonElement t) =>
        t.GetProperty("filas").EnumerateArray().Select(f => f.EnumerateArray().Select(c => c.GetString()!).ToArray()).ToList();

    [Fact]
    public async Task Donaciones_con_texto_estado_medio_y_fechas_y_el_encargado_solo_las_suyas()
    {
        var marca = "REF" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var (contador, _, _) = await api.PersonalAsync("CONTADOR");
        var encargado = await api.EncargadoAsync();
        async Task<JsonElement> Registrar(HttpClient c, decimal monto, string medio, string sufijo) =>
            await (await c.PostJsonAsync("/api/Donaciones", new { monto, medio, referencia = $"{marca}-{sufijo}", donanteId = (Guid?)null })).JsonAsync();
        var a = await Registrar(contador, 100, "EFECTIVO", "A");
        await Registrar(contador, 200, "TRANSFERENCIA", "B");
        await Registrar(encargado, 50, "EFECTIVO", "C");
        await (await contador.PostJsonAsync($"/api/Donaciones/{a.Id()}/anular", new { motivo = "Duplicada en caja" })).EsperarAsync(HttpStatusCode.OK);

        var todas = Filas(await (await contador.GetAsync($"/api/Reportes/donaciones?formato=json&q={marca}")).JsonAsync());
        Assert.Equal(3, todas.Count);
        var efectivoConfirmado = Filas(await (await contador.GetAsync($"/api/Reportes/donaciones?formato=json&q={marca}&estado=CONFIRMADA&accion=EFECTIVO")).JsonAsync());
        Assert.Equal($"{marca}-C", Assert.Single(efectivoConfirmado)[4]);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4)).ToString("yyyy-MM-dd");
        Assert.Equal(3, Filas(await (await contador.GetAsync($"/api/Reportes/donaciones?formato=json&q={marca}&desde={hoy}&hasta={hoy}")).JsonAsync()).Count);

        var t = await (await contador.GetAsync($"/api/Reportes/donaciones?formato=json&q={marca}")).JsonAsync();
        Assert.Contains(t.GetProperty("totales").EnumerateArray(), x => x.GetProperty("valor").GetString() == "Bs. 250,00");

        var propias = Filas(await (await encargado.GetAsync($"/api/Reportes/donaciones?formato=json&q={marca}")).JsonAsync());
        Assert.Equal($"{marca}-C", Assert.Single(propias)[4]);
        await (await (await api.DocenteAsync()).GetAsync("/api/Reportes/donaciones?formato=pdf")).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Campanas_vigentes_y_cerradas()
    {
        var (contador, _, _) = await api.PersonalAsync("CONTADOR");
        var nombre = "Campaña " + Guid.NewGuid().ToString("N")[..6];
        await (await contador.PostJsonAsync("/api/Campanas", new
        {
            nombre, descripcion = "Útiles escolares", meta = 1000, fechaLimite = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)), activa = true
        })).EsperarAsync(HttpStatusCode.OK);
        var vigentes = Filas(await (await contador.GetAsync($"/api/Reportes/campanas?formato=json&q={nombre}&estado=VIGENTE")).JsonAsync());
        Assert.Equal("Vigente", Assert.Single(vigentes)[2]);
        await (await contador.GetAsync($"/api/Reportes/campanas?formato=xlsx&q={nombre}&estado=CERRADA")).EsperarAsync(HttpStatusCode.UnprocessableEntity);
        await (await (await api.EncargadoAsync()).GetAsync("/api/Reportes/campanas?formato=json")).EsperarAsync(HttpStatusCode.Forbidden);
    }
}
