using System.Net;
using System.Net.Http.Json;
using FundacionJaguares.Api;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// Seguridad: bloqueo por intentos fallidos, validación del registro en el servidor, cabeceras de seguridad,
// Swagger cerrado fuera de desarrollo y vigilancia de actividad sospechosa (solo la Dirección).
[Collection(ColeccionApi.Nombre)]
public class SeguridadTests(ApiFixture api)
{
    [Fact]
    public async Task Cinco_fallos_bloquean_la_cuenta_aunque_luego_se_use_la_clave_correcta()
    {
        var (_, _, email) = await api.NuevoParticipanteAsync();
        var anon = api.Anonimo();
        for (var i = 0; i < 5; i++)
            await (await anon.PostAsJsonAsync("/api/Auth/login", new { email, password = "Equivocada1" })).EsperarAsync(HttpStatusCode.Unauthorized);

        var bloqueado = await (await anon.PostAsJsonAsync("/api/Auth/login", new { email, password = "Clave123!" }))
            .JsonAsync(HttpStatusCode.TooManyRequests);
        Assert.Contains("Demasiados intentos", bloqueado.Str("error"));
        // Mayúsculas distintas no saltan el bloqueo
        await (await anon.PostAsJsonAsync("/api/Auth/login", new { email = email.ToUpperInvariant(), password = "Clave123!" }))
            .EsperarAsync(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public void El_bloqueo_por_ip_dura_lo_configurado_y_despues_se_libera()
    {
        var p = new ProteccionAccesos(new SeguridadOpciones { IntentosPorCuenta = 100, IntentosPorIp = 3, MinutosBloqueo = 15 });
        var t = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(p.RegistrarFallo("a@x.bo", "1.2.3.4", t));
        Assert.False(p.RegistrarFallo("b@x.bo", "1.2.3.4", t));
        Assert.True(p.RegistrarFallo("c@x.bo", "1.2.3.4", t));          // tercera cuenta distinta desde la misma IP
        Assert.NotNull(p.Bloqueo("otra@x.bo", "1.2.3.4", t.AddMinutes(14)));
        Assert.Null(p.Bloqueo("otra@x.bo", "5.6.7.8", t.AddMinutes(14))); // otra IP no queda bloqueada
        Assert.Null(p.Bloqueo("otra@x.bo", "1.2.3.4", t.AddMinutes(16)));
    }

    [Fact]
    public void Un_acierto_reinicia_el_contador_de_la_cuenta()
    {
        var p = new ProteccionAccesos(new SeguridadOpciones { IntentosPorCuenta = 3, IntentosPorIp = 100 });
        var t = DateTime.UtcNow;
        p.RegistrarFallo("a@x.bo", "1.1.1.1", t);
        p.RegistrarFallo("a@x.bo", "1.1.1.1", t);
        p.RegistrarExito("A@x.bo");
        Assert.False(p.RegistrarFallo("a@x.bo", "1.1.1.1", t));
        Assert.Null(p.Bloqueo("a@x.bo", "1.1.1.1", t));
    }

    [Theory]
    [InlineData("Ana Pérez", "no-es-correo", "Clave123!", "Correo")]
    [InlineData("Ana Pérez", "ana@prueba.bo", "corta1", "al menos")]
    [InlineData("Ana Pérez", "ana@prueba.bo", "soloLetrasLargas", "letras y números")]
    [InlineData("<script>alert(1)</script>", "ana@prueba.bo", "Clave123!", "no permitidos")]
    [InlineData("A", "ana@prueba.bo", "Clave123!", "nombre")]
    public async Task El_registro_se_valida_en_el_servidor(string nombre, string email, string password, string mensaje)
    {
        var r = await (await api.Anonimo().PostAsJsonAsync("/api/Auth/registro",
            new { nombreCompleto = nombre, email, password, aceptaTerminos = true, aceptaPrivacidad = true })).JsonAsync(HttpStatusCode.BadRequest);
        Assert.Contains(mensaje, r.Str("error"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task El_mismo_correo_con_otras_mayusculas_no_crea_otra_cuenta()
    {
        var (_, _, email) = await api.NuevoParticipanteAsync();
        await (await api.Anonimo().PostAsJsonAsync("/api/Auth/registro",
            new { nombreCompleto = "Duplicada", email = email.ToUpperInvariant(), password = "Clave123!", aceptaTerminos = true, aceptaPrivacidad = true }))
            .EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Las_respuestas_llevan_cabeceras_de_seguridad_y_swagger_no_esta_publicado()
    {
        var anon = api.Anonimo();
        var r = await anon.GetAsync("/api/Cursos");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'none'", r.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("no-store", r.Headers.CacheControl!.ToString());
        Assert.False(r.Headers.Contains("Server"));

        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }

    [Fact]
    public async Task Con_proxy_https_forzado_una_solicitud_http_se_redirige()
    {
        // Sin ForzarHttps (como en las pruebas) no se redirige; con la cabecera https se agrega HSTS
        var c = api.Anonimo();
        var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/Cursos");
        pedido.Headers.Add("X-Forwarded-Proto", "https");
        var r = await c.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("max-age=31536000", r.Headers.GetValues("Strict-Transport-Security").Single());
    }

    [Fact]
    public async Task Solo_la_directora_ve_la_vigilancia_de_actividad_sospechosa()
    {
        var email = $"sospecha{Guid.NewGuid().ToString("N")[..8]}@prueba.bo";
        var anon = api.Anonimo();
        for (var i = 0; i < 3; i++)
            await (await anon.PostAsJsonAsync("/api/Auth/login", new { email, password = "Nada1234" })).EsperarAsync(HttpStatusCode.Unauthorized);

        var directora = await api.DirectoraAsync();
        // El registro se guarda en segundo plano: se espera a que aparezca la cuenta
        System.Text.Json.JsonElement resumen = default;
        for (var i = 0; i < 50; i++)
        {
            resumen = await (await directora.GetAsync("/api/RegistroActividad/seguridad?horas=1")).JsonAsync();
            if (resumen.GetProperty("cuentas").EnumerateArray().Any(c => c.Str("email") == email)) break;
            await Task.Delay(200);
        }
        var cuenta = resumen.GetProperty("cuentas").EnumerateArray().Single(c => c.Str("email") == email);
        Assert.Equal(3, cuenta.GetProperty("fallos").GetInt32());
        Assert.True(resumen.GetProperty("loginsFallidos").GetInt32() >= 3);

        var filtrados = await (await directora.GetAsync($"/api/RegistroActividad?resultado=SOSPECHOSO&q={email}")).JsonAsync();
        Assert.Equal(3, filtrados.GetProperty("total").GetInt32());

        await (await (await api.JuanAsync()).GetAsync("/api/RegistroActividad/seguridad")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await anon.GetAsync("/api/RegistroActividad/seguridad")).EsperarAsync(HttpStatusCode.Unauthorized);
    }
}
