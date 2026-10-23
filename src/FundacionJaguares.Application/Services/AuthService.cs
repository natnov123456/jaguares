using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    public AuthService(AppDbContext db, string secret, string issuer, string audience)
    { _db = db; _jwtSecret = secret; _jwtIssuer = issuer; _jwtAudience = audience; }

    public async Task<TokenDto> RegistroAsync(RegistroDto dto)
    {
        if (await _db.Usuarios.AnyAsync(u => u.Email == dto.Email))
            throw new InvalidOperationException("Email ya registrado");

        var u = new User
        {
            NombreCompleto = dto.NombreCompleto,
            Email = dto.Email,
            Telefono = dto.Telefono,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Rol = Rol.PARTICIPANTE
        };
        _db.Usuarios.Add(u);
        await _db.SaveChangesAsync();
        return GenerarToken(u);
    }

    public async Task<TokenDto> LoginAsync(LoginDto dto)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Email == dto.Email)
                ?? throw new UnauthorizedAccessException("Credenciales inválidas");
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, u.PasswordHash))
            throw new UnauthorizedAccessException("Credenciales inválidas");
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