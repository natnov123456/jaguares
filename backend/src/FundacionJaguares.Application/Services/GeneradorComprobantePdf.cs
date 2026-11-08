using System.Globalization;
using FundacionJaguares.Domain.Entities;
using SkiaSharp;

namespace FundacionJaguares.Application.Services;

public record DatosComprobante(long Numero, DateTime FechaUtc, decimal Monto, string Moneda, MedioPago Medio,
                               string? Referencia, string Donante);

// FUJ-116: comprobante de donación en PDF (A5 horizontal). Se genera a partir de los datos guardados,
// por eso no se almacena el archivo: contiene datos personales y /uploads es público.
public static class GeneradorComprobantePdf
{
    private const float Ancho = 595, Alto = 420, Margen = 48, ColumnaValor = 190;

    private static readonly SKColor ColorAcento = new(0xB4, 0x53, 0x09);
    private static readonly SKColor ColorTexto = new(0x1F, 0x29, 0x37);
    private static readonly SKColor ColorSuave = new(0x6B, 0x72, 0x80);
    private static readonly SKColor ColorLinea = new(0xE5, 0xE7, 0xEB);

    private static readonly NumberFormatInfo FormatoMonto = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    public static string Numero(long numero) => numero.ToString("D6", CultureInfo.InvariantCulture);

    public static string Monto(decimal monto, string moneda) =>
        $"{(moneda.Trim() == "BOB" ? "Bs." : moneda.Trim())} {monto.ToString("N2", FormatoMonto)}";

    private static string Medio(MedioPago medio) => medio switch
    {
        MedioPago.EFECTIVO => "Efectivo",
        MedioPago.TRANSFERENCIA => "Transferencia bancaria",
        MedioPago.QR => "Código QR",
        MedioPago.PASARELA => "Pasarela de pago",
        _ => medio.ToString()
    };

    public static byte[] Generar(DatosComprobante d)
    {
        using var salida = new MemoryStream();
        using (var wstream = new SKManagedWStream(salida))
        using (var doc = SKDocument.CreatePdf(wstream))
        {
            using var normal = SKTypeface.FromFamilyName("Georgia", SKFontStyle.Normal) ?? SKTypeface.Default;
            using var negrita = SKTypeface.FromFamilyName("Georgia", SKFontStyle.Bold) ?? SKTypeface.Default;
            var c = doc.BeginPage(Ancho, Alto);

            using var barra = new SKPaint { Color = ColorAcento };
            c.DrawRect(SKRect.Create(0, 0, Ancho, 8), barra);

            Texto(c, "Fundación Unión de los Jaguares", Margen, 50, Ancho - 2 * Margen, 12, normal, ColorSuave);
            Texto(c, "COMPROBANTE DE DONACIÓN", Margen, 80, 340, 20, negrita, ColorAcento);
            TextoDerecha(c, $"N° {Numero(d.Numero)}", Ancho - Margen, 80, 14, negrita, ColorTexto);

            float y = 124;
            Fila(c, "Fecha", GeneradorCertificadoPdf.FechaLarga(d.FechaUtc), ref y, normal);
            Fila(c, "Donante", d.Donante, ref y, normal);
            Fila(c, "Medio de pago", Medio(d.Medio), ref y, normal);
            if (!string.IsNullOrWhiteSpace(d.Referencia)) Fila(c, "Referencia", d.Referencia, ref y, normal);

            y += 14;
            Texto(c, "Monto donado", Margen, y + 8, ColumnaValor - Margen - 8, 12, normal, ColorSuave);
            Texto(c, Monto(d.Monto, d.Moneda), ColumnaValor, y + 10, Ancho - Margen - ColumnaValor, 24, negrita, ColorTexto);

            Texto(c, "Gracias por tu aporte. Este comprobante respalda tu donación a la fundación.",
                  Margen, Alto - 34, Ancho - 2 * Margen, 10, normal, ColorSuave);

            doc.EndPage();
            doc.Close();
        }
        return salida.ToArray();
    }

    private static void Fila(SKCanvas c, string etiqueta, string valor, ref float y, SKTypeface tipo)
    {
        Texto(c, etiqueta, Margen, y, ColumnaValor - Margen - 8, 12, tipo, ColorSuave);
        Texto(c, valor, ColumnaValor, y, Ancho - Margen - ColumnaValor, 13, tipo, ColorTexto);
        using var trazo = new SKPaint { Color = ColorLinea, IsStroke = true, StrokeWidth = 0.75f, IsAntialias = true };
        c.DrawLine(Margen, y + 10, Ancho - Margen, y + 10, trazo);
        y += 32;
    }

    // Una sola línea; reduce la letra si no cabe en el ancho disponible
    private static void Texto(SKCanvas c, string texto, float x, float y, float anchoMax, float tamano, SKTypeface tipo, SKColor color)
    {
        using var font = new SKFont(tipo, tamano);
        var medida = font.MeasureText(texto);
        if (medida > anchoMax) font.Size = tamano * anchoMax / medida;
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        c.DrawText(texto, x, y, SKTextAlign.Left, font, paint);
    }

    private static void TextoDerecha(SKCanvas c, string texto, float x, float y, float tamano, SKTypeface tipo, SKColor color)
    {
        using var font = new SKFont(tipo, tamano);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        c.DrawText(texto, x, y, SKTextAlign.Right, font, paint);
    }
}
