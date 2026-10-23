namespace FundacionJaguares.Domain.Entities;

public class Evento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Titulo { get; set; } = "";
    public string? Descripcion { get; set; }
    public string? Lugar { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public Guid? EncargadoId { get; set; }
    public string Estado { get; set; } = "BORRADOR";
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

public class AsistenciaEvento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventoId { get; set; }
    public Guid ParticipanteId { get; set; }
    public DateTime RegistradoEn { get; set; } = DateTime.UtcNow;
    public Guid? RegistradoPor { get; set; }
}