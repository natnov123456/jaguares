using System.Collections.Concurrent;
using SkiaSharp;

namespace FundacionJaguares.Application.Services;

// Tipografías de los PDF (certificados, comprobantes y reportes) incluidas dentro de la aplicación.
// En el servidor Linux (Docker, Render) SkiaSharp no busca las fuentes del sistema: si se pidiera "Georgia" o
// "Arial" el PDF saldría sin texto. Por eso se usan siempre estas, que son libres (DejaVu, ver LICENCIA-DejaVu.txt).
// Cada llamada devuelve una tipografía nueva: quien la pide puede liberarla con "using".
public static class Tipografias
{
    private static readonly ConcurrentDictionary<string, byte[]> Archivos = new();

    public static SKTypeface Serif(bool negrita = false) => Crear(negrita ? "DejaVuSerif-Bold.ttf" : "DejaVuSerif.ttf");
    public static SKTypeface Sans(bool negrita = false) => Crear(negrita ? "DejaVuSans-Bold.ttf" : "DejaVuSans.ttf");
    public static SKTypeface Serif(SKFontStyle estilo) => Serif(EsNegrita(estilo));
    public static SKTypeface Sans(SKFontStyle estilo) => Sans(EsNegrita(estilo));

    private static bool EsNegrita(SKFontStyle estilo) => estilo.Weight >= (int)SKFontStyleWeight.SemiBold;

    private static SKTypeface Crear(string archivo)
    {
        using var datos = SKData.CreateCopy(Archivos.GetOrAdd(archivo, Leer));
        return SKTypeface.FromData(datos) ?? throw new InvalidOperationException($"No se pudo cargar la fuente {archivo}");
    }

    private static byte[] Leer(string archivo)
    {
        using var recurso = typeof(Tipografias).Assembly.GetManifestResourceStream("Fuentes." + archivo)
                            ?? throw new InvalidOperationException($"Falta la fuente incrustada {archivo}");
        using var copia = new MemoryStream();
        recurso.CopyTo(copia);
        return copia.ToArray();
    }
}
