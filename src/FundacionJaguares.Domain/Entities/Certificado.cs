namespace FundacionJaguares.Domain.Entities;

public class Certificado
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CodigoUnico { get; set; } = "";
    public Guid? CursoId { get; set; }
    public Guid ParticipanteId { get; set; }
    public string NombreParticipante { get; set; } = "";
    public string NombreCurso { get; set; } = "";
    public DateTime EmitidoEn { get; set; } = DateTime.UtcNow;
    public string? UrlPdf { get; set; }
    public bool Valido { get; set; } = true;
}