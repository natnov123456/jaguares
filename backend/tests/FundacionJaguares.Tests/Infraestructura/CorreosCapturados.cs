using System.Collections.Concurrent;
using FundacionJaguares.Infrastructure.Adapters;

namespace FundacionJaguares.Tests.Infraestructura;

public record CorreoEnviado(string Para, string Asunto, string Texto, string? Html, AdjuntoCorreo? Adjunto, string? ResponderA);

// Reemplaza al EmailAdapter en las pruebas: guarda cada correo en memoria para poder verificarlo
public class CorreosCapturados : IEmailAdapter
{
    private readonly ConcurrentQueue<CorreoEnviado> _enviados = new();

    public Task EnviarAsync(string para, string asunto, string cuerpo, string? responderA = null, AdjuntoCorreo? adjunto = null,
                            string? html = null)
    {
        _enviados.Enqueue(new CorreoEnviado(para, asunto, cuerpo, html, adjunto, responderA));
        return Task.CompletedTask;
    }

    public List<CorreoEnviado> Para(string email) =>
        _enviados.Where(c => string.Equals(c.Para, email, StringComparison.OrdinalIgnoreCase)).ToList();
}
