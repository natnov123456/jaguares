namespace FundacionJaguares.Domain.Entities;

public enum MedioPago { EFECTIVO, TRANSFERENCIA, QR, PASARELA }

public class Donante
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public bool EsAnonimo { get; set; }
}

public class Donacion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DonanteId { get; set; }
    public decimal Monto { get; set; }
    public string Moneda { get; set; } = "BOB";
    public MedioPago Medio { get; set; }
    public string? Referencia { get; set; }
    public string Estado { get; set; } = "CONFIRMADA";
    public Guid? RegistradoPor { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public long NumeroComprobante { get; set; }
    public Guid? UsuarioId { get; set; }
    public DateTime? ComprobanteEnviadoEn { get; set; }
    public Guid? CampanaId { get; set; }
    // Donación en línea: quién confirmó en el banco que el dinero llegó (o la rechazó), cuándo y por qué
    public Guid? VerificadoPor { get; set; }
    public DateTime? VerificadoEn { get; set; }
    public string? MotivoRechazo { get; set; }
}

// Estados de una donación. Solo las CONFIRMADAS suman y tienen comprobante de donación.
public static class EstadoDonacion
{
    public const string Pendiente = "PENDIENTE", Confirmada = "CONFIRMADA", Rechazada = "RECHAZADA", Anulada = "ANULADA";
}

// Foto o PDF del comprobante del banco que sube el donante (privado: solo Contabilidad y Dirección)
public class ComprobantePago
{
    public Guid DonacionId { get; set; }
    public byte[] Contenido { get; set; } = [];
    public string Tipo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}