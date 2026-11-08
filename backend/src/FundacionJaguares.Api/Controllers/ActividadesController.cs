using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

// FUJ-114: calendario público de cursos y eventos
[ApiController]
[Route("api/[controller]")]
public class ActividadesController : ControllerBase
{
    private readonly IActividadService _actividades;
    public ActividadesController(IActividadService actividades) => _actividades = actividades;

    // GET /api/Actividades?desde=2026-11-01&hasta=2026-11-30
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Listar([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        if (desde is null || hasta is null)
            return BadRequest(new { error = "Debe indicar 'desde' y 'hasta' (formato AAAA-MM-DD)" });
        try { return Ok(await _actividades.ListarPorRangoAsync(desde.Value, hasta.Value)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
