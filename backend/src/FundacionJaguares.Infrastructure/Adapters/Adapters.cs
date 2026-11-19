using Microsoft.Extensions.Logging;

namespace FundacionJaguares.Infrastructure.Adapters;

// ContentId: si se indica y el correo tiene HTML, la imagen se incrusta (<img src="cid:...">) en lugar de adjuntarse
public record AdjuntoCorreo(string Nombre, byte[] Contenido, string TipoContenido, string? ContentId = null);

public interface IEmailAdapter
{
    // responderA: dirección a la que se respondería el aviso (opcional)
    // html: versión con formato (JAG-66); cuerpo queda como texto plano para clientes que no muestran HTML
    Task EnviarAsync(string para, string asunto, string cuerpo, string? responderA = null, AdjuntoCorreo? adjunto = null,
                     string? html = null);
}
public interface IWhatsAppAdapter { Task EnviarAsync(string telefono, string mensaje); }
public interface IPaymentAdapter  { Task<bool> ProcesarPagoAsync(decimal monto, string referencia); }

// La plataforma NO envía correos ni mensajes de WhatsApp: cada acción queda registrada y se confirma en pantalla
// ("Listo", "Registrado"). Los servicios siguen avisando a través de estas interfaces para que, si algún día se
// quisiera notificar, baste con cambiar el adaptador; hoy solo dejan una línea de depuración en el log.
public class EmailAdapter : IEmailAdapter
{
    private readonly ILogger<EmailAdapter> _log;
    public EmailAdapter(ILogger<EmailAdapter> log) => _log = log;

    public Task EnviarAsync(string para, string asunto, string cuerpo, string? responderA = null, AdjuntoCorreo? adjunto = null,
                            string? html = null)
    {
        _log.LogDebug("[EMAIL desactivado] {a}", asunto);
        return Task.CompletedTask;
    }
}

public class WhatsAppAdapter : IWhatsAppAdapter
{
    private readonly ILogger<WhatsAppAdapter> _log;
    public WhatsAppAdapter(ILogger<WhatsAppAdapter> log) => _log = log;
    public Task EnviarAsync(string telefono, string mensaje)
    {
        _log.LogDebug("[WHATSAPP desactivado]");
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