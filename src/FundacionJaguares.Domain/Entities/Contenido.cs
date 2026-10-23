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

public class GaleriaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Titulo { get; set; }
    public string ImagenUrl { get; set; } = "";
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