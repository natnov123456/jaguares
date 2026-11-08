namespace FundacionJaguares.Domain.Entities;

// FUJ-121: resumen mensual publicado en el sitio. Guarda solo montos agregados, nunca datos de donantes.
public class ReporteTransparencia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Anio { get; set; }
    public int Mes { get; set; }
    public decimal TotalRecaudado { get; set; }
    public int CantidadDonaciones { get; set; }
    public string? Nota { get; set; }
    public Guid? PublicadoPor { get; set; }
    public DateTime PublicadoEn { get; set; } = DateTime.UtcNow;
    public List<DestinoTransparencia> Destinos { get; set; } = [];
}

public class DestinoTransparencia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporteId { get; set; }
    public string Categoria { get; set; } = "";
    public decimal Monto { get; set; }
    public string? Descripcion { get; set; }
}
