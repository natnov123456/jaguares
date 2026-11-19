namespace FundacionJaguares.Domain.Entities;

// JAG-86 / evaluación (logging): registro de cada reporte exportado (PDF o Excel) y de los errores al generarlo.
// Guarda quién lo pidió (id y rol, no su correo), qué reporte, con qué filtros y cuántas filas salieron.
public class Exportacion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string Rol { get; set; } = "";
    public string Reporte { get; set; } = "";
    public string Formato { get; set; } = "";
    public string Filtros { get; set; } = "{}";   // JSON
    public int Filas { get; set; }
    public bool Exito { get; set; }
    public string? Error { get; set; }
    public int DuracionMs { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
