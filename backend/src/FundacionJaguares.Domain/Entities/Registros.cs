namespace FundacionJaguares.Domain.Entities;

// Una acción en la plataforma: quién, cuándo, desde dónde, qué hizo y con qué resultado (solo lectura)
public class RegistroActividad
{
    public long Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public Guid? UsuarioId { get; set; }
    public string? Email { get; set; }
    public string? Rol { get; set; }
    public string Accion { get; set; } = "";
    public string Metodo { get; set; } = "";
    public string Ruta { get; set; } = "";
    public string? Recurso { get; set; }
    public short Estado { get; set; }
    public bool Exito { get; set; }
    public string? Ip { get; set; }
    public string? Navegador { get; set; }
    public int? DuracionMs { get; set; }
}

// Aceptación de un documento legal (Términos y condiciones o Política de privacidad) en un formulario
public class Consentimiento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UsuarioId { get; set; }
    public string? Email { get; set; }
    public string Formulario { get; set; } = "";
    public string Documento { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime AceptadoEn { get; set; } = DateTime.UtcNow;
    public string? Ip { get; set; }
    public string? Navegador { get; set; }
}
