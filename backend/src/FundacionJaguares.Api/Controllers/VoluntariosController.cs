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
    private readonly IVoluntarioService _voluntarios;
    public VoluntariosController(IVoluntarioService voluntarios) => _voluntarios = voluntarios;

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
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("sub")!);
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        var v = await _voluntarios.ObtenerPorUsuarioAsync(id, email);
        return v == null ? NotFound(new { error = "No figuras en el registro de voluntarios" }) : Ok(v);
    }

    [HttpGet]
    [Authorize(Roles = "DIRECTORA,ENCARGADO_EVENTOS")]
    public async Task<IActionResult> Listar() => Ok(await _voluntarios.ListarConHistorialAsync());

    [HttpPost("{id:guid}/participaciones")]
    [Authorize(Roles = "DIRECTORA,ENCARGADO_EVENTOS")]
    public async Task<IActionResult> RegistrarParticipacion(Guid id, RegistrarParticipacionDto dto)
    {
        try { return Ok(await _voluntarios.RegistrarParticipacionAsync(id, dto.EventoId, dto.Descripcion)); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}