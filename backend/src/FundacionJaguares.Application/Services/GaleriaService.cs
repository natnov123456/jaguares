using System.Text.RegularExpressions;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

// Archivo recibido por la API, sin depender de ASP.NET en esta capa
public record ArchivoSubido(string Nombre, long Tamano, Stream Contenido);

// FUJ-126: álbumes con imágenes (JPG/PNG ≤ 5 MB, con miniatura optimizada) y enlaces de video
public interface IGaleriaService
{
    Task<List<AlbumResumenDto>> ListarAlbumesAsync(bool incluirNoPublicados);
    Task<AlbumDetalleDto?> ObtenerAlbumAsync(Guid id, bool incluirNoPublicados);
    Task<AlbumResumenDto> CrearAlbumAsync(GuardarAlbumDto dto, Guid creadoPor);
    Task<AlbumResumenDto> ActualizarAlbumAsync(Guid id, GuardarAlbumDto dto);
    Task EliminarAlbumAsync(Guid id);
    Task<ResultadoSubidaDto> SubirImagenesAsync(Guid albumId, IReadOnlyList<ArchivoSubido> archivos);
    Task<GaleriaItemDto> AgregarVideoAsync(Guid albumId, AgregarVideoDto dto);
    Task EliminarItemAsync(Guid itemId);
}

public partial class GaleriaService : IGaleriaService
{
    public const long MaxBytes = ProcesadorImagen.MaxBytes;
    public const int LadoMaximo = 1920;   // imagen ampliada
    public const int LadoMiniatura = 400; // miniatura cuadrada para la grilla

    private readonly AppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;

    public GaleriaService(AppDbContext db, IAlmacenamientoArchivos archivos) { _db = db; _archivos = archivos; }

    // ---------- Álbumes ----------

    public async Task<List<AlbumResumenDto>> ListarAlbumesAsync(bool incluirNoPublicados)
    {
        var albumes = await _db.Albumes
            .Where(a => incluirNoPublicados || a.Publicado)
            .OrderByDescending(a => a.CreadoEn)
            .ToListAsync();
        var ids = albumes.Select(a => a.Id).ToList();
        var items = await _db.Galeria.Where(g => g.AlbumId != null && ids.Contains(g.AlbumId.Value))
            .OrderBy(g => g.Orden).ToListAsync();
        var porAlbum = items.GroupBy(i => i.AlbumId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        return albumes.Select(a => Resumen(a, porAlbum.GetValueOrDefault(a.Id) ?? [])).ToList();
    }

    public async Task<AlbumDetalleDto?> ObtenerAlbumAsync(Guid id, bool incluirNoPublicados)
    {
        var a = await _db.Albumes.FirstOrDefaultAsync(x => x.Id == id && (incluirNoPublicados || x.Publicado));
        if (a is null) return null;
        var items = await _db.Galeria.Where(g => g.AlbumId == id).OrderBy(g => g.Orden).ToListAsync();
        return new AlbumDetalleDto(a.Id, a.Titulo, a.Descripcion, a.Publicado, a.CreadoEn, items.Select(ADto).ToList());
    }

    public async Task<AlbumResumenDto> CrearAlbumAsync(GuardarAlbumDto dto, Guid creadoPor)
    {
        var (titulo, descripcion) = ValidarAlbum(dto);
        var a = new Album { Titulo = titulo, Descripcion = descripcion, Publicado = dto.Publicado, CreadoPor = creadoPor };
        _db.Albumes.Add(a);
        await _db.SaveChangesAsync();
        return Resumen(a, []);
    }

    public async Task<AlbumResumenDto> ActualizarAlbumAsync(Guid id, GuardarAlbumDto dto)
    {
        var (titulo, descripcion) = ValidarAlbum(dto);
        var a = await _db.Albumes.FindAsync(id) ?? throw new KeyNotFoundException("Álbum no encontrado");
        a.Titulo = titulo;
        a.Descripcion = descripcion;
        a.Publicado = dto.Publicado;
        a.ActualizadoEn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var items = await _db.Galeria.Where(g => g.AlbumId == id).OrderBy(g => g.Orden).ToListAsync();
        return Resumen(a, items);
    }

    public async Task EliminarAlbumAsync(Guid id)
    {
        var a = await _db.Albumes.FindAsync(id) ?? throw new KeyNotFoundException("Álbum no encontrado");
        var items = await _db.Galeria.Where(g => g.AlbumId == id).ToListAsync();
        _db.Galeria.RemoveRange(items);
        _db.Albumes.Remove(a);
        await _db.SaveChangesAsync();
        foreach (var i in items) BorrarArchivos(i);
    }

    // ---------- Imágenes ----------

    public async Task<ResultadoSubidaDto> SubirImagenesAsync(Guid albumId, IReadOnlyList<ArchivoSubido> archivos)
    {
        if (!await _db.Albumes.AnyAsync(a => a.Id == albumId)) throw new KeyNotFoundException("Álbum no encontrado");
        if (archivos.Count == 0) throw new ArgumentException("No se recibió ningún archivo");

        var subidas = new List<GaleriaItemDto>();
        var errores = new List<ErrorSubidaDto>();
        var orden = await SiguienteOrdenAsync(albumId);

        foreach (var archivo in archivos)
        {
            var nombre = Path.GetFileName(archivo.Nombre);
            try
            {
                var item = await ProcesarImagenAsync(albumId, archivo, orden);
                _db.Galeria.Add(item);
                await _db.SaveChangesAsync();
                subidas.Add(ADto(item));
                orden++;
            }
            catch (ArgumentException ex) { errores.Add(new ErrorSubidaDto(nombre, ex.Message)); }
        }
        return new ResultadoSubidaDto(subidas, errores);
    }

    private async Task<GaleriaItem> ProcesarImagenAsync(Guid albumId, ArchivoSubido archivo, int orden)
    {
        var (bytes, esPng) = await ProcesadorImagen.LeerValidadoAsync(archivo);
        var r = ProcesadorImagen.Procesar(bytes, esPng, LadoMaximo, LadoMiniatura);

        var id = Guid.NewGuid().ToString("N");
        var carpeta = $"galeria/{albumId:N}";
        string urlImagen;
        using (var datos = new MemoryStream(r.Imagen))
            urlImagen = await _archivos.GuardarAsync(carpeta, id + r.Extension, datos);
        string urlMini;
        using (var datos = new MemoryStream(r.Miniatura))
            urlMini = await _archivos.GuardarAsync(carpeta, id + "_min.jpg", datos);

        var titulo = Path.GetFileNameWithoutExtension(archivo.Nombre);
        return new GaleriaItem
        {
            AlbumId = albumId, Tipo = "IMAGEN",
            Titulo = titulo.Length > 200 ? titulo[..200] : titulo,
            ImagenUrl = urlImagen, MiniaturaUrl = urlMini, Orden = orden
        };
    }

    // ---------- Videos ----------

    [GeneratedRegex(@"^https?://(?:www\.|m\.)?(?:youtube\.com/(?:watch\?(?:.*&)?v=|shorts/|embed/|live/)|youtu\.be/)([A-Za-z0-9_-]{11})(?:[?&#/].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex YouTube();

    [GeneratedRegex(@"^https?://(?:www\.|player\.)?vimeo\.com/(?:video/)?(\d{6,12})(?:[?#/].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex Vimeo();

    // Solo se aceptan YouTube y Vimeo; se guarda la URL de inserción normalizada (no la que pegó el usuario)
    public static (string Embed, string? Miniatura) NormalizarVideo(string? url)
    {
        var u = (url ?? "").Trim();
        var yt = YouTube().Match(u);
        if (yt.Success)
        {
            var id = yt.Groups[1].Value;
            return ($"https://www.youtube-nocookie.com/embed/{id}", $"https://i.ytimg.com/vi/{id}/hqdefault.jpg");
        }
        var vm = Vimeo().Match(u);
        if (vm.Success) return ($"https://player.vimeo.com/video/{vm.Groups[1].Value}", null);
        throw new ArgumentException("Enlace no válido: usa un video de YouTube o Vimeo");
    }

    public async Task<GaleriaItemDto> AgregarVideoAsync(Guid albumId, AgregarVideoDto dto)
    {
        if (!await _db.Albumes.AnyAsync(a => a.Id == albumId)) throw new KeyNotFoundException("Álbum no encontrado");
        var (embed, miniatura) = NormalizarVideo(dto.Url);
        var titulo = string.IsNullOrWhiteSpace(dto.Titulo) ? null : dto.Titulo.Trim();
        if (titulo?.Length > 200) throw new ArgumentException("El título no puede superar 200 caracteres");

        var item = new GaleriaItem
        {
            AlbumId = albumId, Tipo = "VIDEO", Titulo = titulo, VideoUrl = embed, MiniaturaUrl = miniatura,
            Orden = await SiguienteOrdenAsync(albumId)
        };
        _db.Galeria.Add(item);
        await _db.SaveChangesAsync();
        return ADto(item);
    }

    public async Task EliminarItemAsync(Guid itemId)
    {
        var item = await _db.Galeria.FindAsync(itemId) ?? throw new KeyNotFoundException("Elemento no encontrado");
        _db.Galeria.Remove(item);
        await _db.SaveChangesAsync();
        BorrarArchivos(item);
    }

    // ---------- auxiliares ----------

    private static (string Titulo, string? Descripcion) ValidarAlbum(GuardarAlbumDto dto)
    {
        var titulo = (dto.Titulo ?? "").Trim();
        if (titulo.Length == 0) throw new ArgumentException("El álbum necesita un título");
        if (titulo.Length > 200) throw new ArgumentException("El título no puede superar 200 caracteres");
        var descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        if (descripcion?.Length > 2000) throw new ArgumentException("La descripción no puede superar 2000 caracteres");
        return (titulo, descripcion);
    }

    private async Task<int> SiguienteOrdenAsync(Guid albumId) =>
        (await _db.Galeria.Where(g => g.AlbumId == albumId).MaxAsync(g => (int?)g.Orden) ?? -1) + 1;

    private void BorrarArchivos(GaleriaItem i)
    {
        _archivos.Eliminar(i.ImagenUrl);
        _archivos.Eliminar(i.MiniaturaUrl);
    }

    private static GaleriaItemDto ADto(GaleriaItem g) =>
        new(g.Id, g.Tipo, g.Titulo, g.ImagenUrl, g.MiniaturaUrl, g.VideoUrl, g.Orden);

    private static AlbumResumenDto Resumen(Album a, List<GaleriaItem> items) =>
        new(a.Id, a.Titulo, a.Descripcion, a.Publicado,
            items.FirstOrDefault(i => i.MiniaturaUrl != null)?.MiniaturaUrl,
            items.Count(i => i.Tipo == "IMAGEN"), items.Count(i => i.Tipo == "VIDEO"), a.CreadoEn);
}
