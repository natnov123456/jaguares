using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

// Términos y condiciones y Política de privacidad: cada formulario que pide datos personales tiene dos
// casillas separadas y obligatorias. Se guarda cuándo y cómo se aceptó cada documento (formulario,
// versión, IP y navegador) en la tabla de solo lectura "consentimientos".
public static class DocumentosLegales
{
    // Fecha de publicación de la versión vigente. Si los textos cambian, se actualiza aquí y en el sitio.
    public const string Version = "2026-11-20";
    public const string Terminos = "TERMINOS";
    public const string Privacidad = "PRIVACIDAD";
}

public static class Formularios
{
    public const string Registro = "REGISTRO", Voluntariado = "VOLUNTARIADO", Donacion = "DONACION", Contacto = "CONTACTO";
}

public record OrigenSolicitud(string? Ip, string? Navegador);

public interface IConsentimientoService
{
    Task RegistrarAsync(string formulario, Guid? usuarioId, string? email, OrigenSolicitud origen);
}

public class ConsentimientoService(AppDbContext db) : IConsentimientoService
{
    // Se valida antes de guardar nada: sin las dos aceptaciones no se crea la cuenta, la postulación, etc.
    public static void Exigir(bool? aceptaTerminos, bool? aceptaPrivacidad)
    {
        if (aceptaTerminos != true) throw new ArgumentException("Debes aceptar los Términos y condiciones");
        if (aceptaPrivacidad != true) throw new ArgumentException("Debes aceptar la Política de privacidad");
    }

    public async Task RegistrarAsync(string formulario, Guid? usuarioId, string? email, OrigenSolicitud origen)
    {
        var ahora = DateTime.UtcNow;
        var correo = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        // Si la persona tiene cuenta, el consentimiento queda también enlazado a ella
        usuarioId ??= correo is null ? null : await db.Usuarios.Where(u => u.Email.ToLower() == correo).Select(u => (Guid?)u.Id).FirstOrDefaultAsync();
        foreach (var documento in new[] { DocumentosLegales.Terminos, DocumentosLegales.Privacidad })
            db.Consentimientos.Add(new Consentimiento
            {
                UsuarioId = usuarioId, Email = Recortar(correo, 150), Formulario = formulario, Documento = documento,
                Version = DocumentosLegales.Version, AceptadoEn = ahora, Ip = Recortar(origen.Ip, 45), Navegador = Recortar(origen.Navegador, 300),
            });
        await db.SaveChangesAsync();
    }

    public static string? Recortar(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];
}
