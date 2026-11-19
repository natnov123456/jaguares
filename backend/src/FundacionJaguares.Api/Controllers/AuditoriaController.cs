using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

// JAG-88 / FUJ-125: consulta de la bitácora de auditoría de donaciones (Contador y Directora).
// Solo lectura: no hay endpoints para modificar ni borrar registros.
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "DIRECTORA,CONTADOR")]
public class AuditoriaController : ControllerBase
{
    private readonly IAuditoriaService _auditoria;
    public AuditoriaController(IAuditoriaService auditoria) => _auditoria = auditoria;

    [HttpGet("donaciones")]
    public async Task<IActionResult> Donaciones([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, [FromQuery] Guid? usuarioId,
        [FromQuery] string? accion, [FromQuery] string? q, [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 25)
    {
        try { return Ok(await _auditoria.ConsultarAsync(new FiltroAuditoria(desde, hasta, usuarioId, accion, q, pagina, tamanoPagina))); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("donaciones/usuarios")]
    public async Task<IActionResult> Usuarios() => Ok(await _auditoria.UsuariosAsync());
}
