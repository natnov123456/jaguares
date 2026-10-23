namespace FundacionJaguares.Domain.Entities;

public enum Rol { VISITANTE, PARTICIPANTE, DOCENTE, ENCARGADO_EVENTOS, VOLUNTARIO, DIRECTORA }

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NombreCompleto { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telefono { get; set; }
    public string PasswordHash { get; set; } = "";
    public Rol Rol { get; set; } = Rol.PARTICIPANTE;
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
}