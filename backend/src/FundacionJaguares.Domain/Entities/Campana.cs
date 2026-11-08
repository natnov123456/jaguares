namespace FundacionJaguares.Domain.Entities;

// FUJ-117: campaña de donación con meta y fecha límite
public class Campana
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public decimal Meta { get; set; }
    public DateOnly FechaLimite { get; set; }
    public bool Activa { get; set; } = true;
    public Guid? CreadoPor { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
