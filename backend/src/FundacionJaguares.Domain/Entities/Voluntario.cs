namespace FundacionJaguares.Domain.Entities;

public class Voluntario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UsuarioId { get; set; }
    public string NombreCompleto { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telefono { get; set; }
    public string Estado { get; set; } = "ACTIVO";
    public DateTime PostuladoEn { get; set; } = DateTime.UtcNow;
}

public class ParticipacionVoluntario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VoluntarioId { get; set; }
    public Guid? EventoId { get; set; }
    public string? Descripcion { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    // FUJ-118: horas aportadas, curso apoyado y asignación que originó la fila (si la hubo)
    public decimal? Horas { get; set; }
    public Guid? CursoId { get; set; }
    public Guid? AsignacionId { get; set; }
}

// FUJ-118: voluntario asignado por el coordinador a un evento o a un curso (exactamente uno)
public class AsignacionVoluntario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VoluntarioId { get; set; }
    public Guid? EventoId { get; set; }
    public Guid? CursoId { get; set; }
    public string? Tarea { get; set; }
    public string Estado { get; set; } = "ASIGNADA"; // ASIGNADA | COMPLETADA | CANCELADA
    public decimal? Horas { get; set; }
    public Guid? AsignadoPor { get; set; }
    public DateTime AsignadoEn { get; set; } = DateTime.UtcNow;
    public Guid? HorasRegistradasPor { get; set; }
    public DateTime? HorasRegistradasEn { get; set; }
}