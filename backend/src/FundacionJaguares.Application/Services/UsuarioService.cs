using System.Security.Cryptography;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

// JAG-82 / FUJ-122: la Directora crea las cuentas del personal, les asigna un rol y puede desactivarlas.
// No se envían correos: la Directora define la contraseña inicial al crear la cuenta (o después, con
// "Asignar contraseña") y se la entrega a la persona en persona.
public interface IUsuarioService
{
    Task<List<UsuarioDto>> ListarAsync(string? texto, Rol? rol, bool? activo);
    Task<List<RolDto>> RolesAsync();
    Task<UsuarioDto> CrearAsync(CrearUsuarioDto dto, Guid directoraId);
    Task<UsuarioDto> CambiarRolAsync(Guid usuarioId, Rol rol, Guid directoraId);
    Task<UsuarioDto> CambiarActivoAsync(Guid usuarioId, bool activo, Guid directoraId);
    Task CambiarContrasenaAsync(Guid usuarioId, string? password);
}

public class UsuarioService : IUsuarioService
{
    public const int LargoMinimoContrasena = 8;

    // Roles que la Directora puede asignar. VISITANTE no es un rol de cuenta.
    public static readonly Rol[] RolesAsignables =
        [Rol.DIRECTORA, Rol.CONTADOR, Rol.ENCARGADO_EVENTOS, Rol.COORDINADOR_VOLUNTARIADO, Rol.DOCENTE, Rol.PARTICIPANTE, Rol.VOLUNTARIO];

    private readonly AppDbContext _db;

    public UsuarioService(AppDbContext db) => _db = db;

    // 256 bits aleatorios en base64url (contraseña que nadie conoce para cuentas aún sin contraseña)
    public static string NuevoToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static void ValidarContrasena(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < LargoMinimoContrasena)
            throw new ArgumentException($"La contraseña debe tener al menos {LargoMinimoContrasena} caracteres");
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new ArgumentException("La contraseña debe combinar letras y números");
    }

    public static void ValidarRolAsignable(Rol rol)
    {
        if (!RolesAsignables.Contains(rol)) throw new ArgumentException("Rol no válido");
    }

    private static UsuarioDto ADto(User u) =>
        new(u.Id, u.NombreCompleto, u.Email, u.Telefono, u.Rol.ToString(), u.Activo, u.CreadoEn);

    public async Task<List<UsuarioDto>> ListarAsync(string? texto, Rol? rol, bool? activo)
    {
        var q = _db.Usuarios.AsQueryable();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = $"%{texto.Trim()}%";
            q = q.Where(u => EF.Functions.ILike(u.NombreCompleto, t) || EF.Functions.ILike(u.Email, t));
        }
        if (rol is not null) q = q.Where(u => u.Rol == rol);
        if (activo is not null) q = q.Where(u => u.Activo == activo);
        return (await q.OrderBy(u => u.NombreCompleto).ToListAsync()).Select(ADto).ToList();
    }

    public Task<List<RolDto>> RolesAsync() =>
        _db.Roles.OrderByDescending(r => r.Interno).ThenBy(r => r.Nombre)
            .Select(r => new RolDto(r.Codigo, r.Nombre, r.Descripcion, r.Interno)).ToListAsync();

    public async Task<UsuarioDto> CrearAsync(CrearUsuarioDto dto, Guid directoraId)
    {
        var nombre = dto.NombreCompleto?.Trim() ?? "";
        var email = dto.Email?.Trim().ToLowerInvariant() ?? "";
        if (nombre.Length is < 3 or > 150) throw new ArgumentException("El nombre debe tener entre 3 y 150 caracteres");
        if (!ValidacionCorreo.EmailValido(email)) throw new ArgumentException("El correo no es válido");
        ValidarRolAsignable(dto.Rol);
        if (dto.Password is not null) ValidarContrasena(dto.Password);
        if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == email))
            throw new InvalidOperationException("Ya existe una cuenta con ese correo");

        var u = new User
        {
            NombreCompleto = nombre, Email = email, Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim(),
            Rol = dto.Rol,
            // Sin contraseña inicial (p. ej. importación) se guarda una aleatoria que nadie conoce:
            // la cuenta no puede iniciar sesión hasta que la Directora le asigne una
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password ?? NuevoToken())
        };
        _db.Usuarios.Add(u);
        await _db.SaveChangesAsync();
        return ADto(u);
    }

    public async Task<UsuarioDto> CambiarRolAsync(Guid usuarioId, Rol rol, Guid directoraId)
    {
        ValidarRolAsignable(rol);
        if (usuarioId == directoraId) throw new InvalidOperationException("No puedes cambiar tu propio rol");
        var u = await _db.Usuarios.FindAsync(usuarioId) ?? throw new KeyNotFoundException("Usuario no encontrado");
        if (u.Rol == Rol.DIRECTORA && rol != Rol.DIRECTORA) await ExigirOtraDirectoraAsync(u.Id);
        u.Rol = rol;
        u.ActualizadoEn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ADto(u);
    }

    public async Task<UsuarioDto> CambiarActivoAsync(Guid usuarioId, bool activo, Guid directoraId)
    {
        if (usuarioId == directoraId && !activo) throw new InvalidOperationException("No puedes desactivar tu propia cuenta");
        var u = await _db.Usuarios.FindAsync(usuarioId) ?? throw new KeyNotFoundException("Usuario no encontrado");
        if (!activo && u.Rol == Rol.DIRECTORA) await ExigirOtraDirectoraAsync(u.Id);
        u.Activo = activo;
        u.ActualizadoEn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ADto(u);
    }

    // Siempre debe quedar al menos una Directora activa que pueda administrar la plataforma
    private async Task ExigirOtraDirectoraAsync(Guid exceptoId)
    {
        if (!await _db.Usuarios.AnyAsync(x => x.Id != exceptoId && x.Rol == Rol.DIRECTORA && x.Activo))
            throw new InvalidOperationException("Debe quedar al menos una cuenta de Dirección activa");
    }

    public async Task CambiarContrasenaAsync(Guid usuarioId, string? password)
    {
        ValidarContrasena(password);
        var u = await _db.Usuarios.FindAsync(usuarioId) ?? throw new KeyNotFoundException("Usuario no encontrado");
        if (!u.Activo) throw new InvalidOperationException("La cuenta está desactivada: actívala antes de asignarle una contraseña");
        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        u.ActualizadoEn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
