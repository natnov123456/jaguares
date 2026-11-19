using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

// JAG-82 / FUJ-122: administración de cuentas y roles, solo para la Dirección
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "DIRECTORA")]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarios;
    public UsuariosController(IUsuarioService usuarios) => _usuarios = usuarios;

    private Guid UsuarioId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? q, [FromQuery] Rol? rol, [FromQuery] bool? activo) =>
        Ok(await _usuarios.ListarAsync(q, rol, activo));

    [HttpGet("roles")]
    public async Task<IActionResult> Roles() => Ok(await _usuarios.RolesAsync());

    [HttpPost]
    public async Task<IActionResult> Crear(CrearUsuarioDto dto)
    {
        try { return Ok(await _usuarios.CrearAsync(dto, UsuarioId())); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/rol")]
    public async Task<IActionResult> CambiarRol(Guid id, CambiarRolDto dto)
    {
        try { return Ok(await _usuarios.CambiarRolAsync(id, dto.Rol, UsuarioId())); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/activo")]
    public async Task<IActionResult> CambiarActivo(Guid id, CambiarActivoDto dto)
    {
        try { return Ok(await _usuarios.CambiarActivoAsync(id, dto.Activo, UsuarioId())); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/reenviar-enlace")]
    public async Task<IActionResult> ReenviarEnlace(Guid id)
    {
        try { await _usuarios.ReenviarEnlaceAsync(id); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}
