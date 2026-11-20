using System.Text;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure.Adapters;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace FundacionJaguares.Tests.Unitarias;

// Imágenes, PDFs y almacenamiento de archivos (sin base de datos)
public class ArchivosYPdfTests
{
    public static byte[] Imagen(int ancho, int alto, SKEncodedImageFormat formato = SKEncodedImageFormat.Png)
    {
        using var bmp = new SKBitmap(ancho, alto);
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(SKColors.OrangeRed);
            using var p = new SKPaint { Color = SKColors.Navy };
            c.DrawRect(0, 0, ancho / 2f, alto / 2f, p);
        }
        using var img = SKImage.FromBitmap(bmp);
        return img.Encode(formato, 90).ToArray();
    }

    [Fact]
    public void Procesar_reduce_a_lado_maximo_y_genera_miniatura_cuadrada_jpeg()
    {
        var r = ProcesadorImagen.Procesar(Imagen(3000, 1500, SKEncodedImageFormat.Jpeg), esPng: false, 1920, 400);

        Assert.Equal((1920, 960), (r.Ancho, r.Alto));
        Assert.Equal(".jpg", r.Extension);
        using var mini = SKBitmap.Decode(r.Miniatura);
        Assert.Equal((400, 400), (mini.Width, mini.Height));
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, r.Miniatura[..3]);
    }

    [Fact]
    public void Procesar_no_agranda_imagenes_chicas_y_mantiene_png()
    {
        var r = ProcesadorImagen.Procesar(Imagen(300, 200), esPng: true, 1920, 400);
        Assert.Equal((300, 200), (r.Ancho, r.Alto));
        Assert.Equal(".png", r.Extension);
        using var mini = SKBitmap.Decode(r.Miniatura);
        Assert.Equal(200, mini.Width); // el recorte cuadrado no supera el lado menor
    }

    [Fact]
    public void Procesar_rechaza_datos_que_no_son_imagen() =>
        Assert.Throws<ArgumentException>(() =>
            ProcesadorImagen.Procesar([0xFF, 0xD8, 0xFF, 1, 2, 3, 4, 5], esPng: false, 1920, 400));

    [Fact]
    public async Task LeerValidado_acepta_png_real()
    {
        var datos = Imagen(50, 50);
        var (leidos, esPng) = await ProcesadorImagen.LeerValidadoAsync(new ArchivoSubido("foto.png", datos.Length, new MemoryStream(datos)));
        Assert.True(esPng);
        Assert.Equal(datos.Length, leidos.Length);
    }

    [Theory]
    [InlineData("foto.gif", "GIF89a......")]          // extensión no permitida
    [InlineData("foto.jpg", "esto no es una imagen")] // firma falsa
    [InlineData("vacia.png", "")]                     // vacío
    public async Task LeerValidado_rechaza_archivos_invalidos(string nombre, string contenido)
    {
        var datos = Encoding.ASCII.GetBytes(contenido);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ProcesadorImagen.LeerValidadoAsync(new ArchivoSubido(nombre, datos.Length, new MemoryStream(datos))));
    }

    [Fact]
    public async Task LeerValidado_rechaza_mas_de_5MB_aunque_el_tamano_declarado_mienta()
    {
        var datos = new byte[ProcesadorImagen.MaxBytes + 10];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(datos, 0);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ProcesadorImagen.LeerValidadoAsync(new ArchivoSubido("grande.png", 100, new MemoryStream(datos))));
    }

    [Fact]
    public void Certificado_pdf_se_genera_con_y_sin_firmas()
    {
        var diseno = new DisenoCertificado("Certificado", "Otorgado a {participante} por {actividad} el {fecha}. Código {codigo}",
            Imagen(200, 100), [new FirmaCertificado(Imagen(150, 60), "María", "Directora"), new FirmaCertificado(null, "Carlos", null)]);
        var pdf = GeneradorCertificadoPdf.Generar(diseno, new DatosCertificado("Juan Pérez", "Taller", DateTime.UtcNow, "FUJ-1"));
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        var simple = GeneradorCertificadoPdf.Generar(new DisenoCertificado("C", "{participante}", null, []),
            new DatosCertificado("Ana", "Curso", DateTime.UtcNow, "X"));
        Assert.Equal("%PDF", Encoding.ASCII.GetString(simple, 0, 4));
        Assert.Contains("2026", GeneradorCertificadoPdf.FechaLarga(new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Comprobante_pdf_y_formatos()
    {
        var pdf = GeneradorComprobantePdf.Generar(new DatosComprobante(42, DateTime.UtcNow, 150.5m, "BOB", MedioPago.QR, "REF-1", "Ana"));
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Equal("000042", GeneradorComprobantePdf.Numero(42));
        Assert.Contains("150", GeneradorComprobantePdf.Monto(150.5m, "BOB"));
    }

    [Fact]
    public async Task Almacenamiento_guarda_lee_y_borra_dentro_de_su_carpeta()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "jaguares-alm-" + Guid.NewGuid().ToString("N"));
        var alm = new AlmacenamientoLocal(raiz, "/uploads");
        try
        {
            var url = await alm.GuardarAsync("galeria/abc", "x.png", new MemoryStream([1, 2, 3]));
            Assert.Equal("/uploads/galeria/abc/x.png", url);
            Assert.Equal(new byte[] { 1, 2, 3 }, await alm.LeerAsync(url));

            alm.Eliminar(url);
            Assert.Null(await alm.LeerAsync(url));
            Assert.Null(await alm.LeerAsync("/otra/ruta.png"));
            alm.Eliminar("/uploads/../../Windows/win.ini"); // fuera de la carpeta: se ignora
            alm.Eliminar(null);
        }
        finally { Directory.Delete(raiz, recursive: true); }
    }

    [Theory]
    [InlineData("../..", "x.png")]
    [InlineData("galeria", "..")]
    [InlineData("galeria", ".oculto")]
    [InlineData("gal eria", "x.png")]
    public async Task Almacenamiento_rechaza_rutas_peligrosas(string carpeta, string nombre)
    {
        var alm = new AlmacenamientoLocal(Path.Combine(Path.GetTempPath(), "jaguares-alm-" + Guid.NewGuid().ToString("N")), "/uploads");
        await Assert.ThrowsAsync<ArgumentException>(() => alm.GuardarAsync(carpeta, nombre, new MemoryStream([1])));
    }

    [Fact]
    public async Task Adaptadores_sin_configurar_solo_registran_en_el_log()
    {
        var email = new EmailAdapter(NullLogger<EmailAdapter>.Instance, new SmtpOpciones());
        await email.EnviarAsync("ana@correo.com", "Asunto", "Cuerpo", responderA: "otro@correo.com");
        await new WhatsAppAdapter(NullLogger<WhatsAppAdapter>.Instance).EnviarAsync("70000000", "Hola");
        Assert.True(await new PaymentAdapter(NullLogger<PaymentAdapter>.Instance).ProcesarPagoAsync(10, "REF"));
        Assert.False(new SmtpOpciones().Configurado);
        Assert.True(new SmtpOpciones { Host = "smtp.x.com" }.Configurado);
    }
}
