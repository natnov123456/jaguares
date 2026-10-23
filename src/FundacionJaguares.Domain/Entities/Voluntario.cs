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
}