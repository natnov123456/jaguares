using System.Net.Mail;

namespace FundacionJaguares.Application.Services;

// Validación del formato de una dirección de correo (cuentas, donantes, datos institucionales)
public static class ValidacionCorreo
{
    public static bool EmailValido(string email)
    {
        if (email.Length > 150 || !MailAddress.TryCreate(email, out var dir) || dir.Address != email) return false;
        var dominio = email[(email.IndexOf('@') + 1)..];
        return dominio.Contains('.') && !dominio.StartsWith('.') && !dominio.EndsWith('.');
    }
}
