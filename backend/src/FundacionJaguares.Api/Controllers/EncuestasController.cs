using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FundacionJaguares.Api.Controllers;

// FUJ-120: encuestas de satisfacción post-actividad
[ApiController]
[Route("api/[controller]")]
public class EncuestasController : ControllerBase
{
    private const string Gestion = "DIRECTORA,DOCENTE,ENCARGADO_EVENTOS";

    private readonly IEncuestaService _encuestas;
    public EncuestasController(IEncuestaService encuestas) => _encuestas = encuestas;

    private Guid UsuarioId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                                           ?? User.FindFirstValue("sub")!);
    private string Rol() => User.FindFirstValue(ClaimTypes.Role) ?? "";

    // Preguntas que se usan si no se personalizan al cerrar la actividad
    [HttpGet("preguntas-por-defecto")]
    [Authorize(Roles = Gestion)]
    public IActionResult PreguntasPorDefecto() => Ok(EncuestaService.PreguntasPorDefecto);

    // ---- Responder: público, con el token del enlace personal ----

    [HttpGet("responder/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting("encuesta-publica")]
    public async Task<IActionResult> Obtener(string token)
    {
        try { return Ok(await _encuestas.ObtenerPublicaAsync(token)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("responder/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting("encuesta-publica")]
    public async Task<IActionResult> Responder(string token, ResponderEncuestaDto dto)
    {
        try { await _encuestas.ResponderAsync(token, dto); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    // Encuestas que el participante todavía no respondió (se muestran en "Mi panel")
    [HttpGet("mis-pendientes")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> MisPendientes() => Ok(await _encuestas.PendientesAsync(UsuarioId()));

    // ---- Resultados: directora (todas), docente (sus cursos), encargado (eventos) ----

    [HttpGet]
    [Authorize(Roles = Gestion)]
    public async Task<IActionResult> Listar() => Ok(await _encuestas.ListarAsync(UsuarioId(), Rol()));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = Gestion)]
    public async Task<IActionResult> Resultados(Guid id)
    {
        try { return Ok(await _encuestas.ResultadosAsync(id, UsuarioId(), Rol())); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}
