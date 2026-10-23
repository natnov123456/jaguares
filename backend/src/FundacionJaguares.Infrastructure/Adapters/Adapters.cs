using Microsoft.Extensions.Logging;

namespace FundacionJaguares.Infrastructure.Adapters;

public interface IEmailAdapter    { Task EnviarAsync(string para, string asunto, string cuerpo); }
public interface IWhatsAppAdapter { Task EnviarAsync(string telefono, string mensaje); }
public interface IPaymentAdapter  { Task<bool> ProcesarPagoAsync(decimal monto, string referencia); }

public class EmailAdapter : IEmailAdapter
{
    private readonly ILogger<EmailAdapter> _log;
    public EmailAdapter(ILogger<EmailAdapter> log) => _log = log;
    public Task EnviarAsync(string para, string asunto, string cuerpo)
    {
        _log.LogInformation("[EMAIL] Para: {p} | {a} | {c}", para, asunto, cuerpo);
        return Task.CompletedTask;
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