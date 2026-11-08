using System.Text.RegularExpressions;
using SkiaSharp;

namespace FundacionJaguares.Application.Services;

public record FirmaCertificado(byte[]? Imagen, string? Nombre, string? Cargo);
public record DisenoCertificado(string Titulo, string Texto, byte[]? Logo, IReadOnlyList<FirmaCertificado> Firmas);
public record DatosCertificado(string Participante, string Actividad, DateTime EmitidoEnUtc, string Codigo);

// FUJ-115: dibuja el certificado en PDF (A4 horizontal) con SkiaSharp a partir de un diseño y los datos del participante
public static partial class GeneradorCertificadoPdf
{
    private const float Ancho = 842, Alto = 595, MargenTexto = 90;
    private const float PiePagina = Alto - 46;
    private const float LineaFirma = Alto - 112;
    private const float AltoImagenFirma = 62;

    private static readonly SKColor ColorAcento = new(0xB4, 0x53, 0x09);
    private static readonly SKColor ColorTexto = new(0x1F, 0x29, 0x37);
    private static readonly SKColor ColorSuave = new(0x6B, 0x72, 0x80);
    private static readonly string[] Meses =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto",
         "septiembre", "octubre", "noviembre", "diciembre"];

    public static readonly string[] Variables = ["{participante}", "{actividad}", "{fecha}", "{codigo}"];

    [GeneratedRegex(@"(\{[^{}]*\})")]
    private static partial Regex Variable();

    public static string FechaLarga(DateTime utc)
    {
        var f = utc.AddHours(-4); // hora de Bolivia
        return $"{f.Day} de {Meses[f.Month - 1]} de {f.Year}";
    }

    public static byte[] Generar(DisenoCertificado diseno, DatosCertificado datos)
    {
        using var salida = new MemoryStream();
        using (var wstream = new SKManagedWStream(salida))
        using (var doc = SKDocument.CreatePdf(wstream))
        {
            using var normal = Fuente(SKFontStyle.Normal);
            using var negrita = Fuente(SKFontStyle.Bold);
            var c = doc.BeginPage(Ancho, Alto);

            DibujarMarco(c);

            float y = 52;
            if (diseno.Logo is not null)
                y += DibujarImagen(c, diseno.Logo, Ancho / 2, y, 190, 84) + 14;
            else
                y += 28;

            y += 38;
            DibujarCentrado(c, diseno.Titulo, Ancho / 2, y, Ancho - 2 * MargenTexto, 36, negrita, ColorAcento);

            var firmas = diseno.Firmas.Where(f => f.Imagen is not null || f.Nombre is not null || f.Cargo is not null).ToList();
            var limite = firmas.Count > 0 ? LineaFirma - AltoImagenFirma - 12 : PiePagina - 24;
            DibujarCuerpo(c, diseno.Texto, datos, y + 18, limite, normal, negrita);

            DibujarFirmas(c, firmas, normal, negrita);

            DibujarCentrado(c, $"Código de verificación: {datos.Codigo}", Ancho / 2, PiePagina,
                            Ancho - 2 * MargenTexto, 10, normal, ColorSuave);

            doc.EndPage();
            doc.Close();
        }
        return salida.ToArray();
    }

    private static SKTypeface Fuente(SKFontStyle estilo) =>
        SKTypeface.FromFamilyName("Georgia", estilo) ?? SKTypeface.Default;

    private static void DibujarMarco(SKCanvas c)
    {
        using var grueso = new SKPaint { Color = ColorAcento, IsStroke = true, StrokeWidth = 3, IsAntialias = true };
        using var fino = new SKPaint { Color = ColorAcento, IsStroke = true, StrokeWidth = 0.75f, IsAntialias = true };
        c.DrawRect(SKRect.Create(22, 22, Ancho - 44, Alto - 44), grueso);
        c.DrawRect(SKRect.Create(29, 29, Ancho - 58, Alto - 58), fino);
    }

    // Dibuja la imagen centrada en cx y ajustada al recuadro máximo. Devuelve el alto usado (0 si no se pudo leer).
    private static float DibujarImagen(SKCanvas c, byte[] datos, float cx, float arriba, float anchoMax, float altoMax)
    {
        using var bmp = SKBitmap.Decode(datos);
        if (bmp is null) return 0;
        var escala = Math.Min(anchoMax / bmp.Width, altoMax / bmp.Height);
        float w = bmp.Width * escala, h = bmp.Height * escala;
        using var img = SKImage.FromBitmap(bmp);
        c.DrawImage(img, SKRect.Create(bmp.Width, bmp.Height), SKRect.Create(cx - w / 2, arriba, w, h),
                    new SKSamplingOptions(SKCubicResampler.Mitchell));
        return h;
    }

    // Una sola línea centrada; reduce la letra si no cabe en el ancho disponible
    private static void DibujarCentrado(SKCanvas c, string texto, float cx, float y, float anchoMax, float tamano,
                                        SKTypeface tipo, SKColor color)
    {
        using var font = new SKFont(tipo, tamano);
        var medida = font.MeasureText(texto);
        if (medida > anchoMax) font.Size = tamano * anchoMax / medida;
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        c.DrawText(texto, cx, y, SKTextAlign.Center, font, paint);
    }

    // Pegada: va sin espacio tras la anterior (por ejemplo, el punto que sigue a una variable)
    private record Palabra(string Texto, bool Negrita, bool Pegada);

    // Sustituye las variables en una sola pasada (un nombre con llaves no se vuelve a interpretar).
    // El nombre del participante va en negrita. Devuelve una lista de palabras por párrafo.
    private static List<List<Palabra>> Parrafos(string texto, DatosCertificado d)
    {
        var parrafos = new List<List<Palabra>>();
        foreach (var linea in texto.Replace("\r\n", "\n").Split('\n'))
        {
            var palabras = new List<Palabra>();
            var sinEspacioAlFinal = false;
            foreach (var parte in Variable().Split(linea).Where(p => p.Length > 0))
            {
                var (valor, negrita) = parte switch
                {
                    "{participante}" => (d.Participante, true),
                    "{actividad}" => (d.Actividad, false),
                    "{fecha}" => (FechaLarga(d.EmitidoEnUtc), false),
                    "{codigo}" => (d.Codigo, false),
                    _ => (parte, false)
                };
                if (valor.Length == 0) continue;
                var pegarPrimera = sinEspacioAlFinal && valor[0] != ' ';
                palabras.AddRange(valor.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select((p, i) => new Palabra(p, negrita, i == 0 && pegarPrimera)));
                sinEspacioAlFinal = valor[^1] != ' ';
            }
            parrafos.Add(palabras);
        }
        return parrafos;
    }

    private static List<List<Palabra>> PartirEnLineas(List<List<Palabra>> parrafos, SKFont normal, SKFont negrita, float anchoMax)
    {
        var lineas = new List<List<Palabra>>();
        var espacio = normal.MeasureText(" ");
        foreach (var parrafo in parrafos)
        {
            var actual = new List<Palabra>();
            float ancho = 0;
            foreach (var p in parrafo)
            {
                var w = (p.Negrita ? negrita : normal).MeasureText(p.Texto);
                var separacion = p.Pegada ? 0 : espacio;
                if (actual.Count > 0 && !p.Pegada && ancho + separacion + w > anchoMax)
                {
                    lineas.Add(actual);
                    actual = [];
                    ancho = 0;
                }
                ancho += (actual.Count > 0 ? separacion : 0) + w;
                actual.Add(p);
            }
            lineas.Add(actual);
        }
        return lineas;
    }

    // Reduce la letra hasta que el texto quepa y lo centra verticalmente en el espacio libre
    private static void DibujarCuerpo(SKCanvas c, string texto, DatosCertificado datos, float arriba, float abajo,
                                      SKTypeface tipoNormal, SKTypeface tipoNegrita)
    {
        var parrafos = Parrafos(texto, datos);
        var anchoMax = Ancho - 2 * MargenTexto;
        using var normal = new SKFont(tipoNormal, 20);
        using var negrita = new SKFont(tipoNegrita, 20);

        List<List<Palabra>> lineas;
        float tamano = 20, interlinea;
        while (true)
        {
            normal.Size = negrita.Size = tamano;
            lineas = PartirEnLineas(parrafos, normal, negrita, anchoMax);
            interlinea = tamano * 1.45f;
            if (lineas.Count * interlinea <= abajo - arriba || tamano <= 8) break;
            tamano -= 1;
        }

        var y = arriba + Math.Max(0, (abajo - arriba - lineas.Count * interlinea) / 2) + tamano;
        var espacio = normal.MeasureText(" ");
        using var paint = new SKPaint { Color = ColorTexto, IsAntialias = true };
        foreach (var linea in lineas)
        {
            var total = linea.Sum(p => (p.Negrita ? negrita : normal).MeasureText(p.Texto))
                        + espacio * linea.Skip(1).Count(p => !p.Pegada);
            var x = (Ancho - total) / 2;
            foreach (var (p, i) in linea.Select((p, i) => (p, i)))
            {
                if (i > 0 && !p.Pegada) x += espacio;
                var font = p.Negrita ? negrita : normal;
                c.DrawText(p.Texto, x, y, SKTextAlign.Left, font, paint);
                x += font.MeasureText(p.Texto);
            }
            y += interlinea;
        }
    }

    private static void DibujarFirmas(SKCanvas c, List<FirmaCertificado> firmas, SKTypeface normal, SKTypeface negrita)
    {
        if (firmas.Count == 0) return;
        var columna = (Ancho - 2 * MargenTexto) / firmas.Count;
        var anchoLinea = Math.Min(230, columna - 30);
        using var trazo = new SKPaint { Color = ColorSuave, IsStroke = true, StrokeWidth = 0.75f, IsAntialias = true };

        for (var i = 0; i < firmas.Count; i++)
        {
            var f = firmas[i];
            var cx = MargenTexto + columna * (i + 0.5f);
            if (f.Imagen is not null)
                DibujarImagen(c, f.Imagen, cx, LineaFirma - AltoImagenFirma - 2, anchoLinea, AltoImagenFirma);
            c.DrawLine(cx - anchoLinea / 2, LineaFirma, cx + anchoLinea / 2, LineaFirma, trazo);
            if (f.Nombre is not null)
                DibujarCentrado(c, f.Nombre, cx, LineaFirma + 18, anchoLinea, 12, negrita, ColorTexto);
            if (f.Cargo is not null)
                DibujarCentrado(c, f.Cargo, cx, LineaFirma + 34, anchoLinea, 10.5f, normal, ColorSuave);
        }
    }
}
