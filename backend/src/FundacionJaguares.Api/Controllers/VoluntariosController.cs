using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FundacionJaguares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VoluntariosController : ControllerBase
{
    // Coordinación de voluntariado: mismos roles que la sección "Voluntarios" del panel
    private const string Coordinacion = "DIRECTORA,ENCARGADO_EVENTOS";

    private readonly IVoluntarioService _voluntarios;
    private readonly IVoluntariadoService _voluntariado;

    public VoluntariosController(IVoluntarioService voluntarios, IVoluntariadoService voluntariado)
    { _voluntarios = voluntarios; _voluntariado = voluntariado; }

    private Guid UsuarioId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                                           ?? User.FindFirstValue("sub")!);
    private string? Email() => User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");

    [HttpPost("postular")]
    [AllowAnonymous]
    public async Task<IActionResult> Postular(PostulacionVoluntarioDto dto)
    {
        try { return Ok(await _voluntarios.PostularAsync(dto)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("mi-historial")]
    [Authorize(Roles = "VOLUNTARIO")]
    public async Task<IActionResult> MiHistorial()
    {
        var v = await _voluntarios.ObtenerPorUsuarioAsync(UsuarioId(), Email());
        return v == null ? NotFound(new { error = "No figuras en el registro de voluntarios" }) : Ok(v);
    }

    [HttpGet]
    [Authorize(Roles = Coordinacion)]
    public async Task<IActionResult> Listar() => Ok(await _voluntarios.ListarConHistorialAsync());

    [HttpPost("{id:guid}/participaciones")]
    [Authorize(Roles = Coordinacion)]
    public async Task<IActionResult> RegistrarParticipacion(Guid id, RegistrarParticipacionDto dto)
    {
        try { return Ok(await _voluntarios.RegistrarParticipacionAsync(id, dto.EventoId, dto.Descripcion, dto.Horas)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ---- FUJ-118: asignación a actividades y registro de horas ----

    // Eventos y cursos publicados que aún no terminaron, con cuántos voluntarios tienen asignados
    [HttpGet("actividades-asignables")]
    [Authorize(Roles = Coordinacion)]
    public async Task<IActionResult> ActividadesAsignables() => Ok(await _voluntariado.ActividadesAsignablesAsync());

    [HttpGet("asignaciones")]
    [Authorize(Roles = Coordinacion)]
    public async Task<IActionResult> Asignaciones([FromQuery] Guid? eventoId, [FromQuery] Guid? cursoId) =>
        Ok(await _voluntariado.ListarAsync(eventoId, cursoId));

    [HttpPost("asignaciones")]
    [Authorize(Roles = Coordinacion)]
    public async Task<IActionResult> Asignar(AsignarVoluntariosDto dto)
    {
        try { return Ok(await _voluntariado.AsignarAsync(dto, UsuarioId())); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("asignaciones/{id:guid}")]
    [Authorize(Roles = Coordinacion)]
    public async Task<IActionResult> CancelarAsignacion(Guid id)
    {
        try { await _voluntariado.CancelarAsync(id); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPut("asignaciones/{id:guid}/horas")]
    [Authorize(Roles = Coordinacion)]
    public async Task<IActionResult> RegistrarHoras(Guid id, RegistrarHorasDto dto)
    {
        try { return Ok(await _voluntariado.RegistrarHorasAsync(id, dto.Horas, UsuarioId())); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // El voluntario ve sus asignaciones (próximas y pasadas) en su portal
    [HttpGet("mis-asignaciones")]
    [Authorize(Roles = "VOLUNTARIO")]
    public async Task<IActionResult> MisAsignaciones() =>
        Ok(await _voluntariado.MisAsignacionesAsync(UsuarioId(), Email()));
}
