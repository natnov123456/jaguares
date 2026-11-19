namespace FundacionJaguares.Domain.Entities;

// FUJ-122: CONTADOR y COORDINADOR_VOLUNTARIADO se agregan al final (el enum de PostgreSQL se amplía con ADD VALUE)
public enum Rol { VISITANTE, PARTICIPANTE, DOCENTE, ENCARGADO_EVENTOS, VOLUNTARIO, DIRECTORA, CONTADOR, COORDINADOR_VOLUNTARIADO }

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

// FUJ-122: catálogo de roles (nombre visible y qué puede hacer cada uno)
public class RolCatalogo
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public bool Interno { get; set; }
}

// FUJ-122: enlace de un solo uso para definir la contraseña. Solo se guarda el hash SHA-256 del token.
public class TokenContrasena
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiraEn { get; set; }
    public DateTime? UsadoEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
