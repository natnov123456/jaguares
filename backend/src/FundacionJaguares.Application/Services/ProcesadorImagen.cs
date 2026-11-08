using SkiaSharp;

namespace FundacionJaguares.Application.Services;

// Procesa las fotos de la galería (FUJ-126) con SkiaSharp (licencia MIT):
// corrige la orientación de las fotos de celular, limita el tamaño, quita metadatos (EXIF/GPS)
// y genera una miniatura cuadrada liviana para que la galería cargue rápido en el celular.
public static class ProcesadorImagen
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public const long MaxPixeles = 40_000_000; // protege contra imágenes "bomba" de dimensiones enormes

    private static readonly SKSamplingOptions Muestreo = new(SKCubicResampler.Mitchell);

    public record Resultado(byte[] Imagen, string Extension, byte[] Miniatura, int Ancho, int Alto);

    /// <summary>Lee un archivo subido con límite de tamaño y comprueba que sea realmente JPG o PNG.</summary>
    /// <exception cref="ArgumentException">Si el archivo no cumple las reglas de subida.</exception>
    public static async Task<(byte[] Datos, bool EsPng)> LeerValidadoAsync(ArchivoSubido archivo)
    {
        if (archivo.Tamano == 0) throw new ArgumentException("El archivo está vacío");
        if (archivo.Tamano > MaxBytes) throw new ArgumentException("El archivo supera el máximo de 5 MB");

        var ext = Path.GetExtension(archivo.Nombre).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png")) throw new ArgumentException("Formato no permitido: solo JPG o PNG");

        // Se lee con límite: el tamaño declarado por el cliente no es confiable
        using var ms = new MemoryStream();
        await CopiarConLimiteAsync(archivo.Contenido, ms, MaxBytes);
        var datos = ms.ToArray();

        // La extensión no basta: se comprueba la firma real del archivo
        var cabecera = datos.AsSpan(0, Math.Min(8, datos.Length));
        var esJpeg = cabecera.Length >= 3 && cabecera[0] == 0xFF && cabecera[1] == 0xD8 && cabecera[2] == 0xFF;
        var esPng = cabecera.Length >= 8 && cabecera.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        if (!esJpeg && !esPng) throw new ArgumentException("El archivo no es una imagen JPG o PNG válida");
        return (datos, esPng);
    }

    private static async Task CopiarConLimiteAsync(Stream origen, Stream destino, long limite)
    {
        var buffer = new byte[81920];
        long total = 0;
        int leidos;
        while ((leidos = await origen.ReadAsync(buffer)) > 0)
        {
            total += leidos;
            if (total > limite) throw new ArgumentException("El archivo supera el máximo de 5 MB");
            await destino.WriteAsync(buffer.AsMemory(0, leidos));
        }
    }

    /// <exception cref="ArgumentException">Si los datos no son una imagen válida o es demasiado grande.</exception>
    public static Resultado Procesar(byte[] datos, bool esPng, int ladoMaximo, int ladoMiniatura)
    {
        using var ampliada = Preparar(datos, ladoMaximo);
        var imagen = CodificarSegunTipo(ampliada, esPng);
        var miniatura = MiniaturaCuadrada(ampliada, ladoMiniatura);
        return new Resultado(imagen, esPng ? ".png" : ".jpg", miniatura, ampliada.Width, ampliada.Height);
    }

    /// <summary>Igual que <see cref="Procesar"/> pero sin miniatura (logos y firmas). Conserva la transparencia del PNG.</summary>
    /// <exception cref="ArgumentException">Si los datos no son una imagen válida o es demasiado grande.</exception>
    public static (byte[] Imagen, string Extension) Normalizar(byte[] datos, bool esPng, int ladoMaximo)
    {
        using var ampliada = Preparar(datos, ladoMaximo);
        return (CodificarSegunTipo(ampliada, esPng), esPng ? ".png" : ".jpg");
    }

    private static SKBitmap Preparar(byte[] datos, int ladoMaximo)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(datos))
                          ?? throw new ArgumentException("El archivo está dañado o no es una imagen válida");
        if ((long)codec.Info.Width * codec.Info.Height > MaxPixeles)
            throw new ArgumentException("La imagen tiene dimensiones demasiado grandes");

        using var decodificada = SKBitmap.Decode(codec)
                                 ?? throw new ArgumentException("El archivo está dañado o no es una imagen válida");
        using var orientada = Orientar(decodificada, codec.EncodedOrigin);

        // Imagen ampliada: como máximo ladoMaximo px en su lado mayor
        return Reducir(orientada, ladoMaximo);
    }

    // Al recodificar no se copian los metadatos: la foto publicada no incluye EXIF ni ubicación GPS
    private static byte[] CodificarSegunTipo(SKBitmap bmp, bool esPng) =>
        Codificar(bmp, esPng ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, esPng ? 100 : 82);

    private static SKBitmap Reducir(SKBitmap src, int ladoMaximo)
    {
        var mayor = Math.Max(src.Width, src.Height);
        if (mayor <= ladoMaximo) return src.Copy();
        var escala = (float)ladoMaximo / mayor;
        var info = new SKImageInfo(Math.Max(1, (int)Math.Round(src.Width * escala)),
                                   Math.Max(1, (int)Math.Round(src.Height * escala)), src.ColorType, src.AlphaType);
        return src.Resize(info, Muestreo) ?? throw new ArgumentException("No se pudo procesar la imagen");
    }

    // Recorte centrado al cuadrado; fondo blanco para PNG con transparencia (la miniatura siempre es JPEG)
    private static byte[] MiniaturaCuadrada(SKBitmap src, int lado)
    {
        var corte = Math.Min(src.Width, src.Height);
        var origen = SKRect.Create((src.Width - corte) / 2f, (src.Height - corte) / 2f, corte, corte);
        var destinoLado = Math.Min(lado, corte);

        using var superficie = SKSurface.Create(new SKImageInfo(destinoLado, destinoLado, SKColorType.Rgba8888, SKAlphaType.Premul));
        var lienzo = superficie.Canvas;
        lienzo.Clear(SKColors.White);
        using var img = SKImage.FromBitmap(src);
        lienzo.DrawImage(img, origen, SKRect.Create(destinoLado, destinoLado), Muestreo);
        lienzo.Flush();
        using var snapshot = superficie.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Jpeg, 75);
        return data.ToArray();
    }

    private static byte[] Codificar(SKBitmap bmp, SKEncodedImageFormat formato, int calidad)
    {
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(formato, calidad)
                         ?? throw new ArgumentException("No se pudo procesar la imagen");
        return data.ToArray();
    }

    // Aplica la orientación EXIF (las fotos de celular suelen venir "acostadas")
    private static SKBitmap Orientar(SKBitmap src, SKEncodedOrigin origen)
    {
        float w = src.Width, h = src.Height;
        SKMatrix? m = origen switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),     // espejo horizontal
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1), // 180°
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),   // espejo vertical
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),       // transpuesta
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),     // 90° horario
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1), // transversa
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),   // 90° antihorario
            _ => null
        };
        if (m is null) return src.Copy();

        var intercambia = origen is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
                                 or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var dst = new SKBitmap(intercambia ? src.Height : src.Width, intercambia ? src.Width : src.Height,
                               src.ColorType, src.AlphaType);
        using var lienzo = new SKCanvas(dst);
        lienzo.SetMatrix(m.Value);
        using var img = SKImage.FromBitmap(src);
        lienzo.DrawImage(img, 0, 0, SKSamplingOptions.Default);
        lienzo.Flush();
        return dst;
    }
}
