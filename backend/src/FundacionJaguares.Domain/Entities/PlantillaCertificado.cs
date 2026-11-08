namespace FundacionJaguares.Domain.Entities;

// FUJ-115: diseño de certificado (logo, firmas y texto) para un tipo de actividad
public class PlantillaCertificado
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    public string TipoActividad { get; set; } = "TALLER";
    public string Titulo { get; set; } = "CERTIFICADO";
    public string Texto { get; set; } = "";
    public string? LogoUrl { get; set; }
    public string? FirmaUnoUrl { get; set; }
    public string? FirmaUnoNombre { get; set; }
    public string? FirmaUnoCargo { get; set; }
    public string? FirmaDosUrl { get; set; }
    public string? FirmaDosNombre { get; set; }
    public string? FirmaDosCargo { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
}
