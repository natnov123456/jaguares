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

// FUJ-113: inscripción a un evento con código QR único para el check-in
public class InscripcionEvento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventoId { get; set; }
    public Guid ParticipanteId { get; set; }
    public string CodigoQr { get; set; } = "";
    public string Estado { get; set; } = "CONFIRMADA"; // CONFIRMADA | CANCELADA | ASISTIO
    public DateTime InscritoEn { get; set; } = DateTime.UtcNow;
    public DateTime? CheckinEn { get; set; }
    public DateTime? RecordatorioEnviadoEn { get; set; } // FUJ-119: recordatorio 24 h antes ya enviado
}

public class AsistenciaEvento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventoId { get; set; }
    public Guid ParticipanteId { get; set; }
    public DateTime RegistradoEn { get; set; } = DateTime.UtcNow;
    public Guid? RegistradoPor { get; set; }
}