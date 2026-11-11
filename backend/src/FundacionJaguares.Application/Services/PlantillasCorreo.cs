using System.Globalization;
using System.Net;
using System.Text;

namespace FundacionJaguares.Application.Services;

// Correo listo para enviar: Texto es la versión plana (clientes sin HTML), Html la versión con formato
public record CorreoPlantilla(string Asunto, string Texto, string Html);

// Datos de la actividad que se muestran en los correos (Cuando ya viene formateado en hora de Bolivia)
public record ActividadCorreo(string Tipo, string Titulo, string Cuando, string? Lugar);

// JAG-66 / FUJ-119: plantillas de recordatorio (24 h antes), actividad reprogramada y actividad cancelada.
// HTML hecho con tablas y estilos en línea: es lo que Gmail y Outlook (escritorio y celular) muestran bien.
// Todo valor que viene de la base de datos se escapa para que no se pueda inyectar HTML en el correo.
public static class PlantillasCorreo
{
    public const string ContentIdQr = "qr-ingreso";

    private const string Naranja = "#f59e0b", Oscuro = "#111827", Gris = "#4b5563", Fondo = "#f5f5f5";
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-BO");

    // ---------- Formato de fechas (hora de Bolivia, UTC-4 sin horario de verano) ----------

    public static DateTime ABolivia(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(-4);

    // "martes 12 de noviembre de 2026, 15:30"
    public static string FechaHora(DateTime utc) =>
        ABolivia(utc).ToString("dddd d 'de' MMMM 'de' yyyy, HH:mm", Es);

    public static string Fecha(DateOnly fecha) => fecha.ToString("dddd d 'de' MMMM 'de' yyyy", Es);

    public static string CuandoEvento(DateTime inicioUtc, DateTime? finUtc) =>
        finUtc is { } fin && ABolivia(fin).Date == ABolivia(inicioUtc).Date
            ? $"{FechaHora(inicioUtc)} a {ABolivia(fin):HH:mm}"
            : finUtc is { } f2 ? $"{FechaHora(inicioUtc)} al {FechaHora(f2)}" : FechaHora(inicioUtc);

    public static string CuandoCurso(DateOnly inicio, DateOnly fin) =>
        inicio == fin ? Fecha(inicio) : $"del {Fecha(inicio)} al {Fecha(fin)}";

    // ---------- Plantillas ----------

    public static CorreoPlantilla Recordatorio(string nombre, ActividadCorreo a, string? codigoQr, string? urlPanel)
    {
        var asunto = $"Recordatorio: {a.Titulo} es mañana";
        var texto = new StringBuilder()
            .AppendLine($"Hola {nombre},")
            .AppendLine()
            .AppendLine($"Te recordamos que mañana tienes {Articulo(a.Tipo)} \"{a.Titulo}\".")
            .AppendLine($"Cuándo: {a.Cuando}")
            .AppendLine(a.Lugar is null ? "" : $"Dónde: {a.Lugar}")
            .AppendLine(codigoQr is null ? "" : $"Presenta tu código QR al ingresar (va adjunto). Código: {codigoQr}")
            .AppendLine(urlPanel is null ? "" : $"Detalle en Mi panel: {urlPanel}")
            .AppendLine()
            .AppendLine("¡Te esperamos!")
            .ToString();

        var cuerpo = new StringBuilder()
            .Append(Parrafo($"Hola <strong>{E(nombre)}</strong>,"))
            .Append(Parrafo($"Te recordamos que <strong>mañana</strong> tienes {Articulo(a.Tipo)}:"))
            .Append(Tarjeta(a))
            .Append(codigoQr is null ? "" :
                $"""
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:8px 0 16px">
                  <tr><td align="center" style="padding:16px;border:1px dashed {Naranja};border-radius:8px">
                    <p style="margin:0 0 8px;font-size:14px;color:{Gris}">Presenta este código QR al ingresar</p>
                    <img src="cid:{ContentIdQr}" width="200" height="200" alt="Código QR de ingreso" style="display:block;border:0">
                    <p style="margin:8px 0 0;font-family:monospace;font-size:13px;color:{Gris}">{E(codigoQr)}</p>
                  </td></tr>
                </table>
                """)
            .Append(urlPanel is null ? "" : Boton("Ver en Mi panel", urlPanel))
            .Append(Parrafo("¡Te esperamos!"))
            .ToString();

        return new CorreoPlantilla(asunto, Limpiar(texto), Layout(asunto, "Recordatorio de actividad", cuerpo));
    }

    public static CorreoPlantilla Reprogramada(string nombre, ActividadCorreo anterior, ActividadCorreo nueva, string? motivo, string? urlPanel)
    {
        var asunto = $"Cambio de fecha: {nueva.Titulo}";
        var texto = new StringBuilder()
            .AppendLine($"Hola {nombre},")
            .AppendLine()
            .AppendLine($"{Mayuscula(Articulo(nueva.Tipo))} \"{nueva.Titulo}\" fue reprogramado.")
            .AppendLine($"Antes: {anterior.Cuando}{(anterior.Lugar is null ? "" : $" — {anterior.Lugar}")}")
            .AppendLine($"Ahora: {nueva.Cuando}{(nueva.Lugar is null ? "" : $" — {nueva.Lugar}")}")
            .AppendLine(motivo is null ? "" : $"Motivo: {motivo}")
            .AppendLine()
            .AppendLine("Tu inscripción sigue vigente: no necesitas hacer nada. Te enviaremos un recordatorio un día antes.")
            .ToString();

        var cuerpo = new StringBuilder()
            .Append(Parrafo($"Hola <strong>{E(nombre)}</strong>,"))
            .Append(Parrafo($"{Mayuscula(Articulo(nueva.Tipo))} en el que estás inscrito/a <strong>cambió de fecha</strong>:"))
            .Append(
                $"""
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 16px;font-size:15px">
                  <tr><td style="padding:12px 16px;background:#fee2e2;border-radius:8px 8px 0 0;color:#991b1b">
                    <strong>Antes:</strong> <span style="text-decoration:line-through">{E(anterior.Cuando)}{(anterior.Lugar is null ? "" : " — " + E(anterior.Lugar))}</span>
                  </td></tr>
                  <tr><td style="padding:12px 16px;background:#dcfce7;border-radius:0 0 8px 8px;color:#166534">
                    <strong>Ahora:</strong> {E(nueva.Cuando)}{(nueva.Lugar is null ? "" : " — " + E(nueva.Lugar))}
                  </td></tr>
                </table>
                """)
            .Append(Tarjeta(nueva))
            .Append(motivo is null ? "" : Parrafo($"<strong>Motivo:</strong> {E(motivo)}"))
            .Append(Parrafo("Tu inscripción sigue vigente: no necesitas hacer nada. Te enviaremos un recordatorio un día antes."))
            .Append(urlPanel is null ? "" : Boton("Ver en Mi panel", urlPanel))
            .ToString();

        return new CorreoPlantilla(asunto, Limpiar(texto), Layout(asunto, "Actividad reprogramada", cuerpo));
    }

    public static CorreoPlantilla Cancelada(string nombre, ActividadCorreo a, string? motivo, string? urlCalendario)
    {
        var asunto = $"Actividad cancelada: {a.Titulo}";
        var texto = new StringBuilder()
            .AppendLine($"Hola {nombre},")
            .AppendLine()
            .AppendLine($"Lamentamos informarte que {Articulo(a.Tipo)} \"{a.Titulo}\" ({a.Cuando}) fue cancelado.")
            .AppendLine(motivo is null ? "" : $"Motivo: {motivo}")
            .AppendLine()
            .AppendLine(urlCalendario is null ? "" : $"Mira otras actividades en el calendario: {urlCalendario}")
            .AppendLine("Disculpa las molestias.")
            .ToString();

        var cuerpo = new StringBuilder()
            .Append(Parrafo($"Hola <strong>{E(nombre)}</strong>,"))
            .Append(Parrafo($"Lamentamos informarte que {Articulo(a.Tipo)} en el que estabas inscrito/a <strong>fue cancelado</strong>:"))
            .Append(Tarjeta(a, tachado: true))
            .Append(motivo is null ? "" : Parrafo($"<strong>Motivo:</strong> {E(motivo)}"))
            .Append(Parrafo("Disculpa las molestias. Te invitamos a conocer nuestras otras actividades."))
            .Append(urlCalendario is null ? "" : Boton("Ver calendario de actividades", urlCalendario))
            .ToString();

        return new CorreoPlantilla(asunto, Limpiar(texto), Layout(asunto, "Actividad cancelada", cuerpo));
    }

    // ---------- Piezas comunes ----------

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string Articulo(string tipo) => tipo == "CURSO" ? "el curso" : "el evento";
    private static string Mayuscula(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    // Quita las líneas vacías que dejan los campos opcionales (máximo una línea en blanco seguida)
    private static string Limpiar(string texto)
    {
        var lineas = texto.Replace("\r", "").Split('\n');
        var sb = new StringBuilder();
        var vacia = false;
        foreach (var l in lineas)
        {
            if (l.Length == 0) { if (!vacia) sb.Append('\n'); vacia = true; }
            else { sb.Append(l).Append('\n'); vacia = false; }
        }
        return sb.ToString().Trim() + "\n";
    }

    private static string Parrafo(string html) =>
        $"""<p style="margin:0 0 16px;font-size:15px;line-height:1.5;color:{Oscuro}">{html}</p>""";

    private static string Tarjeta(ActividadCorreo a, bool tachado = false) =>
        $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 16px;background:#fffbeb;border-left:4px solid {Naranja};border-radius:4px">
          <tr><td style="padding:14px 16px;font-size:15px;line-height:1.6;color:{Oscuro}">
            <div style="font-size:17px;font-weight:bold;{(tachado ? "text-decoration:line-through;" : "")}">{E(a.Titulo)}</div>
            <div>&#128197; {E(a.Cuando)}</div>
            {(a.Lugar is null ? "" : $"<div>&#128205; {E(a.Lugar)}</div>")}
          </td></tr>
        </table>
        """;

    private static string Boton(string texto, string url) =>
        $"""
        <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 16px">
          <tr><td style="background:{Naranja};border-radius:6px">
            <a href="{E(url)}" style="display:inline-block;padding:12px 22px;font-size:15px;font-weight:bold;color:{Oscuro};text-decoration:none">{E(texto)}</a>
          </td></tr>
        </table>
        """;

    private static string Layout(string asunto, string encabezado, string cuerpo) =>
        $"""
        <!DOCTYPE html>
        <html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>{E(asunto)}</title></head>
        <body style="margin:0;padding:0;background:{Fondo};font-family:Arial,Helvetica,sans-serif">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{Fondo}">
            <tr><td align="center" style="padding:24px 12px">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;background:#ffffff;border-radius:8px">
                <tr><td style="background:{Oscuro};border-bottom:4px solid {Naranja};border-radius:8px 8px 0 0;padding:18px 24px">
                  <div style="color:{Naranja};font-size:18px;font-weight:bold;letter-spacing:1px">FUNDACIÓN UNIÓN DE LOS JAGUARES</div>
                  <div style="color:#e5e7eb;font-size:14px">{E(encabezado)}</div>
                </td></tr>
                <tr><td style="padding:24px">{cuerpo}</td></tr>
                <tr><td style="padding:16px 24px;border-top:1px solid #e5e7eb;font-size:12px;color:{Gris}">
                  Recibes este correo porque te inscribiste en una actividad de la Fundación Unión de los Jaguares, La Paz, Bolivia.
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body></html>
        """;
}
