using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FundacionJaguares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IUsuarioService _usuarios;
    public AuthController(IAuthService auth, IUsuarioService usuarios) { _auth = auth; _usuarios = usuarios; }

    // FUJ-122: el personal define su contraseña con el enlace de un solo uso que recibió por correo
    [HttpPost("definir-contrasena")]
    [EnableRateLimiting("encuesta-publica")]
    public async Task<IActionResult> DefinirContrasena(DefinirContrasenaDto dto)
    {
        try { await _usuarios.DefinirContrasenaAsync(dto); return NoContent(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("registro")]
    public async Task<ActionResult<TokenDto>> Registro(RegistroDto dto)
    {
        try { return Ok(await _auth.RegistroAsync(dto)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenDto>> Login(LoginDto dto)
    {
        try { return Ok(await _auth.LoginAsync(dto)); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { error = ex.Message }); }
    }
}