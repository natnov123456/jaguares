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

    [HttpPost]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> Crear(CrearCursoDto dto)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("sub")!);
        return Ok(await _cursos.CrearAsync(dto, id));
    }

    [HttpPost("{id:guid}/inscribir")]
    [Authorize]
    public async Task<IActionResult> Inscribir(Guid id)
    {
        var participanteId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? User.FindFirstValue("sub")!);
        try { return Ok(await _cursos.InscribirAsync(id, participanteId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}/inscripcion")]
    [Authorize]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        var participanteId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? User.FindFirstValue("sub")!);
        try { await _cursos.CancelarInscripcionAsync(id, participanteId); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/finalizar")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> Finalizar(Guid id)
    {
        var n = await _cursos.FinalizarYGenerarCertificadosAsync(id);
        return Ok(new { certificados_generados = n });
    }
}