using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FundacionJaguares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IUsuarioService _usuarios;
    private readonly IConsentimientoService _consentimientos;
    public AuthController(IAuthService auth, IUsuarioService usuarios, IConsentimientoService consentimientos)
    { _auth = auth; _usuarios = usuarios; _consentimientos = consentimientos; }

    // FUJ-122: el personal define su contraseña con el enlace de un solo uso que recibió por correo
    [HttpPost("definir-contrasena")]
    [EnableRateLimiting("encuesta-publica")]
    public async Task<IActionResult> DefinirContrasena(DefinirContrasenaDto dto)
    {
        try { await _usuarios.DefinirContrasenaAsync(dto); return NoContent(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // "¿Olvidaste tu contraseña?": siempre responde lo mismo, exista o no la cuenta
    [HttpPost("olvide-contrasena")]
    [EnableRateLimiting("recuperar-contrasena")]
    public async Task<IActionResult> OlvideContrasena(OlvideContrasenaDto dto)
    {
        HttpContext.Items[RegistroActividadMiddleware.ClaveEmail] = dto.Email?.Trim();
        try { await _usuarios.SolicitarRecuperacionAsync(dto.Email); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        return Ok(new { mensaje = "Si el correo está registrado, te enviamos un enlace para definir una contraseña nueva. Revisa tu bandeja de entrada y el correo no deseado." });
    }

    // Cada persona cambia su propia contraseña (nadie más puede hacerlo)
    [Authorize]
    [HttpPut("mi-contrasena")]
    public async Task<IActionResult> CambiarMiContrasena(CambiarMiContrasenaDto dto)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        try { await _usuarios.CambiarMiContrasenaAsync(id, dto.Actual, dto.Nueva); return NoContent(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // Registro de participante: exige aceptar los Términos y la Política de privacidad (dos casillas)
    [HttpPost("registro")]
    public async Task<ActionResult<TokenDto>> Registro(RegistroDto dto)
    {
        HttpContext.Items[RegistroActividadMiddleware.ClaveEmail] = dto.Email?.Trim();
        try
        {
            ConsentimientoService.Exigir(dto.AceptaTerminos, dto.AceptaPrivacidad);
            var token = await _auth.RegistroAsync(dto);
            await _consentimientos.RegistrarAsync(Formularios.Registro, null, dto.Email, HttpContext.Origen());
            return Ok(token);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // El correo queda en el registro de actividad también cuando el intento falla
    [HttpPost("login")]
    public async Task<ActionResult<TokenDto>> Login(LoginDto dto)
    {
        HttpContext.Items[RegistroActividadMiddleware.ClaveEmail] = dto.Email?.Trim();
        try
        {
            var token = await _auth.LoginAsync(dto);
            HttpContext.Items[RegistroActividadMiddleware.ClaveRol] = token.Rol;
            return Ok(token);
        }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { error = ex.Message }); }
    }

    // Cierre de sesión: el token vive en el navegador; esta llamada deja constancia en el registro de actividad
    [Authorize]
    [HttpPost("salir")]
    public IActionResult Salir() => NoContent();
}