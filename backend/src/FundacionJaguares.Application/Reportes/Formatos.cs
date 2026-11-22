using System.Globalization;

namespace FundacionJaguares.Application.Reportes;

// Formatos de celdas en español de Bolivia, comunes a todos los reportes
public static class Fmt
{
    private static readonly NumberFormatInfo Numero = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    public static string Fecha(DateTime utc) => PlantillaFecha(utc).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public static string Fecha(DateOnly f) => f.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public static string FechaHora(DateTime utc) => PlantillaFecha(utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    public static string FechaHora(DateTime? utc) => utc is null ? "—" : FechaHora(utc.Value);
    public static string Pct(double? v) => v is null ? "—" : v.Value.ToString("0.#", Numero) + " %";
    public static string Num(decimal v) => v.ToString("#,0.##", Numero);
    // Horas de voluntariado: 2,75 -> "2 h 45 min"; 3 -> "3 h"; 0,5 -> "30 min"
    public static string Horas(decimal v)
    {
        var minutos = (int)decimal.Round(v * 60m);
        var (h, m) = (minutos / 60, minutos % 60);
        return h == 0 && m == 0 ? "0 h" : m == 0 ? $"{h} h" : h == 0 ? $"{m} min" : $"{h} h {m} min";
    }
    // Horario de una colaboración: "15:30 a 18:15" (y "x 4 días" si se repitió)
    public static string Horario(string? inicio, string? fin, int? dias) =>
        inicio is null || fin is null ? "—" : $"{inicio} a {fin}" + (dias > 1 ? $" × {dias} días" : "");
    public static string Bs(decimal v) => "Bs. " + v.ToString("#,0.00", Numero);
    public static string Entero(int v) => v.ToString(CultureInfo.InvariantCulture);

    private static DateTime PlantillaFecha(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(-4);

    // Etiquetas legibles de los estados que guardamos en mayúsculas
    public static string Estado(string? estado) => estado switch
    {
        "CONFIRMADA" => "Confirmada",
        "CANCELADA" => "Cancelada",
        "ASISTIO" => "Asistió",
        "EN_ESPERA" => "En espera",
        "PUBLICADO" => "Publicado",
        "EN_CURSO" => "En curso",
        "FINALIZADO" => "Finalizado",
        "CANCELADO" => "Cancelado",
        "BORRADOR" => "Borrador",
        "ACTIVO" => "Activo",
        "INACTIVO" => "Inactivo",
        "ASIGNADA" => "Asignada",
        "COMPLETADA" => "Completada",
        "ANULADA" => "Anulada",
        "PENDIENTE" => "En verificación",
        "RECHAZADA" => "Rechazada",
        null or "" => "—",
        _ => estado
    };
}
