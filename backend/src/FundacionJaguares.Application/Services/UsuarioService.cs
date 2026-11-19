using System.Security.Cryptography;
using System.Text;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

// JAG-82 / FUJ-122: la Directora crea las cuentas del personal, les asigna un rol y puede desactivarlas.
// La cuenta nueva no tiene contraseña utilizable: la persona la define con un enlace de un solo uso (24 h)
// que recibe por correo. Del token solo se guarda su hash SHA-256.
public interface IUsuarioService
{
    Task<List<UsuarioDto>> ListarAsync(string? texto, Rol? rol, bool? activo);
    Task<List<RolDto>> RolesAsync();
    Task<UsuarioDto> CrearAsync(CrearUsuarioDto dto, Guid directoraId);
    Task<UsuarioDto> CambiarRolAsync(Guid usuarioId, Rol rol, Guid directoraId);
    Task<UsuarioDto> CambiarActivoAsync(Guid usuarioId, bool activo, Guid directoraId);
    Task ReenviarEnlaceAsync(Guid usuarioId);
    Task DefinirContrasenaAsync(DefinirContrasenaDto dto);
}

public class UsuarioService : IUsuarioService
{
    public static readonly TimeSpan VigenciaEnlace = TimeSpan.FromHours(24);
    public const int LargoMinimoContrasena = 8;

    // Roles que la Directora puede asignar. VISITANTE no es un rol de cuenta.
    public static readonly Rol[] RolesAsignables =
        [Rol.DIRECTORA, Rol.CONTADOR, Rol.ENCARGADO_EVENTOS, Rol.COORDINADOR_VOLUNTARIADO, Rol.DOCENTE, Rol.PARTICIPANTE, Rol.VOLUNTARIO];

    private readonly AppDbContext _db;
    private readonly IEmailAdapter _email;
    private readonly AppOpciones _app;

    public UsuarioService(AppDbContext db, IEmailAdapter email, AppOpciones app) { _db = db; _email = email; _app = app; }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    // 256 bits aleatorios en base64url
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
        if (!ContactoService.EmailValido(email)) throw new ArgumentException("El correo no es válido");
        ValidarRolAsignable(dto.Rol);
        if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == email))
            throw new InvalidOperationException("Ya existe una cuenta con ese correo");

        var u = new User
        {
            NombreCompleto = nombre, Email = email, Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim(),
            Rol = dto.Rol,
            // Contraseña aleatoria que nadie conoce: hasta usar el enlace la cuenta no puede iniciar sesión
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(NuevoToken())
        };
        _db.Usuarios.Add(u);
        await _db.SaveChangesAsync();
        await EnviarEnlaceAsync(u, primeraVez: true);
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

    public async Task ReenviarEnlaceAsync(Guid usuarioId)
    {
        var u = await _db.Usuarios.FindAsync(usuarioId) ?? throw new KeyNotFoundException("Usuario no encontrado");
        if (!u.Activo) throw new InvalidOperationException("La cuenta está desactivada: actívala antes de reenviar el enlace");
        await EnviarEnlaceAsync(u, primeraVez: false);
    }

    private async Task EnviarEnlaceAsync(User u, bool primeraVez)
    {
        // Un enlace nuevo invalida los anteriores que aún no se usaron
        await _db.TokensContrasena.Where(t => t.UsuarioId == u.Id && t.UsadoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiraEn, DateTime.UtcNow));
        var token = NuevoToken();
        _db.TokensContrasena.Add(new TokenContrasena { UsuarioId = u.Id, TokenHash = Hash(token), ExpiraEn = DateTime.UtcNow + VigenciaEnlace });
        await _db.SaveChangesAsync();

        var url = $"{_app.FrontendUrl.TrimEnd('/')}/definir-contrasena?token={token}";
        var rol = await _db.Roles.Where(r => r.Codigo == u.Rol.ToString()).Select(r => r.Nombre).FirstOrDefaultAsync() ?? u.Rol.ToString();
        await _email.EnviarAsync(u.Email,
            primeraVez ? "Tu cuenta en la Fundación Unión de los Jaguares" : "Enlace para definir tu contraseña",
            $"Hola {u.NombreCompleto}, " +
            (primeraVez ? $"la Dirección creó tu cuenta con el rol {rol}. " : "") +
            $"Define tu contraseña desde este enlace (válido por 24 horas y de un solo uso): {url}");
    }

    public async Task DefinirContrasenaAsync(DefinirContrasenaDto dto)
    {
        ValidarContrasena(dto.Password);
        var hash = Hash(dto.Token?.Trim() ?? "");
        var ahora = DateTime.UtcNow;
        var t = await _db.TokensContrasena.FirstOrDefaultAsync(x => x.TokenHash == hash);
        if (t is null || t.UsadoEn is not null || t.ExpiraEn <= ahora)
            throw new InvalidOperationException("El enlace no es válido o ya venció. Pide a la Dirección que te envíe uno nuevo.");
        var u = await _db.Usuarios.FindAsync(t.UsuarioId) ?? throw new InvalidOperationException("El enlace no es válido");
        if (!u.Activo) throw new InvalidOperationException("Tu cuenta está desactivada. Contacta a la fundación.");

        // Condicional: si el enlace se usa dos veces a la vez, solo una gana
        var marcados = await _db.TokensContrasena.Where(x => x.Id == t.Id && x.UsadoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsadoEn, ahora));
        if (marcados == 0) throw new InvalidOperationException("El enlace ya fue usado");

        u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        u.ActualizadoEn = ahora;
        await _db.SaveChangesAsync();
    }
}
