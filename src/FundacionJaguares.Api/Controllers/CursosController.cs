using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CursosController : ControllerBase
{
    private readonly ICursoService _cursos;
    public CursosController(ICursoService cursos) => _cursos = cursos;

    [HttpGet]
    public async Task<IActionResult> Listar() => Ok(await _cursos.ListarPublicadosAsync());

    private Guid UsuarioId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                                           ?? User.FindFirstValue("sub")!);

    [HttpPost]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> Crear(CrearCursoDto dto)
    {
        try { return Ok(await _cursos.CrearAsync(dto, UsuarioId())); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Panel de gestión: el docente ve solo sus cursos, la directora todos
    [HttpGet("gestion")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> Gestion() =>
        Ok(await _cursos.ListarGestionAsync(User.IsInRole("DIRECTORA") ? null : UsuarioId()));

    [HttpGet("mis-inscripciones")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> MisInscripciones() =>
        Ok(await _cursos.ListarInscripcionesAsync(UsuarioId()));

    [HttpPost("{id:guid}/inscribir")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> Inscribir(Guid id)
    {
        var participanteId = UsuarioId();
        try { return Ok(await _cursos.InscribirAsync(id, participanteId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}/inscripcion")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        var participanteId = UsuarioId();
        try { await _cursos.CancelarInscripcionAsync(id, participanteId); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/finalizar")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> Finalizar(Guid id)
    {
        var docenteId = User.IsInRole("DIRECTORA") ? (Guid?)null : UsuarioId();
        try
        {
            var n = await _cursos.FinalizarYGenerarCertificadosAsync(id, docenteId);
            return Ok(new { certificados_generados = n });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}