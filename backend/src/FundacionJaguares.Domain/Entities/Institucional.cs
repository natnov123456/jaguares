namespace FundacionJaguares.Domain.Entities;

// FUJ-127: contenido de "Quiénes somos" (una sola fila, editable por la Directora)
public class PaginaInstitucional
{
    public int Id { get; set; } = 1;
    public string? Mision { get; set; }
    public string? Vision { get; set; }
    public string? Historia { get; set; }
    public string EquipoJson { get; set; } = "[]"; // columna JSONB: [{ nombre, cargo, descripcion }]
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? EmailContacto { get; set; }
    public string? Horario { get; set; }
    public Guid? ActualizadoPor { get; set; }
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
}

// FUJ-127: consulta enviada desde el formulario de contacto
public class ConsultaContacto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Asunto { get; set; }
    public string Mensaje { get; set; } = "";
    public string? Ip { get; set; }
    public bool Enviada { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
