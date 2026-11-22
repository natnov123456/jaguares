using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FundacionJaguares.Application.Services;

public interface IAuthService
{
    Task<TokenDto> RegistroAsync(RegistroDto dto);
    Task<TokenDto> LoginAsync(LoginDto dto);
}

public partial class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    public AuthService(AppDbContext db, string secret, string issuer, string audience)
    { _db = db; _jwtSecret = secret; _jwtIssuer = issuer; _jwtAudience = audience; }

    // Hash de una contraseña que no existe: si el correo no está registrado igual se verifica uno, así la respuesta
    // tarda lo mismo y no se puede averiguar qué correos tienen cuenta midiendo el tiempo.
    private static readonly Lazy<string> HashFalso = new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()));

    [GeneratedRegex(@"^[0-9+()\-\s]{6,20}$")]
    private static partial Regex TelefonoValido();

    // Validación en el servidor (no se confía en la del navegador)
    public static (string Nombre, string Email, string? Telefono) ValidarRegistro(RegistroDto dto)
    {
        var nombre = (dto.NombreCompleto ?? "").Trim();
        var email = (dto.Email ?? "").Trim();
        var telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();
        if (nombre.Length < 3 || nombre.Length > 150)
            throw new ArgumentException("El nombre debe tener entre 3 y 150 caracteres");
        if (nombre.Any(c => char.IsControl(c) || c is '<' or '>'))
            throw new ArgumentException("El nombre tiene caracteres no permitidos");
        if (!ContactoService.EmailValido(email))
            throw new ArgumentException("Correo inválido");
        UsuarioService.ValidarContrasena(dto.Password);
        if (dto.Password!.Length > 128)
            throw new ArgumentException("La contraseña no puede tener más de 128 caracteres");
        if (telefono is not null && !TelefonoValido().IsMatch(telefono))
            throw new ArgumentException("Teléfono inválido");
        return (nombre, email, telefono);
    }

    public async Task<TokenDto> RegistroAsync(RegistroDto dto)
    {
        var (nombre, email, telefono) = ValidarRegistro(dto);
        var emailMin = email.ToLower();
        if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == emailMin))
            throw new InvalidOperationException("Email ya registrado");

        var u = new User
        {
            NombreCompleto = nombre,
            Email = email,
            Telefono = telefono,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Rol = Rol.PARTICIPANTE
        };
        _db.Usuarios.Add(u);
        await _db.SaveChangesAsync();
        return GenerarToken(u);
    }

    public async Task<TokenDto> LoginAsync(LoginDto dto)
    {
        var email = (dto.Email ?? "").Trim();
        if (email.Length == 0 || email.Length > 150 || string.IsNullOrEmpty(dto.Password) || dto.Password.Length > 128)
            throw new UnauthorizedAccessException("Credenciales inválidas");
        var emailMin = email.ToLower();
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Email.ToLower() == emailMin);
        if (u is null)
        {
            BCrypt.Net.BCrypt.Verify(dto.Password, HashFalso.Value);
            throw new UnauthorizedAccessException("Credenciales inválidas");
        }
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, u.PasswordHash))
            throw new UnauthorizedAccessException("Credenciales inválidas");
        if (!u.Activo)
            throw new UnauthorizedAccessException("Tu cuenta está desactivada. Contacta a la fundación.");
        return GenerarToken(u);
    }

    private TokenDto GenerarToken(User u)
    {
        var expira = DateTime.UtcNow.AddMinutes(60);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, u.Id.ToString()),
            new Claim(ClaimTypes.Name, u.NombreCompleto),
            new Claim(ClaimTypes.Email, u.Email),
            new Claim(ClaimTypes.Role, u.Rol.ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_jwtIssuer, _jwtAudience, claims,
            expires: expira, signingCredentials: creds);
        return new TokenDto(new JwtSecurityTokenHandler().WriteToken(jwt),
                            u.NombreCompleto, u.Rol.ToString(), expira);
    }
}