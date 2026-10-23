namespace FundacionJaguares.Domain.Entities;

public class Curso
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Titulo { get; set; } = "";
    public string? Descripcion { get; set; }
    public Guid? DocenteId { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public int CupoMaximo { get; set; }
    public int CupoDisponible { get; set; }
    public int AsistenciaMinimaPct { get; set; } = 80;
    public string Estado { get; set; } = "BORRADOR";
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

public class Inscripcion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CursoId { get; set; }
    public Guid ParticipanteId { get; set; }
    public string Estado { get; set; } = "CONFIRMADA";
    public DateTime FechaInscripcion { get; set; } = DateTime.UtcNow;
}