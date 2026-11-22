using System.Net;
using System.Text;
using FundacionJaguares.Tests.Infraestructura;
using SkiaSharp;

namespace FundacionJaguares.Tests.Integracion;

// Donación en línea por QR o transferencia: el donante sube el comprobante del banco y el número de operación;
// queda en verificación hasta que Contabilidad o la Dirección confirma el pago (o lo rechaza con motivo).
[Collection(ColeccionApi.Nombre)]
public class VerificacionDonacionesTests(ApiFixture api)
{
    private static string Correo() => $"don{Guid.NewGuid().ToString("N")[..10]}@correo.com";

    private static byte[] PngDePrueba()
    {
        using var bmp = new SKBitmap(40, 30);
        bmp.Erase(SKColors.SteelBlue);
        using var img = SKImage.FromBitmap(bmp);
        return img.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    [Fact]
    public async Task Pendiente_no_suma_y_al_confirmar_suma_y_llega_el_comprobante_con_texto()
    {
        var (contador, _, _) = await api.PersonalAsync("CONTADOR");
        var correo = Correo();
        var hoy = DateTime.UtcNow;
        var operacion = $"OP-{Guid.NewGuid().ToString("N")[..8]}";
        var d = await (await api.Anonimo().DonarEnLineaAsync(77.25m, "TRANSFERENCIA", "Rosa Prueba", correo, operacion)).JsonAsync();
        var id = d.Id();

        // En verificación: no aparece como confirmada en el reporte ni se puede descargar comprobante
        var reporte = await (await contador.GetAsync($"/api/Donaciones/reporte/{hoy.Year}/{hoy.Month}")).JsonAsync();
        Assert.DoesNotContain(reporte.GetProperty("detalle").EnumerateArray(),
            x => x.GetProperty("numeroComprobante").GetInt64() == d.GetProperty("numeroComprobante").GetInt64() && x.Str("estado") == "CONFIRMADA");
        var dir = await api.DirectoraAsync();
        await (await dir.GetAsync($"/api/Donaciones/{id}/comprobante")).EsperarAsync(HttpStatusCode.BadRequest);

        // Contabilidad ve el archivo que subió el donante, idéntico
        var archivo = await contador.GetAsync($"/api/Donaciones/{id}/comprobante-pago");
        Assert.Equal(HttpStatusCode.OK, archivo.StatusCode);
        Assert.Equal(Http.PdfDePrueba, await archivo.Content.ReadAsByteArrayAsync());

        var confirmada = await contador.VerificarDonacionAsync(id);
        Assert.Equal("CONFIRMADA", confirmada.Str("estado"));
        await (await contador.PostJsonAsync($"/api/Donaciones/{id}/verificar", new { confirmar = true })).EsperarAsync(HttpStatusCode.BadRequest); // ya verificada
        Assert.DoesNotContain((await (await contador.GetAsync("/api/Donaciones/por-verificar")).JsonAsync()).EnumerateArray(), x => x.Id() == id);

        // Comprobante de donación por correo y descargable, con texto (fuentes incrustadas)
        var correoComprobante = api.Correos.Para(correo).Single(c => c.Adjunto is not null);
        var pdf = Encoding.Latin1.GetString(correoComprobante.Adjunto!.Contenido);
        Assert.Contains("/FontFile", pdf);
        var descargado = await dir.GetAsync($"/api/Donaciones/{id}/comprobante");
        Assert.Equal(HttpStatusCode.OK, descargado.StatusCode);
        Assert.True((await descargado.Content.ReadAsByteArrayAsync()).Length > 5000);
    }

    [Fact]
    public async Task El_rechazo_exige_motivo_avisa_al_donante_y_libera_el_numero_de_operacion()
    {
        var dir = await api.DirectoraAsync();
        var correo = Correo();
        var operacion = $"OP-{Guid.NewGuid().ToString("N")[..8]}";
        var d = await (await api.Anonimo().DonarEnLineaAsync(40, "QR", "Pedro Prueba", correo, operacion)).JsonAsync();

        // Mismo número de operación (aunque cambien mayúsculas) no se acepta dos veces
        var repetida = await (await api.Anonimo().DonarEnLineaAsync(40, "QR", "Otro", Correo(), operacion.ToLowerInvariant()))
            .JsonAsync(HttpStatusCode.BadRequest);
        Assert.Contains("ya fue registrado", repetida.Str("error"));

        await (await dir.PostJsonAsync($"/api/Donaciones/{d.Id()}/verificar", new { confirmar = false, motivo = "" })).EsperarAsync(HttpStatusCode.BadRequest);
        var rechazada = await dir.VerificarDonacionAsync(d.Id(), confirmar: false, motivo: "No llegó el depósito a la cuenta");
        Assert.Equal("RECHAZADA", rechazada.Str("estado"));
        var aviso = api.Correos.Para(correo).Last();
        Assert.Contains("No llegó el depósito", aviso.Texto);
        Assert.Null(aviso.Adjunto);
        await (await dir.GetAsync($"/api/Donaciones/{d.Id()}/comprobante")).EsperarAsync(HttpStatusCode.BadRequest);

        // Rechazada: el número de operación puede volver a usarse (p. ej. si el donante se equivocó de monto)
        await (await api.Anonimo().DonarEnLineaAsync(40, "QR", "Pedro Prueba", correo, operacion)).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Solo_contabilidad_y_direccion_ven_y_verifican_los_pagos()
    {
        var d = await (await api.Anonimo().DonarEnLineaAsync(15)).JsonAsync();
        var juan = await api.JuanAsync();
        var (encargado, _, _) = await api.PersonalAsync("ENCARGADO_EVENTOS");
        foreach (var c in new[] { juan, encargado })
        {
            await (await c.GetAsync("/api/Donaciones/por-verificar")).EsperarAsync(HttpStatusCode.Forbidden);
            await (await c.GetAsync($"/api/Donaciones/{d.Id()}/comprobante-pago")).EsperarAsync(HttpStatusCode.Forbidden);
            await (await c.PostJsonAsync($"/api/Donaciones/{d.Id()}/verificar", new { confirmar = true })).EsperarAsync(HttpStatusCode.Forbidden);
        }
        await (await api.Anonimo().GetAsync($"/api/Donaciones/{d.Id()}/comprobante-pago")).EsperarAsync(HttpStatusCode.Unauthorized);
        // Una pendiente no se anula: se confirma o se rechaza
        await (await (await api.DirectoraAsync()).PostJsonAsync($"/api/Donaciones/{d.Id()}/anular", new { motivo = "Prueba de anulación" }))
            .EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task El_comprobante_del_banco_debe_ser_una_foto_o_un_pdf_reales()
    {
        var anon = api.Anonimo();
        await (await anon.DonarEnLineaAsync(10, archivo: Encoding.ASCII.GetBytes("MZ ejecutable"), nombreArchivo: "virus.exe")).EsperarAsync(HttpStatusCode.BadRequest);
        await (await anon.DonarEnLineaAsync(10, archivo: Encoding.ASCII.GetBytes("no soy imagen"), nombreArchivo: "foto.jpg")).EsperarAsync(HttpStatusCode.BadRequest);
        await (await anon.DonarEnLineaAsync(10, archivo: Encoding.ASCII.GetBytes("no soy pdf"), nombreArchivo: "banco.pdf")).EsperarAsync(HttpStatusCode.BadRequest);

        var d = await (await anon.DonarEnLineaAsync(10, archivo: PngDePrueba(), nombreArchivo: "captura.png")).JsonAsync();
        var r = await (await api.DirectoraAsync()).GetAsync($"/api/Donaciones/{d.Id()}/comprobante-pago");
        Assert.Equal("image/png", r.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task La_direccion_carga_la_cuenta_bancaria_y_el_qr_que_se_muestran_al_donar()
    {
        var dir = await api.DirectoraAsync();
        await (await dir.PutJsonAsync("/api/Institucional/datos-pago", new
        {
            bancoNombre = "Banco Unión", bancoTipoCuenta = "Caja de ahorro", bancoCuenta = "10000-123",
            bancoTitular = "Fundación Unión de los Jaguares", bancoDocumento = "NIT 123456"
        })).EsperarAsync(HttpStatusCode.OK);
        await (await dir.PutJsonAsync("/api/Institucional/datos-pago", new { bancoCuenta = "abc", bancoNombre = "B", bancoTitular = "T" }))
            .EsperarAsync(HttpStatusCode.BadRequest);
        await (await (await api.JuanAsync()).PutJsonAsync("/api/Institucional/datos-pago", new { bancoNombre = "X" })).EsperarAsync(HttpStatusCode.Forbidden);

        var qr = new MultipartFormDataContent { { new ByteArrayContent(PngDePrueba()), "archivo", "qr.png" } };
        await (await dir.PostAsync("/api/Institucional/qr-donacion", qr)).EsperarAsync(HttpStatusCode.OK);

        var datos = await (await api.Anonimo().GetAsync("/api/Institucional/datos-pago")).JsonAsync();
        Assert.True(datos.GetProperty("tieneQr").GetBoolean());
        Assert.Equal("10000-123", datos.Str("bancoCuenta"));
        var imagen = await api.Anonimo().GetAsync("/api/Institucional/qr-donacion");
        Assert.Equal("image/png", imagen.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task En_oficina_ya_no_se_registra_pasarela()
    {
        var dir = await api.DirectoraAsync();
        await (await dir.PostJsonAsync("/api/Donaciones", new { monto = 10, medio = "PASARELA", aceptaTerminos = true, aceptaPrivacidad = true }))
            .EsperarAsync(HttpStatusCode.BadRequest);
    }
}
