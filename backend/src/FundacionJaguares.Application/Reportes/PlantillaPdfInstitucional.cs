using System.Globalization;
using System.Text;
using SkiaSharp;

namespace FundacionJaguares.Application.Reportes;

// JAG-86 / corrección de la evaluación (exportaciones): plantilla PDF institucional única para todos los reportes.
// Documentada en docs/plantilla-pdf-institucional.pdf (fuente: tools/documentos-pdf/fuentes).
//
// Se genera en dos pasos:
//   1) Maquetar(): calcula QUÉ va en cada página y dónde (lista de elementos). Es determinista y no depende
//      de las fuentes instaladas: las pruebas de regresión (snapshot) comparan este resultado.
//   2) Dibujar(): pinta esos elementos con SkiaSharp.
//
// Seguridad: el encabezado y los metadatos del PDF NO llevan datos de quien lo generó (ni nombre ni correo):
// solo el nombre de la fundación, el título del reporte, la fecha y los filtros usados.
public static class PlantillaPdfInstitucional
{
    public const string Institucion = "Fundación Unión de los Jaguares";
    public const string Pie = "Fundación Unión de los Jaguares · Documento generado por la plataforma · Uso interno";

    // A4 en puntos. Con más de 5 columnas se usa horizontal.
    public const float LadoCorto = 595, LadoLargo = 842, Margen = 36;
    public const float AltoFilaEncabezado = 20, AltoFila = 17, TamanoCelda = 8.5f, TamanoEncabezadoTabla = 8.5f;
    // Ancho promedio de un carácter respecto al tamaño de letra (para recortar sin medir con la fuente)
    private const float FactorCaracter = 0.52f;

    public static readonly string ColorAcento = "B45309", ColorTexto = "1F2937", ColorSuave = "6B7280",
                                  ColorFondoEncabezado = "FEF3C7", ColorFilaAlterna = "F9FAFB", ColorLinea = "E5E7EB";

    public abstract record Elemento(int Pagina);
    public record Texto(int Pagina, float X, float Y, float Ancho, string Contenido, float Tamano, bool Negrita,
                        string Color, bool Derecha = false) : Elemento(Pagina);
    public record Rect(int Pagina, float X, float Y, float Ancho, float Alto, string Color) : Elemento(Pagina);

    public record Maqueta(float Ancho, float Alto, int Paginas, List<Elemento> Elementos);

    public static string FechaGeneracion(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(-4).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " (hora de Bolivia)";

    public static string Recortar(string? texto, float ancho, float tamano)
    {
        var limpio = (texto ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
        var max = Math.Max(1, (int)(ancho / (tamano * FactorCaracter)));
        return limpio.Length <= max ? limpio : limpio[..Math.Max(0, max - 1)].TrimEnd() + "…";
    }

    public static Maqueta Maquetar(TablaReporte t, DateTime generadoUtc)
    {
        var horizontal = t.Columnas.Count > 5;
        float ancho = horizontal ? LadoLargo : LadoCorto, alto = horizontal ? LadoCorto : LadoLargo;
        var util = ancho - 2 * Margen;
        var elementos = new List<Elemento>();

        // Anchos de columna según su peso
        var pesoTotal = t.Columnas.Sum(c => c.Peso);
        var anchos = t.Columnas.Select(c => util * c.Peso / pesoTotal).ToArray();

        float inicioTabla = 128;
        var filasPorPagina = Math.Max(1, (int)((alto - Margen - 30 - inicioTabla - AltoFilaEncabezado) / AltoFila));
        // Las filas de totales ocupan espacio al final de la última página
        var totales = t.Totales ?? [];
        var filasTotales = totales.Count == 0 ? 0 : totales.Count + 1;
        var paginas = Math.Max(1, (int)Math.Ceiling((t.Filas.Count + filasTotales) / (double)filasPorPagina));

        var filtros = t.Filtros.Count == 0 ? "Sin filtros (todos los registros)"
            : string.Join(" · ", t.Filtros.Select(f => $"{f.Etiqueta}: {f.Valor}"));

        for (var p = 1; p <= paginas; p++)
        {
            // Encabezado institucional (en cada página)
            elementos.Add(new Rect(p, 0, 0, ancho, 6, ColorAcento));
            elementos.Add(new Texto(p, Margen, 34, util, Institucion, 10, false, ColorSuave));
            elementos.Add(new Texto(p, Margen, 58, util, Recortar(t.Titulo, util, 16), 16, true, ColorAcento));
            if (!string.IsNullOrWhiteSpace(t.Subtitulo))
                elementos.Add(new Texto(p, Margen, 76, util, Recortar(t.Subtitulo, util, 10), 10, false, ColorTexto));
            elementos.Add(new Texto(p, Margen, 94, util,
                $"Generado el {FechaGeneracion(generadoUtc)} · {t.Filas.Count} registro(s)", 8.5f, false, ColorSuave));
            elementos.Add(new Texto(p, Margen, 108, util, Recortar("Filtros: " + filtros, util, 8.5f), 8.5f, false, ColorSuave));

            // Encabezado de la tabla (se repite en cada página)
            float y = inicioTabla;
            elementos.Add(new Rect(p, Margen, y, util, AltoFilaEncabezado, ColorFondoEncabezado));
            float x = Margen;
            for (var c = 0; c < t.Columnas.Count; c++)
            {
                var col = t.Columnas[c];
                var posX = col.Numerica ? x + anchos[c] - 4 : x + 4;
                elementos.Add(new Texto(p, posX, y + 13.5f, anchos[c] - 8, Recortar(col.Titulo, anchos[c] - 8, TamanoEncabezadoTabla),
                    TamanoEncabezadoTabla, true, ColorTexto, col.Numerica));
                x += anchos[c];
            }
            y += AltoFilaEncabezado;

            var desde = (p - 1) * filasPorPagina;
            var hasta = Math.Min(t.Filas.Count, desde + filasPorPagina);
            for (var i = desde; i < hasta; i++)
            {
                if ((i - desde) % 2 == 1) elementos.Add(new Rect(p, Margen, y, util, AltoFila, ColorFilaAlterna));
                x = Margen;
                var fila = t.Filas[i];
                for (var c = 0; c < t.Columnas.Count; c++)
                {
                    var col = t.Columnas[c];
                    var valor = c < fila.Length ? fila[c] : "";
                    var posX = col.Numerica ? x + anchos[c] - 4 : x + 4;
                    elementos.Add(new Texto(p, posX, y + 11.5f, anchos[c] - 8, Recortar(valor, anchos[c] - 8, TamanoCelda),
                        TamanoCelda, false, ColorTexto, col.Numerica));
                    x += anchos[c];
                }
                y += AltoFila;
            }

            if (p == paginas && totales.Count > 0)
            {
                elementos.Add(new Rect(p, Margen, y + 4, util, 1, ColorLinea));
                y += AltoFila;
                foreach (var tot in totales)
                {
                    elementos.Add(new Texto(p, Margen + 4, y + 4, util - 8, Recortar($"{tot.Etiqueta}: {tot.Valor}", util - 8, 9.5f),
                        9.5f, true, ColorTexto));
                    y += AltoFila;
                }
            }

            // Pie de página
            elementos.Add(new Rect(p, Margen, alto - Margen - 12, util, 0.75f, ColorLinea));
            elementos.Add(new Texto(p, Margen, alto - Margen, util - 90, Pie, 7.5f, false, ColorSuave));
            elementos.Add(new Texto(p, ancho - Margen, alto - Margen, 90, $"Página {p} de {paginas}", 7.5f, false, ColorSuave, true));
        }

        return new Maqueta(ancho, alto, paginas, elementos);
    }

    // Representación en texto de la maqueta: es lo que comparan las pruebas de regresión (snapshot)
    public static string Describir(Maqueta m)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.AppendLine(inv, $"pagina {m.Ancho}x{m.Alto} · {m.Paginas} pagina(s)");
        foreach (var e in m.Elementos)
            sb.AppendLine(e switch
            {
                Texto t => string.Create(inv, $"p{t.Pagina} texto ({t.X:0.#},{t.Y:0.#}) {t.Tamano:0.#}{(t.Negrita ? "b" : "")} #{t.Color}{(t.Derecha ? " der" : "")} \"{t.Contenido}\""),
                Rect r => string.Create(inv, $"p{r.Pagina} rect ({r.X:0.#},{r.Y:0.#}) {r.Ancho:0.#}x{r.Alto:0.##} #{r.Color}"),
                _ => ""
            });
        return sb.ToString();
    }

    public static byte[] Generar(TablaReporte t, DateTime generadoUtc) => Dibujar(Maquetar(t, generadoUtc), t.Titulo, generadoUtc);

    public static byte[] Dibujar(Maqueta m, string titulo, DateTime generadoUtc)
    {
        // Metadatos sin datos personales: ni autor humano ni correo, solo la institución y el título
        var metadatos = new SKDocumentPdfMetadata
        {
            Title = titulo,
            Author = Institucion,
            Creator = "Plataforma " + Institucion,
            Producer = Institucion,
            Subject = "Reporte institucional",
            Creation = generadoUtc,
            Modified = generadoUtc
        };
        using var salida = new MemoryStream();
        using (var wstream = new SKManagedWStream(salida))
        using (var doc = SKDocument.CreatePdf(wstream, metadatos))
        {
            using var normal = Services.Tipografias.Sans();
            using var negrita = Services.Tipografias.Sans(negrita: true);
            for (var p = 1; p <= m.Paginas; p++)
            {
                var c = doc.BeginPage(m.Ancho, m.Alto);
                foreach (var e in m.Elementos.Where(e => e.Pagina == p))
                {
                    switch (e)
                    {
                        case Rect r:
                            using (var paint = new SKPaint { Color = SKColor.Parse(r.Color) })
                                c.DrawRect(SKRect.Create(r.X, r.Y, r.Ancho, r.Alto), paint);
                            break;
                        case Texto t:
                            using (var font = new SKFont(t.Negrita ? negrita : normal, t.Tamano))
                            using (var paint = new SKPaint { Color = SKColor.Parse(t.Color), IsAntialias = true })
                            {
                                // Si con la fuente real no cabe, se reduce la letra (nunca se sale de su columna)
                                var medida = font.MeasureText(t.Contenido);
                                if (medida > t.Ancho && medida > 0) font.Size = t.Tamano * t.Ancho / medida;
                                c.DrawText(t.Contenido, t.X, t.Y, t.Derecha ? SKTextAlign.Right : SKTextAlign.Left, font, paint);
                            }
                            break;
                    }
                }
                doc.EndPage();
            }
            doc.Close();
        }
        return salida.ToArray();
    }
}
