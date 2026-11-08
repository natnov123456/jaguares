using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

// FUJ-117: campañas de donación con meta y progreso
[ApiController]
[Route("api/[controller]")]
public class CampanasController : ControllerBase
{
    private readonly IDonacionService _donaciones;
    public CampanasController(IDonacionService donaciones) => _donaciones = donaciones;

    // Campañas vigentes publicadas en el sitio
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Vigentes() => Ok(await _donaciones.ListarCampanasAsync(soloVigentes: true));

    [HttpGet("admin")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> Todas() => Ok(await _donaciones.ListarCampanasAsync(soloVigentes: false));

    [HttpPost]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> Crear(GuardarCampanaDto dto)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        try { return Ok(await _donaciones.CrearCampanaAsync(dto, id)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> Actualizar(Guid id, GuardarCampanaDto dto)
    {
        try { return Ok(await _donaciones.ActualizarCampanaAsync(id, dto)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}
