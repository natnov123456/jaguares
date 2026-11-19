namespace FundacionJaguares.Domain.Entities;

// JAG-88 / FUJ-125: registro de solo lectura de cada alta, edición o anulación de una donación
public class AuditoriaDonacion
{
    public const string Alta = "ALTA", Edicion = "EDICION", Anulacion = "ANULACION";

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DonacionId { get; set; }
    public long NumeroComprobante { get; set; }
    public string Accion { get; set; } = "";
    public Guid? UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = "";
    public string? Rol { get; set; }
    public string? Motivo { get; set; }
    public string? ValorAnterior { get; set; } // JSON
    public string ValorNuevo { get; set; } = "{}"; // JSON
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
