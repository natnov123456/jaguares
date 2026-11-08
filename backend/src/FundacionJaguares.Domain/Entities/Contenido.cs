namespace FundacionJaguares.Domain.Entities;

public class Noticia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Titulo { get; set; } = "";
    public string? Resumen { get; set; }
    public string Contenido { get; set; } = "";
    public string? ImagenUrl { get; set; }
    public bool Publicada { get; set; }
    public Guid? AutorId { get; set; }
    public DateTime? PublicadoEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

// FUJ-126: álbum de la galería multimedia
public class Album
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Titulo { get; set; } = "";
    public string? Descripcion { get; set; }
    public bool Publicado { get; set; }
    public Guid? CreadoPor { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
}

public class GaleriaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AlbumId { get; set; }
    public string Tipo { get; set; } = "IMAGEN"; // IMAGEN | VIDEO
    public string? Titulo { get; set; }
    public string? ImagenUrl { get; set; }       // imagen optimizada (tipo IMAGEN)
    public string? MiniaturaUrl { get; set; }    // miniatura para la grilla
    public string? VideoUrl { get; set; }        // URL de inserción de YouTube/Vimeo (tipo VIDEO)
    public int Orden { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

public class NotificacionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Destinatario { get; set; } = "";
    public string Canal { get; set; } = "EMAIL";
    public string? Asunto { get; set; }
    public string? Contenido { get; set; }
    public DateTime EnviadoEn { get; set; } = DateTime.UtcNow;
    public bool Exito { get; set; } = true;
}