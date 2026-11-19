using FundacionJaguares.Application.Services;

namespace FundacionJaguares.Application.Reportes;

// JAG-84 + evaluación: el panel de indicadores también se exporta (PDF institucional / Excel).
// El mes se indica con el filtro "desde" (cualquier día del mes); sin él, el mes actual.
public class ReportesIndicadores(IIndicadorService indicadores) : IProveedorReportes
{
    public IEnumerable<DefinicionReporte> Reportes() =>
    [
        new("indicadores", "Panel de indicadores", ["DIRECTORA"], IndicadoresAsync),
    ];

    private async Task<TablaReporte> IndicadoresAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        var (anio, mes) = f.Desde is { } d ? (d.Year, d.Month) : IndicadorService.Parsear(null, DateTime.UtcNow);
        var i = await indicadores.ObtenerAsync(anio, mes);
        var filas = new List<string[]>
        {
            new[] { "Inscritos del mes", Fmt.Entero(i.InscritosMes), $"{i.InscritosCursos} en cursos y {i.InscritosEventos} en eventos" },
            new[] { "Asistencia promedio", Fmt.Pct(i.AsistenciaPromedio), "Clases de cursos y eventos realizados en el mes" },
            new[] { "Certificados emitidos", Fmt.Entero(i.CertificadosEmitidos), "" },
            new[] { "Donaciones del mes", Fmt.Bs(i.DonacionesMes),
                    $"{i.CantidadDonaciones} aporte(s) · mes anterior {Fmt.Bs(i.DonacionesMesAnterior)} · variación {(i.VariacionDonacionesPct is null ? "—" : (i.VariacionDonacionesPct > 0 ? "+" : "") + Fmt.Pct(i.VariacionDonacionesPct))}" },
            new[] { "Voluntarios activos", Fmt.Entero(i.VoluntariosActivos), $"{i.VoluntariosConActividad} participaron en actividades del mes" },
            new[] { "Satisfacción promedio", i.SatisfaccionPromedio is null ? "—" : Fmt.Num((decimal)i.SatisfaccionPromedio.Value) + " / 5",
                    $"{i.RespuestasEncuestas} encuesta(s) respondida(s)" },
        };
        filas.AddRange(i.DonacionesUltimosMeses.Select(m => new[] { $"Donaciones {m.Mes}", Fmt.Bs(m.Total), "Serie de los últimos 6 meses" }));
        filas.AddRange(i.InscritosPorCurso.Select(c => new[] { $"Inscritos: {c.Curso}", Fmt.Entero(c.Inscritos), "Curso activo en el mes" }));
        return new TablaReporte($"Panel de indicadores · {i.Mes}", "Indicadores clave de la Fundación del mes seleccionado",
            [new("Indicador", 2f), new("Valor", 1.2f, true), new("Detalle", 3.2f)], filas,
            [new("Mes", i.Mes)]);
    }
}
