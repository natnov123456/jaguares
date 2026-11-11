using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Tests.Infraestructura;
using FundacionJaguares.Tests.Unitarias;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace FundacionJaguares.Tests.Integracion;

// Sitio público: noticias (FUJ-108), galería (FUJ-126), "Quiénes somos" y contacto (FUJ-127)
// y plantillas de certificado (FUJ-115)
[Collection(ColeccionApi.Nombre)]
public class ContenidoTests(ApiFixture api)
{
    private static MultipartFormDataContent Archivo(string campo, string nombre, byte[] datos, string tipo)
    {
        var form = new MultipartFormDataContent();
        var contenido = new ByteArrayContent(datos);
        contenido.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        form.Add(contenido, campo, nombre);
        return form;
    }

    [Fact]
    public async Task Noticias_publicadas_aparecen_en_el_sitio_y_los_borradores_no()
    {
        var dir = await api.DirectoraAsync();
        var titulo = "Noticia " + Guid.NewGuid().ToString("N")[..6];
        await (await dir.PostJsonAsync("/api/Contenido/noticias",
            new { titulo, resumen = "Resumen", contenido = "Texto", imagenUrl = (string?)null, publicada = true })).EsperarAsync(HttpStatusCode.OK);
        await (await dir.PostJsonAsync("/api/Contenido/noticias",
            new { titulo = titulo + " borrador", resumen = (string?)null, contenido = "Texto", imagenUrl = (string?)null, publicada = false })).EsperarAsync(HttpStatusCode.OK);

        var noticias = await (await api.Anonimo().GetAsync("/api/Contenido/noticias")).JsonAsync();
        Assert.Contains(noticias.EnumerateArray(), n => n.Str("titulo") == titulo);
        Assert.DoesNotContain(noticias.EnumerateArray(), n => n.Str("titulo") == titulo + " borrador");
        await (await (await api.EncargadoAsync()).PostJsonAsync("/api/Contenido/noticias",
            new { titulo, contenido = "x", publicada = true })).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Galeria_albumes_imagenes_con_miniatura_videos_y_errores_claros()
    {
        var dir = await api.DirectoraAsync();
        var album = await (await dir.PostJsonAsync("/api/Galeria/albumes", new { titulo = "Feria 2026", descripcion = "Fotos", publicado = false })).JsonAsync();
        var id = album.Id();
        await (await dir.PostJsonAsync("/api/Galeria/albumes", new { titulo = "  ", descripcion = (string?)null, publicado = false })).EsperarAsync(HttpStatusCode.BadRequest);

        var subida = await (await dir.PostAsync($"/api/Galeria/albumes/{id}/imagenes",
            Archivo("archivos", "foto grande.jpg", ArchivosYPdfTests.Imagen(2500, 1200, SKEncodedImageFormat.Jpeg), "image/jpeg"))).JsonAsync();
        var item = subida.GetProperty("subidas")[0];

        var falso = await (await dir.PostAsync($"/api/Galeria/albumes/{id}/imagenes",
            Archivo("archivos", "falso.jpg", "no soy imagen"u8.ToArray(), "image/jpeg"))).JsonAsync(HttpStatusCode.BadRequest);
        Assert.Contains("no es una imagen", falso.GetProperty("errores")[0].Str("error"));
        await (await dir.PostAsync($"/api/Galeria/albumes/{Guid.NewGuid()}/imagenes",
            Archivo("archivos", "x.png", ArchivosYPdfTests.Imagen(10, 10), "image/png"))).EsperarAsync(HttpStatusCode.NotFound);
        await (await (await api.EncargadoAsync()).PostAsync($"/api/Galeria/albumes/{id}/imagenes",
            Archivo("archivos", "x.png", ArchivosYPdfTests.Imagen(10, 10), "image/png"))).EsperarAsync(HttpStatusCode.Forbidden);

        var video = await (await dir.PostJsonAsync($"/api/Galeria/albumes/{id}/videos", new { url = "https://youtu.be/dQw4w9WgXcQ", titulo = "Video" })).JsonAsync();
        Assert.Contains("youtube-nocookie.com/embed/dQw4w9WgXcQ", video.Str("videoUrl"));
        await (await dir.PostJsonAsync($"/api/Galeria/albumes/{id}/videos", new { url = "https://malo.com/v", titulo = (string?)null })).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostJsonAsync($"/api/Galeria/albumes/{Guid.NewGuid()}/videos", new { url = "https://youtu.be/dQw4w9WgXcQ", titulo = (string?)null })).EsperarAsync(HttpStatusCode.NotFound);

        // Oculto: el público no lo ve; la directora sí
        await (await api.Anonimo().GetAsync($"/api/Galeria/albumes/{id}")).EsperarAsync(HttpStatusCode.NotFound);
        Assert.Equal(2, (await (await dir.GetAsync($"/api/Galeria/albumes/{id}")).JsonAsync()).GetProperty("items").GetArrayLength());
        Assert.DoesNotContain((await (await api.Anonimo().GetAsync("/api/Galeria/albumes")).JsonAsync()).EnumerateArray(), a => a.Id() == id);

        var publicado = await (await dir.PutJsonAsync($"/api/Galeria/albumes/{id}", new { titulo = "Feria 2026", descripcion = "Fotos", publicado = true })).JsonAsync();
        Assert.Equal(1, publicado.GetProperty("cantidadImagenes").GetInt32());
        Assert.Equal(1, publicado.GetProperty("cantidadVideos").GetInt32());
        Assert.Contains((await (await api.Anonimo().GetAsync("/api/Galeria/albumes")).JsonAsync()).EnumerateArray(), a => a.Id() == id);
        Assert.Contains((await (await dir.GetAsync("/api/Galeria/admin/albumes")).JsonAsync()).EnumerateArray(), a => a.Id() == id);
        await (await dir.PutJsonAsync($"/api/Galeria/albumes/{Guid.NewGuid()}", new { titulo = "X", descripcion = (string?)null, publicado = true })).EsperarAsync(HttpStatusCode.NotFound);

        // La imagen se sirve reducida a 1920 px y la miniatura es un JPEG de 400x400
        var img = await api.Anonimo().GetAsync(item.Str("imagenUrl"));
        await img.EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("nosniff", img.Headers.GetValues("X-Content-Type-Options").Single());
        using (var bmp = SKBitmap.Decode(await img.Content.ReadAsByteArrayAsync())) Assert.Equal(1920, bmp.Width);
        using (var mini = SKBitmap.Decode(await (await api.Anonimo().GetAsync(item.Str("miniaturaUrl"))).Content.ReadAsByteArrayAsync()))
            Assert.Equal((400, 400), (mini.Width, mini.Height));

        await (await dir.DeleteAsync($"/api/Galeria/items/{item.Id()}")).EsperarAsync(HttpStatusCode.NoContent);
        await (await api.Anonimo().GetAsync(item.Str("imagenUrl"))).EsperarAsync(HttpStatusCode.NotFound);
        await (await dir.DeleteAsync($"/api/Galeria/items/{Guid.NewGuid()}")).EsperarAsync(HttpStatusCode.NotFound);
        await (await api.Anonimo().GetAsync("/api/Contenido/galeria")).EsperarAsync(HttpStatusCode.OK);

        await (await dir.DeleteAsync($"/api/Galeria/albumes/{id}")).EsperarAsync(HttpStatusCode.NoContent);
        await (await dir.DeleteAsync($"/api/Galeria/albumes/{id}")).EsperarAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Quienes_somos_editable_por_la_directora_con_validaciones()
    {
        var info = await (await api.Anonimo().GetAsync("/api/Institucional")).JsonAsync();
        Assert.Contains("Empoderar", info.Str("mision"));

        var dir = await api.DirectoraAsync();
        object Pagina(string? email, params object[] equipo) => new
        {
            mision = "Nueva misión", vision = "Visión", historia = "Historia", equipo,
            direccion = "La Paz", telefono = "+591 70000000", emailContacto = email, horario = "Lun a Vie", actualizadoEn = (DateTime?)null
        };

        var guardado = await (await dir.PutJsonAsync("/api/Institucional",
            Pagina("contacto@jaguares.org", new { nombre = "María", cargo = "Directora", descripcion = (string?)null }))).JsonAsync();
        Assert.Equal("María", guardado.GetProperty("equipo")[0].Str("nombre"));

        var malo = await (await dir.PutJsonAsync("/api/Institucional", Pagina("no-es-correo"))).JsonAsync(HttpStatusCode.BadRequest);
        Assert.True(malo.GetProperty("errores").TryGetProperty("emailContacto", out _));
        await (await dir.PutJsonAsync("/api/Institucional", Pagina(null, new { nombre = " ", cargo = "x", descripcion = (string?)null }))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await (await api.JuanAsync()).PutJsonAsync("/api/Institucional", Pagina(null))).EsperarAsync(HttpStatusCode.Forbidden);

        Assert.Equal("Nueva misión", (await (await api.Anonimo().GetAsync("/api/Institucional")).JsonAsync()).Str("mision"));
    }

    [Fact]
    public async Task Contacto_valida_campos_y_descarta_bots()
    {
        var anon = api.Anonimo();
        var errores = await (await anon.PostJsonAsync("/api/Contacto",
            new { nombre = "", email = "malo", asunto = (string?)null, mensaje = "corto", sitioWeb = "" })).JsonAsync(HttpStatusCode.BadRequest);
        foreach (var campo in new[] { "nombre", "email", "mensaje" })
            Assert.True(errores.GetProperty("errores").TryGetProperty(campo, out _), campo);

        await (await anon.PostJsonAsync("/api/Contacto",
            new { nombre = "Ana", email = "ana@correo.com", asunto = (string?)null, mensaje = "http://a http://b http://c http://d texto", sitioWeb = "" }))
            .EsperarAsync(HttpStatusCode.BadRequest);
        // Campo trampa lleno: responde OK pero no guarda nada
        await (await anon.PostJsonAsync("/api/Contacto",
            new { nombre = "Bot", email = "bot@spam.com", asunto = "x", mensaje = "Compre ahora barato", sitioWeb = "http://spam" }))
            .EsperarAsync(HttpStatusCode.OK);
        await (await anon.PostJsonAsync("/api/Contacto",
            new { nombre = "Ana", email = "ana@correo.com", asunto = "Voluntariado\r\nBcc: x@y.com", mensaje = "Quiero ser voluntaria.", sitioWeb = "" }))
            .EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Contacto_limita_a_5_consultas_por_IP_por_hora()
    {
        using var scope = api.Factory.Services.CreateScope();
        var contacto = scope.ServiceProvider.GetRequiredService<IContactoService>();
        var ip = $"10.9.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}";
        var consulta = new ConsultaContactoDto("Ana", "ana@correo.com", null, "Mensaje de prueba válido", null);

        for (var i = 0; i < ContactoService.MaxPorIpHora; i++) await contacto.EnviarAsync(consulta, ip);
        await Assert.ThrowsAsync<LimiteExcedidoException>(() => contacto.EnviarAsync(consulta, ip));
        await contacto.EnviarAsync(consulta, "10.250.250.250"); // otra IP no se ve afectada
    }

    [Fact]
    public async Task Plantillas_de_certificado_crud_imagenes_vista_previa_y_uso()
    {
        var dir = await api.DirectoraAsync();
        object Plantilla(string nombre, string texto = "Se certifica que {participante} completó {actividad}.", string tipo = "TALLER") => new
        {
            nombre, tipoActividad = tipo, titulo = "Certificado", texto,
            firmaUnoNombre = "María", firmaUnoCargo = "Directora", firmaDosNombre = "Carlos", firmaDosCargo = "Docente"
        };
        var nombre = "Plantilla " + Guid.NewGuid().ToString("N")[..6];

        await (await dir.PostJsonAsync("/api/Certificados/plantillas", Plantilla(nombre, "Sin la variable del nombre"))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostJsonAsync("/api/Certificados/plantillas", Plantilla(nombre, "{participante} {desconocida}"))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostJsonAsync("/api/Certificados/plantillas", Plantilla(nombre, tipo: "FIESTA"))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostJsonAsync("/api/Certificados/plantillas", Plantilla(""))).EsperarAsync(HttpStatusCode.BadRequest);

        var p = await (await dir.PostJsonAsync("/api/Certificados/plantillas", Plantilla(nombre))).JsonAsync();
        await (await dir.PostJsonAsync("/api/Certificados/plantillas", Plantilla(nombre.ToUpperInvariant()))).EsperarAsync(HttpStatusCode.BadRequest); // nombre repetido
        var editada = await (await dir.PutJsonAsync($"/api/Certificados/plantillas/{p.Id()}", Plantilla(nombre, "Para {participante}, {fecha}, {codigo}"))).JsonAsync();
        Assert.Contains("{codigo}", editada.Str("texto"));
        await (await dir.PutJsonAsync($"/api/Certificados/plantillas/{Guid.NewGuid()}", Plantilla("Otra"))).EsperarAsync(HttpStatusCode.NotFound);

        var conLogo = await (await dir.PostAsync($"/api/Certificados/plantillas/{p.Id()}/imagenes/logo",
            Archivo("archivo", "logo.png", ArchivosYPdfTests.Imagen(300, 150), "image/png"))).JsonAsync();
        Assert.NotNull(conLogo.GetProperty("logoUrl").GetString());
        await (await dir.PostAsync($"/api/Certificados/plantillas/{p.Id()}/imagenes/firmaUno",
            Archivo("archivo", "firma.png", ArchivosYPdfTests.Imagen(200, 80), "image/png"))).EsperarAsync(HttpStatusCode.OK);
        await (await dir.PostAsync($"/api/Certificados/plantillas/{p.Id()}/imagenes/fondo",
            Archivo("archivo", "x.png", ArchivosYPdfTests.Imagen(20, 20), "image/png"))).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostAsync($"/api/Certificados/plantillas/{p.Id()}/imagenes/logo",
            Archivo("archivo", "x.gif", "GIF89a"u8.ToArray(), "image/gif"))).EsperarAsync(HttpStatusCode.BadRequest);

        var previa = await (await api.DocenteAsync()).GetAsync($"/api/Certificados/plantillas/{p.Id()}/vista-previa");
        await previa.EsperarAsync(HttpStatusCode.OK);
        Assert.Equal("application/pdf", previa.Content.Headers.ContentType?.MediaType);
        await (await dir.GetAsync($"/api/Certificados/plantillas/{Guid.NewGuid()}/vista-previa")).EsperarAsync(HttpStatusCode.NotFound);

        var sinLogo = await (await dir.DeleteAsync($"/api/Certificados/plantillas/{p.Id()}/imagenes/logo")).JsonAsync();
        Assert.Equal(JsonValueKind.Null, sinLogo.GetProperty("logoUrl").ValueKind);
        await (await dir.DeleteAsync($"/api/Certificados/plantillas/{p.Id()}/imagenes/otra")).EsperarAsync(HttpStatusCode.BadRequest);

        var plantillas = await (await (await api.DocenteAsync()).GetAsync("/api/Certificados/plantillas")).JsonAsync();
        Assert.Contains(plantillas.EnumerateArray(), x => x.Id() == p.Id());

        // En uso por un curso activo no se puede borrar
        var curso = await (await dir.PostJsonAsync("/api/Cursos", new
        {
            titulo = "Curso con plantilla", descripcion = (string?)null,
            fechaInicio = DateOnly.FromDateTime(DateTime.Today.AddDays(1)), fechaFin = DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
            cupoMaximo = 5, asistenciaMinimaPct = 80, plantillaId = p.Id()
        })).JsonAsync();
        await (await dir.DeleteAsync($"/api/Certificados/plantillas/{p.Id()}")).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PutJsonAsync($"/api/Cursos/{curso.Id()}/plantilla", new { plantillaId = (Guid?)null })).EsperarAsync(HttpStatusCode.NoContent);
        await (await dir.DeleteAsync($"/api/Certificados/plantillas/{p.Id()}")).EsperarAsync(HttpStatusCode.NoContent);
        await (await dir.DeleteAsync($"/api/Certificados/plantillas/{p.Id()}")).EsperarAsync(HttpStatusCode.NotFound);
    }
}
