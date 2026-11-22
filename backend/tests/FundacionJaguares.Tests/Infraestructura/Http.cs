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

    // PDF mínimo que pasa la verificación de firma ("%PDF"): hace de comprobante del banco en las pruebas
    public static readonly byte[] PdfDePrueba = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 1 0 obj<<>>endobj trailer<<>> %%EOF");

    // Donación en línea (formulario multipart con el comprobante de pago). referencia null = una única al azar.
    public static Task<HttpResponseMessage> DonarEnLineaAsync(this HttpClient c, decimal monto, string medio = "QR",
        string? nombre = "Ana", string? email = "ana@correo.com", string? referencia = null, Guid? campanaId = null,
        bool aceptar = true, bool conArchivo = true, byte[]? archivo = null, string nombreArchivo = "comprobante.pdf")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(monto.ToString(System.Globalization.CultureInfo.InvariantCulture)), "monto" },
            { new StringContent(medio), "medio" },
            { new StringContent(referencia ?? $"OP-{Guid.NewGuid().ToString("N")[..12]}"), "referencia" }
        };
        if (nombre is not null) form.Add(new StringContent(nombre), "donanteNombre");
        if (email is not null) form.Add(new StringContent(email), "donanteEmail");
        if (campanaId is { } cid) form.Add(new StringContent(cid.ToString()), "campanaId");
        if (aceptar) { form.Add(new StringContent("true"), "aceptaTerminos"); form.Add(new StringContent("true"), "aceptaPrivacidad"); }
        if (conArchivo) form.Add(new ByteArrayContent(archivo ?? PdfDePrueba), "comprobantePago", nombreArchivo);
        return c.PostAsync("/api/Donaciones/en-linea", form);
    }

    // Contabilidad o Dirección confirma (o rechaza) en el banco una donación en línea
    public static async Task<JsonElement> VerificarDonacionAsync(this HttpClient c, Guid id, bool confirmar = true, string? motivo = null) =>
        await (await c.PostJsonAsync($"/api/Donaciones/{id}/verificar", new { confirmar, motivo })).JsonAsync();

    public static string Str(this JsonElement e, string prop) => e.GetProperty(prop).GetString()!;
    public static Guid Id(this JsonElement e, string prop = "id") => e.GetProperty(prop).GetGuid();

    // Fechas en UTC con "Z", igual que envía el frontend (toISOString)
    public static string Z(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
}
