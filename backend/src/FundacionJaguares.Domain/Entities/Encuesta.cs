namespace FundacionJaguares.Domain.Entities;

// FUJ-120: encuesta de satisfacción de un curso o de un evento (exactamente uno)
public class Encuesta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CursoId { get; set; }
    public Guid? EventoId { get; set; }
    public Guid? CreadaPor { get; set; }
    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
}

public class EncuestaPregunta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EncuestaId { get; set; }
    public short Orden { get; set; }
    public string Texto { get; set; } = "";
    public string Tipo { get; set; } = "ESCALA"; // ESCALA (1 a 5) | TEXTO
}

// Enlace personal de un asistente para responder la encuesta una sola vez
public class EncuestaInvitacion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EncuestaId { get; set; }
    public Guid ParticipanteId { get; set; }
    public string Token { get; set; } = "";
    public DateTime EnviadaEn { get; set; } = DateTime.UtcNow;
    public DateTime? RespondidaEn { get; set; }
}

public class EncuestaRespuesta
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvitacionId { get; set; }
    public Guid PreguntaId { get; set; }
    public short? Valor { get; set; }
    public string? Texto { get; set; }
}
