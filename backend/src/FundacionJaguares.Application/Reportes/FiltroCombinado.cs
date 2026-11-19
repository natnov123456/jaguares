using System.Globalization;
using System.Text;

namespace FundacionJaguares.Application.Reportes;

// Corrección de la evaluación (búsquedas filtradas): un solo módulo para los filtros combinados.
// Las mismas reglas las aplica el frontend (src/utils/filtros.js), así lo que se ve en pantalla
// es exactamente lo que se exporta:
//   - Todos los filtros se combinan con Y (AND); un filtro vacío no restringe.
//   - Texto: sin distinguir mayúsculas ni tildes, busca en cualquiera de los campos indicados.
//   - Estado: igualdad exacta (sin distinguir mayúsculas).
//   - Rango de fechas: inclusivo en ambos extremos, con la fecha en hora de Bolivia.
//     Si hay rango y el elemento no tiene fecha, queda fuera.
public static class FiltroCombinado
{
    public const int LargoMaximoTexto = 100;
    private static readonly TimeSpan HusoBolivia = TimeSpan.FromHours(-4);

    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        var sinTildes = new StringBuilder();
        foreach (var c in texto.Trim().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sinTildes.Append(c);
        return sinTildes.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    public static bool CoincideTexto(string? q, params string?[] campos)
    {
        var buscado = Normalizar(q);
        return buscado.Length == 0 || campos.Any(c => Normalizar(c).Contains(buscado));
    }

    public static bool CoincideEstado(string? estado, string? valor) =>
        string.IsNullOrWhiteSpace(estado) || string.Equals(estado.Trim(), valor?.Trim(), StringComparison.OrdinalIgnoreCase);

    public static DateOnly FechaBolivia(DateTime utc) =>
        DateOnly.FromDateTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc) + HusoBolivia);

    public static bool EnRango(DateTime? utc, DateOnly? desde, DateOnly? hasta)
    {
        if (desde is null && hasta is null) return true;
        if (utc is null) return false;
        return EnRango(FechaBolivia(utc.Value), desde, hasta);
    }

    public static bool EnRango(DateOnly? fecha, DateOnly? desde, DateOnly? hasta)
    {
        if (desde is null && hasta is null) return true;
        if (fecha is null) return false;
        return (desde is null || fecha >= desde) && (hasta is null || fecha <= hasta);
    }

    public static IEnumerable<T> Aplicar<T>(IEnumerable<T> items, FiltrosReporte f,
        Func<T, string?[]>? texto = null, Func<T, string?>? estado = null, Func<T, DateTime?>? fecha = null) =>
        items.Where(x =>
            (texto is null || CoincideTexto(f.Q, texto(x))) &&
            (estado is null || CoincideEstado(f.Estado, estado(x))) &&
            (fecha is null || EnRango(fecha(x), f.Desde, f.Hasta)));

    // Rechaza combinaciones imposibles antes de consultar
    public static void Validar(FiltrosReporte f)
    {
        if (f.Q?.Length > LargoMaximoTexto) throw new ArgumentException($"La búsqueda no puede superar {LargoMaximoTexto} caracteres");
        if (f.Desde is not null && f.Hasta is not null && f.Desde > f.Hasta)
            throw new ArgumentException("La fecha 'desde' no puede ser posterior a 'hasta'");
    }

    // Texto de los filtros para el encabezado del reporte (solo los que se usaron)
    public static List<ParReporte> Describir(FiltrosReporte f, string? actividad = null, Func<string, string>? etiquetaEstado = null)
    {
        var lista = new List<ParReporte>();
        if (actividad is not null) lista.Add(new("Actividad", actividad));
        if (!string.IsNullOrWhiteSpace(f.Q)) lista.Add(new("Búsqueda", $"\"{f.Q.Trim()}\""));
        if (!string.IsNullOrWhiteSpace(f.Estado)) lista.Add(new("Estado", etiquetaEstado?.Invoke(f.Estado) ?? f.Estado));
        if (!string.IsNullOrWhiteSpace(f.Rol)) lista.Add(new("Rol", f.Rol));
        if (!string.IsNullOrWhiteSpace(f.Accion)) lista.Add(new("Acción", f.Accion));
        if (f.Desde is not null || f.Hasta is not null)
            lista.Add(new("Período", $"{f.Desde?.ToString("dd/MM/yyyy") ?? "inicio"} al {f.Hasta?.ToString("dd/MM/yyyy") ?? "hoy"}"));
        return lista;
    }
}
