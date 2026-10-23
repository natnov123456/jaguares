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
}