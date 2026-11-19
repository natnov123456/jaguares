using ClosedXML.Excel;

namespace FundacionJaguares.Application.Reportes;

// JAG-86 / FUJ-124: el mismo reporte en Excel (ClosedXML), con el mismo encabezado institucional que el PDF.
// Las propiedades del archivo (autor, última modificación) llevan el nombre de la fundación, no el de quien lo generó.
public static class GeneradorExcel
{
    public static byte[] Generar(TablaReporte t, DateTime generadoUtc)
    {
        using var libro = new XLWorkbook();
        libro.Properties.Author = PlantillaPdfInstitucional.Institucion;
        libro.Properties.LastModifiedBy = PlantillaPdfInstitucional.Institucion;
        libro.Properties.Company = PlantillaPdfInstitucional.Institucion;
        libro.Properties.Title = t.Titulo;
        libro.Properties.Created = generadoUtc;
        libro.Properties.Modified = generadoUtc;

        var hoja = libro.Worksheets.Add("Reporte");
        var ultimaColumna = Math.Max(1, t.Columnas.Count);

        hoja.Cell(1, 1).Value = PlantillaPdfInstitucional.Institucion;
        hoja.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#6B7280");
        hoja.Cell(2, 1).Value = t.Titulo;
        hoja.Cell(2, 1).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml("#B45309"));
        var fila = 3;
        if (!string.IsNullOrWhiteSpace(t.Subtitulo)) hoja.Cell(fila++, 1).Value = t.Subtitulo;
        hoja.Cell(fila++, 1).Value = $"Generado el {PlantillaPdfInstitucional.FechaGeneracion(generadoUtc)} · {t.Filas.Count} registro(s)";
        hoja.Cell(fila++, 1).Value = "Filtros: " + (t.Filtros.Count == 0 ? "Sin filtros (todos los registros)"
            : string.Join(" · ", t.Filtros.Select(f => $"{f.Etiqueta}: {f.Valor}")));
        fila++;

        var inicioTabla = fila;
        for (var c = 0; c < t.Columnas.Count; c++) hoja.Cell(fila, c + 1).Value = t.Columnas[c].Titulo;
        var encabezado = hoja.Range(fila, 1, fila, ultimaColumna);
        encabezado.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#FEF3C7"));

        foreach (var f in t.Filas)
        {
            fila++;
            for (var c = 0; c < t.Columnas.Count; c++)
            {
                var valor = c < f.Length ? f[c] ?? "" : "";
                var celda = hoja.Cell(fila, c + 1);
                // Las columnas numéricas se guardan como número para poder sumar en Excel
                if (t.Columnas[c].Numerica && decimal.TryParse(valor.Replace(" %", "").Replace("Bs. ", "").Replace(".", "").Replace(',', '.'),
                        System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var n))
                    celda.Value = n;
                else
                    celda.Value = valor;
            }
        }

        if (t.Filas.Count > 0) hoja.Range(inicioTabla, 1, fila, ultimaColumna).SetAutoFilter();
        if (t.Totales is { Count: > 0 })
        {
            fila++;
            foreach (var tot in t.Totales)
            {
                fila++;
                hoja.Cell(fila, 1).Value = tot.Etiqueta;
                hoja.Cell(fila, 2).Value = tot.Valor;
                hoja.Cell(fila, 1).Style.Font.SetBold();
            }
        }

        hoja.SheetView.FreezeRows(inicioTabla);
        hoja.Columns(1, ultimaColumna).AdjustToContents(inicioTabla, fila);
        foreach (var col in hoja.Columns(1, ultimaColumna)) if (col.Width > 60) col.Width = 60;

        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return salida.ToArray();
    }
}
