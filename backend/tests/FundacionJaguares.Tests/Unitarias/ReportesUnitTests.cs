using System.Text;
using ClosedXML.Excel;
using FundacionJaguares.Application.Reportes;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Unitarias;

// JAG-86 + correcciones de la evaluación: filtros combinados, plantilla PDF institucional (snapshot), Excel y metadatos
public class ReportesUnitTests
{
    private record Item(string Nombre, string Correo, string Estado, DateTime? Fecha);

    // Fechas en UTC: 03:00 UTC del día 11 es todavía el día 10 en Bolivia
    private static readonly Item[] Items =
    [
        new("Ana Pérez", "ana@correo.com", "CONFIRMADA", new DateTime(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc)),
        new("José Núñez", "jose@correo.com", "CANCELADA", new DateTime(2026, 11, 11, 3, 0, 0, DateTimeKind.Utc)),
        new("Beto Ramos", "beto@ramos.bo", "CONFIRMADA", new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc)),
        new("Carla Ana Ruiz", "carla@correo.com", "ASISTIO", null),
    ];

    private static List<string> Filtrar(FiltrosReporte f) =>
        FiltroCombinado.Aplicar(Items, f, texto: x => [x.Nombre, x.Correo], estado: x => x.Estado, fecha: x => x.Fecha)
            .Select(x => x.Nombre).ToList();

    [Fact]
    public void Sin_filtros_devuelve_todo() => Assert.Equal(4, Filtrar(new FiltrosReporte()).Count);

    [Theory]
    [InlineData("ana", new[] { "Ana Pérez", "Carla Ana Ruiz" })]
    [InlineData("JOSE", new[] { "José Núñez" })]          // sin tildes ni mayúsculas
    [InlineData("nunez", new[] { "José Núñez" })]
    [InlineData("ramos.bo", new[] { "Beto Ramos" })]       // busca también en el correo
    [InlineData("   ", new[] { "Ana Pérez", "José Núñez", "Beto Ramos", "Carla Ana Ruiz" })]
    public void Texto_busca_en_todos_los_campos(string q, string[] esperado) =>
        Assert.Equal(esperado, Filtrar(new FiltrosReporte(Q: q)));

    [Fact]
    public void Texto_y_estado_se_combinan_con_Y() =>
        Assert.Equal(["Ana Pérez"], Filtrar(new FiltrosReporte(Q: "ana", Estado: "confirmada")));

    [Fact]
    public void Rango_es_inclusivo_en_hora_de_Bolivia_y_excluye_sin_fecha()
    {
        Assert.Equal(["Ana Pérez", "José Núñez"], Filtrar(new FiltrosReporte(Desde: new(2026, 11, 10), Hasta: new(2026, 11, 10))));
        Assert.Equal(["Beto Ramos"], Filtrar(new FiltrosReporte(Desde: new(2026, 11, 11))));
        Assert.Equal(["Ana Pérez", "José Núñez"], Filtrar(new FiltrosReporte(Hasta: new(2026, 11, 19))));
    }

    [Fact]
    public void Los_tres_filtros_juntos()
    {
        Assert.Equal(["Beto Ramos"], Filtrar(new FiltrosReporte(Q: "o", Estado: "CONFIRMADA", Desde: new(2026, 11, 15), Hasta: new(2026, 11, 30))));
        Assert.Empty(Filtrar(new FiltrosReporte(Q: "ana", Estado: "CANCELADA", Desde: new(2026, 11, 1))));
    }

    [Fact]
    public void Validar_rechaza_rango_invertido_y_busqueda_larga()
    {
        Assert.Throws<ArgumentException>(() => FiltroCombinado.Validar(new FiltrosReporte(Desde: new(2026, 12, 1), Hasta: new(2026, 11, 1))));
        Assert.Throws<ArgumentException>(() => FiltroCombinado.Validar(new FiltrosReporte(Q: new string('a', 101))));
        FiltroCombinado.Validar(new FiltrosReporte(Q: "ok", Desde: new(2026, 11, 1), Hasta: new(2026, 11, 1)));
    }

    [Fact]
    public void Describir_lista_solo_los_filtros_usados()
    {
        var d = FiltroCombinado.Describir(new FiltrosReporte(Q: "ana", Estado: "CONFIRMADA", Desde: new(2026, 11, 1)), "Taller", e => "Confirmada");
        Assert.Equal(["Actividad: Taller", "Búsqueda: \"ana\"", "Estado: Confirmada", "Período: 01/11/2026 al hoy"],
            d.Select(p => $"{p.Etiqueta}: {p.Valor}"));
        Assert.Empty(FiltroCombinado.Describir(new FiltrosReporte()));
    }

    // ---------- Plantilla PDF institucional ----------

    private static readonly DateTime Generado = new(2026, 11, 19, 16, 30, 0, DateTimeKind.Utc);

    private static TablaReporte Tabla(int filas, int columnas = 4) => new(
        "Inscritos y asistencia: Taller de reciclaje",
        "Curso del 01/11/2026 al 30/11/2026 · Docente: Carlos Docente",
        Enumerable.Range(0, columnas).Select(i => new ColumnaReporte(i == 0 ? "Participante" : $"Columna {i}", i == 0 ? 2 : 1, i == columnas - 1)).ToList(),
        Enumerable.Range(1, filas).Select(i => Enumerable.Range(0, columnas)
            .Select(c => c == 0 ? $"Participante número {i:00} con un nombre bastante largo para recortar" : $"{i * c}").ToArray()).ToList(),
        [new("Búsqueda", "\"ana\""), new("Estado", "Confirmada")],
        [new("Inscritos confirmados", filas.ToString())]);

    [Fact]
    public void Snapshot_reporte_de_una_pagina() =>
        Snapshot.Verificar("pdf-una-pagina", PlantillaPdfInstitucional.Describir(PlantillaPdfInstitucional.Maquetar(Tabla(3), Generado)));

    [Fact]
    public void Snapshot_reporte_de_varias_paginas_horizontal()
    {
        var m = PlantillaPdfInstitucional.Maquetar(Tabla(45, 7), Generado);
        Assert.Equal(842, m.Ancho); // más de 5 columnas: horizontal
        Assert.True(m.Paginas >= 2);
        Snapshot.Verificar("pdf-varias-paginas", PlantillaPdfInstitucional.Describir(m));
    }

    [Fact]
    public void Cada_pagina_lleva_encabezado_institucional_y_numeracion()
    {
        var m = PlantillaPdfInstitucional.Maquetar(Tabla(120), Generado);
        var textos = m.Elementos.OfType<PlantillaPdfInstitucional.Texto>().ToList();
        for (var p = 1; p <= m.Paginas; p++)
        {
            Assert.Contains(textos, t => t.Pagina == p && t.Contenido == PlantillaPdfInstitucional.Institucion);
            Assert.Contains(textos, t => t.Pagina == p && t.Contenido == $"Página {p} de {m.Paginas}");
            Assert.Contains(textos, t => t.Pagina == p && t.Contenido.StartsWith("Filtros: Búsqueda: \"ana\" · Estado: Confirmada"));
        }
        // Todas las filas aparecen una sola vez y los totales solo en la última página
        Assert.Equal(120, textos.Count(t => t.Contenido.StartsWith("Participante número")));
        Assert.All(textos.Where(t => t.Contenido.StartsWith("Inscritos confirmados")), t => Assert.Equal(m.Paginas, t.Pagina));
    }

    [Fact]
    public void Recortar_no_se_sale_de_la_columna()
    {
        Assert.Equal("Hola", PlantillaPdfInstitucional.Recortar("Hola", 100, 8.5f));
        var largo = PlantillaPdfInstitucional.Recortar(new string('x', 200), 50, 8.5f);
        Assert.EndsWith("…", largo);
        Assert.True(largo.Length <= 12);
        Assert.Equal("a b", PlantillaPdfInstitucional.Recortar("a\nb", 100, 8));
    }

    [Fact]
    public void Pdf_valido_sin_datos_de_quien_lo_genero_en_los_metadatos()
    {
        var pdf = PlantillaPdfInstitucional.Generar(Tabla(5), Generado);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        var crudo = Encoding.Latin1.GetString(pdf);
        Assert.Contains("/Author", crudo);                 // el autor es la institución (no la persona)
        Assert.Contains("/Title", crudo);
        Assert.DoesNotContain("directora@jaguares.org", crudo);
        Assert.DoesNotContain("Directora Ejecutiva", crudo);
    }

    [Fact]
    public void Excel_tiene_encabezado_filas_y_propiedades_institucionales()
    {
        var xlsx = GeneradorExcel.Generar(Tabla(20), Generado);
        using var libro = new XLWorkbook(new MemoryStream(xlsx));
        var hoja = libro.Worksheet("Reporte");
        Assert.Equal(PlantillaPdfInstitucional.Institucion, hoja.Cell(1, 1).GetString());
        Assert.StartsWith("Inscritos y asistencia", hoja.Cell(2, 1).GetString());
        var encabezado = hoja.RowsUsed().First(r => r.Cell(1).GetString() == "Participante").RowNumber();
        Assert.Equal(20, hoja.RowsUsed().Count(r => r.RowNumber() > encabezado && r.Cell(1).GetString().StartsWith("Participante número")));
        Assert.Equal(PlantillaPdfInstitucional.Institucion, libro.Properties.Author);
        Assert.Equal(PlantillaPdfInstitucional.Institucion, libro.Properties.LastModifiedBy);
        Assert.True(hoja.Cell(encabezado + 1, 4).DataType == XLDataType.Number); // columna numérica como número
    }
}
