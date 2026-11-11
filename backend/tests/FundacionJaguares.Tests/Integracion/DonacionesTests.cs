using System.Net;
using System.Text.Json;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// Donaciones (FUJ-105/106/109), comprobantes (FUJ-116), campañas (FUJ-117) y transparencia (FUJ-121)
[Collection(ColeccionApi.Nombre)]
public class DonacionesTests(ApiFixture api)
{
    private static object Donacion(decimal monto, string medio = "EFECTIVO", string? nombre = null, string? email = null,
        Guid? campanaId = null, string? referencia = null) =>
        new { monto, medio, referencia, donanteId = (Guid?)null, donanteNombre = nombre, donanteEmail = email, campanaId };

    [Fact]
    public async Task Registro_manual_valida_monto_medio_y_donante()
    {
        var dir = await api.DirectoraAsync();
        await (await dir.PostJsonAsync("/api/Donaciones", Donacion(0))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostJsonAsync("/api/Donaciones", Donacion(10, "CHEQUE"))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostJsonAsync("/api/Donaciones", Donacion(10, email: "solo@correo.com"))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostJsonAsync("/api/Donaciones", Donacion(10, nombre: "Ana", email: "malo"))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostJsonAsync("/api/Donaciones", Donacion(10, campanaId: Guid.NewGuid()))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostJsonAsync("/api/Donaciones", new { monto = 10, medio = "EFECTIVO", donanteId = Guid.NewGuid() }))
            .EsperarAsync(HttpStatusCode.BadRequest);

        var anonima = await (await dir.PostJsonAsync("/api/Donaciones", Donacion(25))).JsonAsync();
        Assert.Equal("CONFIRMADA", anonima.Str("estado"));
        await (await (await api.JuanAsync()).PostJsonAsync("/api/Donaciones", Donacion(25))).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Donacion_registrada_con_el_correo_de_un_usuario_queda_en_su_cuenta_y_genera_comprobante()
    {
        var (ana, _, email) = await api.NuevoParticipanteAsync("Ana Donante");
        var enc = await api.EncargadoAsync();
        var d = await (await enc.PostJsonAsync("/api/Donaciones", Donacion(80, "TRANSFERENCIA", "Ana Donante", email.ToUpperInvariant(), referencia: "TR-1"))).JsonAsync();

        var mias = await (await ana.GetAsync("/api/Donaciones/mias")).JsonAsync();
        Assert.Contains(mias.EnumerateArray(), m => m.Id() == d.Id());

        // Comprobante: la donante, quien la registró y la directora; nadie más
        foreach (var c in new[] { ana, enc, await api.DirectoraAsync() })
        {
            var pdf = await c.GetAsync($"/api/Donaciones/{d.Id()}/comprobante");
            await pdf.EsperarAsync(HttpStatusCode.OK);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        }
        var (otro, _, _) = await api.NuevoParticipanteAsync();
        await (await otro.GetAsync($"/api/Donaciones/{d.Id()}/comprobante")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await otro.GetAsync($"/api/Donaciones/{Guid.NewGuid()}/comprobante")).EsperarAsync(HttpStatusCode.NotFound);

        var registros = await (await enc.GetAsync("/api/Donaciones/mis-registros")).JsonAsync();
        Assert.Contains(registros.EnumerateArray(), r => r.Id() == d.Id());
    }

    [Fact]
    public async Task Donacion_en_linea_publica_y_con_sesion()
    {
        var anon = api.Anonimo();
        await (await anon.PostJsonAsync("/api/Donaciones/en-linea", Donacion(50, "EFECTIVO", "Ana", "ana@correo.com"))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await anon.PostJsonAsync("/api/Donaciones/en-linea", Donacion(50, "QR"))).EsperarAsync(HttpStatusCode.BadRequest);       // sin datos
        await (await anon.PostJsonAsync("/api/Donaciones/en-linea", Donacion(-5, "QR", "Ana", "ana@correo.com"))).EsperarAsync(HttpStatusCode.BadRequest);

        var publica = await (await anon.PostJsonAsync("/api/Donaciones/en-linea", Donacion(50, "QR", "Ana", "ana@correo.com"))).JsonAsync();
        Assert.StartsWith("DON-", publica.Str("referencia"));

        var (juan, _, _) = await api.NuevoParticipanteAsync();
        var conSesion = await (await juan.PostJsonAsync("/api/Donaciones/en-linea", new { monto = 30, medio = "", donanteId = (Guid?)null })).JsonAsync();
        Assert.Equal("PASARELA", conSesion.Str("medio"));
        var mias = await (await juan.GetAsync("/api/Donaciones/mias")).JsonAsync();
        Assert.Single(mias.EnumerateArray());
    }

    [Fact]
    public async Task Reporte_mensual_solo_para_la_directora()
    {
        var dir = await api.DirectoraAsync();
        await (await dir.PostJsonAsync("/api/Donaciones", Donacion(12.5m))).EsperarAsync(HttpStatusCode.OK);
        var hoy = DateTime.UtcNow;
        var rep = await (await dir.GetAsync($"/api/Donaciones/reporte/{hoy.Year}/{hoy.Month}")).JsonAsync();
        Assert.True(rep.GetProperty("total").GetDecimal() >= 12.5m);
        Assert.True(rep.GetProperty("cantidad").GetInt32() >= 1);
        await (await dir.GetAsync($"/api/Donaciones/reporte/{hoy.Year}/13")).EsperarAsync(HttpStatusCode.BadRequest);
        await (await (await api.EncargadoAsync()).GetAsync($"/api/Donaciones/reporte/{hoy.Year}/{hoy.Month}")).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Campanas_con_meta_progreso_y_vigencia()
    {
        var dir = await api.DirectoraAsync();
        var manana = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
        object Campana(string nombre, decimal meta, DateOnly limite, bool activa = true) =>
            new { nombre, descripcion = "Para materiales", meta, fechaLimite = limite, activa };

        await (await dir.PostJsonAsync("/api/Campanas", Campana("", 100, manana))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostJsonAsync("/api/Campanas", Campana("Sin meta", 0, manana))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostJsonAsync("/api/Campanas", Campana("Vencida", 100, DateOnly.FromDateTime(DateTime.Today.AddDays(-2))))).EsperarAsync(HttpStatusCode.BadRequest);

        var c = await (await dir.PostJsonAsync("/api/Campanas", Campana("Útiles escolares", 200, manana))).JsonAsync();
        await (await api.Anonimo().PostJsonAsync("/api/Donaciones/en-linea", Donacion(50, "QR", "Ana", "ana@correo.com", c.Id()))).EsperarAsync(HttpStatusCode.OK);

        var vigentes = await (await api.Anonimo().GetAsync("/api/Campanas")).JsonAsync();
        var conProgreso = vigentes.EnumerateArray().Single(x => x.Id() == c.Id());
        Assert.Equal(25m, conProgreso.GetProperty("porcentaje").GetDecimal());
        Assert.Equal(1, conProgreso.GetProperty("cantidadDonaciones").GetInt32());

        // Al desactivarla deja de mostrarse y no acepta más donaciones
        await (await dir.PutJsonAsync($"/api/Campanas/{c.Id()}", Campana("Útiles escolares", 200, manana, activa: false))).EsperarAsync(HttpStatusCode.OK);
        Assert.DoesNotContain((await (await api.Anonimo().GetAsync("/api/Campanas")).JsonAsync()).EnumerateArray(), x => x.Id() == c.Id());
        Assert.Contains((await (await dir.GetAsync("/api/Campanas/admin")).JsonAsync()).EnumerateArray(), x => x.Id() == c.Id());
        await (await api.Anonimo().PostJsonAsync("/api/Donaciones/en-linea", Donacion(50, "QR", "Ana", "ana@correo.com", c.Id()))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PutJsonAsync($"/api/Campanas/{Guid.NewGuid()}", Campana("X", 1, manana))).EsperarAsync(HttpStatusCode.NotFound);
        await (await dir.PutJsonAsync($"/api/Campanas/{c.Id()}", Campana("X", -1, manana))).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Transparencia_publica_solo_totales_sin_datos_personales()
    {
        var dir = await api.DirectoraAsync();
        await (await dir.PostJsonAsync("/api/Donaciones", Donacion(100, nombre: "Donante Secreto", email: "secreto@correo.com", referencia: "REF-PRIVADA"))).EsperarAsync(HttpStatusCode.OK);
        var hoy = DateTime.UtcNow;
        var url = $"/api/Transparencia/{hoy.Year}/{hoy.Month}";

        var previa = await (await dir.GetAsync($"/api/Transparencia/vista-previa/{hoy.Year}/{hoy.Month}")).JsonAsync();
        var total = previa.GetProperty("totalRecaudado").GetDecimal();
        Assert.True(total >= 100);

        object Destinos(params (string Cat, decimal Monto)[] d) =>
            new { destinos = d.Select(x => new { categoria = x.Cat, monto = x.Monto, descripcion = (string?)null }), nota = "Gracias" };

        await (await dir.PutJsonAsync(url, Destinos(("Cursos", total + 1)))).EsperarAsync(HttpStatusCode.BadRequest);         // excede
        await (await dir.PutJsonAsync(url, Destinos(("Cursos", 1), ("cursos", 1)))).EsperarAsync(HttpStatusCode.BadRequest);  // repetida
        await (await dir.PutJsonAsync(url, Destinos())).EsperarAsync(HttpStatusCode.BadRequest);                               // vacía
        await (await dir.PutJsonAsync(url, Destinos(("Cursos", -1)))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PutJsonAsync($"/api/Transparencia/{hoy.Year + 1}/1", Destinos(("A", 0)))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PutJsonAsync($"/api/Transparencia/{hoy.Year}/13", Destinos(("A", 0)))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await (await api.EncargadoAsync()).PutJsonAsync(url, Destinos(("A", 1)))).EsperarAsync(HttpStatusCode.Forbidden);

        var mitad = Math.Round(total / 2, 2);
        await (await dir.PutJsonAsync(url, Destinos(("Cursos", mitad), ("Eventos", 1)))).EsperarAsync(HttpStatusCode.OK);
        // Re-publicar reemplaza los destinos (regresión: antes daba 500)
        var republicado = await (await dir.PutJsonAsync(url, Destinos(("Eventos", mitad)))).JsonAsync();
        Assert.Single(republicado.GetProperty("destinos").EnumerateArray());

        var meses = await (await api.Anonimo().GetAsync("/api/Transparencia")).JsonAsync();
        Assert.Contains(meses.EnumerateArray(), m => m.GetProperty("mes").GetInt32() == hoy.Month);

        var publico = await api.Anonimo().GetAsync(url);
        var texto = await publico.Content.ReadAsStringAsync();
        await publico.EsperarAsync(HttpStatusCode.OK);
        Assert.DoesNotContain("Donante Secreto", texto);
        Assert.DoesNotContain("secreto@correo.com", texto);
        Assert.DoesNotContain("REF-PRIVADA", texto);

        await (await dir.DeleteAsync(url)).EsperarAsync(HttpStatusCode.NoContent);
        await (await api.Anonimo().GetAsync(url)).EsperarAsync(HttpStatusCode.NotFound);
        await (await dir.DeleteAsync(url)).EsperarAsync(HttpStatusCode.NotFound);
        await (await api.Anonimo().GetAsync($"/api/Transparencia/{hoy.Year}/0")).EsperarAsync(HttpStatusCode.BadRequest);
    }
}
