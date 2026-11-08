using Microsoft.Extensions.Logging;

namespace FundacionJaguares.Infrastructure.Adapters;

public record AdjuntoCorreo(string Nombre, byte[] Contenido, string TipoContenido);

public interface IEmailAdapter
{
    // responderA: dirección a la que se responde (p. ej. el correo de quien escribió el formulario de contacto)
    Task EnviarAsync(string para, string asunto, string cuerpo, string? responderA = null, AdjuntoCorreo? adjunto = null);
}
public interface IWhatsAppAdapter { Task EnviarAsync(string telefono, string mensaje); }
public interface IPaymentAdapter  { Task<bool> ProcesarPagoAsync(decimal monto, string referencia); }

// Configuración "Smtp" de appsettings / user-secrets. Si Host está vacío no se envía nada:
// el correo solo queda en el log (modo desarrollo).
public class SmtpOpciones
{
    public string? Host { get; set; }
    public int Puerto { get; set; } = 587;
    public string? Usuario { get; set; }
    public string? Password { get; set; }
    public bool UsarSsl { get; set; } = true;
    public string Remitente { get; set; } = "no-responder@jaguares.org";
    public string NombreRemitente { get; set; } = "Fundación Unión de los Jaguares";
    public bool Configurado => !string.IsNullOrWhiteSpace(Host);
}

public class EmailAdapter : IEmailAdapter
{
    private readonly ILogger<EmailAdapter> _log;
    private readonly SmtpOpciones _smtp;

    public EmailAdapter(ILogger<EmailAdapter> log, SmtpOpciones smtp) { _log = log; _smtp = smtp; }

    public async Task EnviarAsync(string para, string asunto, string cuerpo, string? responderA = null, AdjuntoCorreo? adjunto = null)
    {
        if (!_smtp.Configurado)
        {
            _log.LogInformation("[EMAIL sin SMTP] Para: {p} | {a} | {c} | Adjunto: {n}", para, asunto, cuerpo, adjunto?.Nombre ?? "ninguno");
            return;
        }

        // Los saltos de línea en el asunto permitirían inyectar cabeceras
        var asuntoLimpio = asunto.Replace('\r', ' ').Replace('\n', ' ');
        using var mensaje = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress(_smtp.Remitente, _smtp.NombreRemitente),
            Subject = asuntoLimpio,
            Body = cuerpo,
            IsBodyHtml = false,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };
        mensaje.To.Add(para);
        if (!string.IsNullOrWhiteSpace(responderA)) mensaje.ReplyToList.Add(responderA);
        if (adjunto is not null)
            mensaje.Attachments.Add(new System.Net.Mail.Attachment(new MemoryStream(adjunto.Contenido), adjunto.Nombre, adjunto.TipoContenido));

        using var cliente = new System.Net.Mail.SmtpClient(_smtp.Host, _smtp.Puerto) { EnableSsl = _smtp.UsarSsl };
        if (!string.IsNullOrEmpty(_smtp.Usuario))
            cliente.Credentials = new System.Net.NetworkCredential(_smtp.Usuario, _smtp.Password);
        await cliente.SendMailAsync(mensaje);
        _log.LogInformation("[EMAIL] Enviado a {p} | {a}", para, asuntoLimpio);
    }
}

public class WhatsAppAdapter : IWhatsAppAdapter
{
    private readonly ILogger<WhatsAppAdapter> _log;
    public WhatsAppAdapter(ILogger<WhatsAppAdapter> log) => _log = log;
    public Task EnviarAsync(string telefono, string mensaje)
    {
        _log.LogInformation("[WHATSAPP] Para: {t} | {m}", telefono, mensaje);
        return Task.CompletedTask;
    }
}

public class PaymentAdapter : IPaymentAdapter
{
    private readonly ILogger<PaymentAdapter> _log;
    public PaymentAdapter(ILogger<PaymentAdapter> log) => _log = log;
    public Task<bool> ProcesarPagoAsync(decimal monto, string referencia)
    {
        _log.LogInformation("[PAYMENT] Procesando {monto} BOB ref={ref}", monto, referencia);
        return Task.FromResult(true);
    }
}