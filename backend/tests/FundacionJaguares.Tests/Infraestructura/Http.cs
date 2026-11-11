using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FundacionJaguares.Tests.Infraestructura;

// Atajos para leer respuestas JSON en las pruebas
public static class Http
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage r, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        var cuerpo = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == esperado, $"Se esperaba {(int)esperado} y llegó {(int)r.StatusCode}: {cuerpo}");
        return cuerpo.Length == 0 ? default : JsonDocument.Parse(cuerpo).RootElement.Clone();
    }

    public static async Task<T> ComoAsync<T>(this HttpResponseMessage r, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        var cuerpo = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == esperado, $"Se esperaba {(int)esperado} y llegó {(int)r.StatusCode}: {cuerpo}");
        return JsonSerializer.Deserialize<T>(cuerpo, ApiFixture.Json)!;
    }

    public static async Task EsperarAsync(this HttpResponseMessage r, HttpStatusCode esperado)
    {
        var cuerpo = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == esperado, $"Se esperaba {(int)esperado} y llegó {(int)r.StatusCode}: {cuerpo}");
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient c, string url, object? cuerpo) =>
        c.PostAsJsonAsync(url, cuerpo, ApiFixture.Json);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient c, string url, object? cuerpo) =>
        c.PutAsJsonAsync(url, cuerpo, ApiFixture.Json);

    public static string Str(this JsonElement e, string prop) => e.GetProperty(prop).GetString()!;
    public static Guid Id(this JsonElement e, string prop = "id") => e.GetProperty(prop).GetGuid();

    // Fechas en UTC con "Z", igual que envía el frontend (toISOString)
    public static string Z(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
}
