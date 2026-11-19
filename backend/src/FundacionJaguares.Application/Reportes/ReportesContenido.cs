using FundacionJaguares.Application.Services;

namespace FundacionJaguares.Application.Reportes;

// Evaluación: exportación con filtros combinados en las pantallas de contenido del sitio
public class ReportesContenido(IContenidoService contenido, IGaleriaService galeria, ITransparenciaService transparencia) : IProveedorReportes
{
    public IEnumerable<DefinicionReporte> Reportes() =>
    [
        new("noticias", "Noticias", ["DIRECTORA"], NoticiasAsync),
        new("galeria", "Álbumes de la galería", ["DIRECTORA"], GaleriaAsync),
        new("transparencia", "Transparencia: destino de las donaciones", ["DIRECTORA", "CONTADOR"], TransparenciaAsync),
    ];

    private async Task<TablaReporte> NoticiasAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        var lista = await contenido.ListarTodasAsync();
        // estado = PUBLICADA | BORRADOR ; fechas = publicación (o creación si es borrador)
        var filtrados = FiltroCombinado.Aplicar(lista, f,
            texto: n => [n.Titulo, n.Resumen, n.Contenido], estado: n => n.Publicada ? "PUBLICADA" : "BORRADOR",
            fecha: n => n.PublicadoEn ?? n.CreadoEn).ToList();
        return new TablaReporte("Noticias", "Noticias publicadas y borradores del sitio",
            [new("Título", 2.6f), new("Resumen", 3.2f), new("Estado", 0.9f), new("Fecha", 1f), new("Con imagen", 0.8f)],
            filtrados.Select(n => new[]
            {
                n.Titulo, n.Resumen ?? "—", n.Publicada ? "Publicada" : "Borrador", Fmt.Fecha(n.PublicadoEn ?? n.CreadoEn),
                string.IsNullOrWhiteSpace(n.ImagenUrl) ? "No" : "Sí"
            }).ToList(),
            FiltroCombinado.Describir(f, null, e => e == "PUBLICADA" ? "Publicadas" : "Borradores"),
            [new("Noticias", Fmt.Entero(filtrados.Count)), new("Publicadas", Fmt.Entero(filtrados.Count(n => n.Publicada)))]);
    }

    private async Task<TablaReporte> GaleriaAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        var lista = await galeria.ListarAlbumesAsync(incluirNoPublicados: true);
        var filtrados = FiltroCombinado.Aplicar(lista, f,
            texto: a => [a.Titulo, a.Descripcion], estado: a => a.Publicado ? "PUBLICADO" : "OCULTO", fecha: a => a.CreadoEn).ToList();
        return new TablaReporte("Álbumes de la galería", "Álbumes con la cantidad de fotos y videos",
            [new("Álbum", 2.6f), new("Descripción", 3f), new("Estado", 0.9f), new("Fotos", 0.7f, true), new("Videos", 0.7f, true),
             new("Creado el", 1f)],
            filtrados.Select(a => new[]
            {
                a.Titulo, a.Descripcion ?? "—", a.Publicado ? "Publicado" : "Oculto", Fmt.Entero(a.CantidadImagenes),
                Fmt.Entero(a.CantidadVideos), Fmt.Fecha(a.CreadoEn)
            }).ToList(),
            FiltroCombinado.Describir(f, null, e => e == "PUBLICADO" ? "Publicados" : "Ocultos"),
            [new("Álbumes", Fmt.Entero(filtrados.Count)), new("Fotos", Fmt.Entero(filtrados.Sum(a => a.CantidadImagenes))),
             new("Videos", Fmt.Entero(filtrados.Sum(a => a.CantidadVideos)))]);
    }

    private async Task<TablaReporte> TransparenciaAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        // Solo lo ya publicado en el sitio (sin datos de donantes); desde/hasta eligen los meses
        var meses = (await transparencia.ListarMesesPublicadosAsync())
            .Where(m => FiltroCombinado.EnRango(new DateOnly(m.Anio, m.Mes, 1), f.Desde is { } d ? new DateOnly(d.Year, d.Month, 1) : null, f.Hasta))
            .OrderBy(m => m.Anio).ThenBy(m => m.Mes).ToList();
        var filas = new List<string[]>();
        decimal recaudado = 0;
        foreach (var m in meses)
        {
            var r = await transparencia.ObtenerPublicadoAsync(m.Anio, m.Mes);
            if (r is null) continue;
            recaudado += r.TotalRecaudado;
            filas.AddRange(r.Destinos.Where(dst => FiltroCombinado.CoincideTexto(f.Q, dst.Categoria, dst.Descripcion))
                .Select(dst => new[]
                {
                    $"{r.Mes:00}/{r.Anio}", Fmt.Bs(r.TotalRecaudado), dst.Categoria, dst.Descripcion ?? "—", Fmt.Bs(dst.Monto),
                    Fmt.Pct((double)dst.Porcentaje)
                }));
        }
        return new TablaReporte("Transparencia: destino de las donaciones", "Meses publicados en el sitio, solo montos agregados",
            [new("Mes", 0.8f), new("Recaudado", 1.1f, true), new("Categoría", 1.6f), new("Descripción", 2.6f), new("Monto", 1.1f, true),
             new("% del mes", 0.8f, true)],
            filas, FiltroCombinado.Describir(f),
            [new("Meses publicados", Fmt.Entero(meses.Count)), new("Total recaudado", Fmt.Bs(recaudado))]);
    }
}
