using System.Net.Mail;
using System.Text.RegularExpressions;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FundacionJaguares.Application.Services;

// Correo que recibe las consultas si la Directora no cargó uno en "Quiénes somos" (config "Contacto:EmailDestino")
public class ContactoOpciones
{
    public string? EmailDestino { get; set; }
}

public class LimiteExcedidoException : Exception
{
    public LimiteExcedidoException(string mensaje) : base(mensaje) { }
}

// FUJ-127: formulario de contacto. Protección anti-spam (RNF-02):
//  1) campo trampa (honeypot) que solo llenan los bots,
//  2) límite de envíos por IP (3 cada 10 min y 10 por día) y un tope global por hora,
//  3) validación estricta y rechazo de mensajes con muchos enlaces.
public interface IContactoService
{
    Task EnviarAsync(ConsultaContactoDto dto, string? ip);
}

public partial class ContactoService : IContactoService
{
    public const int MaxPorIp10Min = 3;
    public const int MaxPorIpDia = 10;
    public const int MaxGlobalHora = 100;
    public const int MaxEnlaces = 3;

    private readonly AppDbContext _db;
    private readonly IEmailAdapter _email;
    private readonly ContactoOpciones _opciones;
    private readonly ILogger<ContactoService> _log;

    public ContactoService(AppDbContext db, IEmailAdapter email, ContactoOpciones opciones, ILogger<ContactoService> log)
    { _db = db; _email = email; _opciones = opciones; _log = log; }

    [GeneratedRegex(@"https?://|www\.", RegexOptions.IgnoreCase)]
    private static partial Regex Enlace();

    public static bool EmailValido(string email)
    {
        if (email.Length > 150 || !MailAddress.TryCreate(email, out var dir) || dir.Address != email) return false;
        var dominio = email[(email.IndexOf('@') + 1)..];
        return dominio.Contains('.') && !dominio.StartsWith('.') && !dominio.EndsWith('.');
    }

    // Quita saltos de línea de campos que van a una sola línea (asunto del correo)
    private static string UnaLinea(string s) => Regex.Replace(s, @"[\r\n\t]+", " ").Trim();

    public async Task EnviarAsync(ConsultaContactoDto dto, string? ip)
    {
        // Bot detectado: se responde como si todo saliera bien para no darle pistas, pero no se envía nada
        if (!string.IsNullOrWhiteSpace(dto.SitioWeb))
        {
            _log.LogWarning("[CONTACTO] Envío descartado por campo trampa desde {ip}", ip);
            return;
        }

        var nombre = UnaLinea(dto.Nombre ?? "");
        var email = (dto.Email ?? "").Trim();
        var asunto = UnaLinea(dto.Asunto ?? "");
        var mensaje = (dto.Mensaje ?? "").Trim();

        var errores = new Dictionary<string, string>();
        if (nombre.Length < 2) errores["nombre"] = "Ingresa tu nombre";
        else if (nombre.Length > 150) errores["nombre"] = "Máximo 150 caracteres";
        if (email.Length == 0) errores["email"] = "Ingresa tu correo";
        else if (!EmailValido(email)) errores["email"] = "Correo inválido";
        if (asunto.Length > 150) errores["asunto"] = "Máximo 150 caracteres";
        if (mensaje.Length < 10) errores["mensaje"] = "El mensaje debe tener al menos 10 caracteres";
        else if (mensaje.Length > 3000) errores["mensaje"] = "Máximo 3000 caracteres";
        else if (Enlace().Matches(mensaje).Count > MaxEnlaces) errores["mensaje"] = "El mensaje tiene demasiados enlaces";
        if (errores.Count > 0) throw new ValidacionException(errores);

        var ahora = DateTime.UtcNow;
        if (ip is not null)
        {
            var hace10 = ahora.AddMinutes(-10);
            var haceDia = ahora.AddDays(-1);
            var recientes = await _db.ConsultasContacto
                .Where(c => c.Ip == ip && c.CreadoEn > haceDia)
                .Select(c => c.CreadoEn).ToListAsync();
            if (recientes.Count(f => f > hace10) >= MaxPorIp10Min || recientes.Count >= MaxPorIpDia)
                throw new LimiteExcedidoException("Enviaste varias consultas seguidas. Intenta de nuevo más tarde.");
        }
        if (await _db.ConsultasContacto.CountAsync(c => c.CreadoEn > ahora.AddHours(-1)) >= MaxGlobalHora)
            throw new LimiteExcedidoException("Estamos recibiendo muchas consultas. Intenta de nuevo en unos minutos.");

        var consulta = new ConsultaContacto
        {
            Nombre = nombre, Email = email, Asunto = asunto.Length == 0 ? null : asunto,
            Mensaje = mensaje, Ip = ip?.Length > 64 ? ip[..64] : ip, CreadoEn = ahora
        };
        _db.ConsultasContacto.Add(consulta);
        await _db.SaveChangesAsync();

        // Destino: correo institucional cargado por la Directora; si no hay, el de configuración
        var destino = (await _db.PaginaInstitucional.Where(p => p.Id == 1).Select(p => p.EmailContacto).FirstOrDefaultAsync())
                      ?? _opciones.EmailDestino;
        if (string.IsNullOrWhiteSpace(destino))
        {
            _log.LogWarning("[CONTACTO] Consulta {id} guardada sin enviar: no hay correo institucional configurado", consulta.Id);
            return;
        }

        try
        {
            await _email.EnviarAsync(destino,
                $"Consulta web: {(consulta.Asunto ?? "Sin asunto")} — {nombre}",
                $"Nombre: {nombre}\nCorreo: {email}\nFecha: {ahora:yyyy-MM-dd HH:mm} UTC\n\n{mensaje}\n\n" +
                "— Enviado desde el formulario de contacto del sitio. Responde a este correo para contestar a la persona.",
                responderA: email);
            consulta.Enviada = true;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            // La consulta queda guardada (enviada = false) para no perderla aunque falle el correo
            _log.LogError(ex, "[CONTACTO] No se pudo enviar la consulta {id} por correo", consulta.Id);
        }
    }
}
