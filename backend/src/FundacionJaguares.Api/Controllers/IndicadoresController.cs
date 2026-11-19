using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

// JAG-84 / FUJ-123: panel de indicadores de la Directora.  GET /api/Indicadores?mes=AAAA-MM (por defecto, el mes actual)
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "DIRECTORA")]
public class IndicadoresController : ControllerBase
{
    private readonly IIndicadorService _indicadores;
    public IndicadoresController(IIndicadorService indicadores) => _indicadores = indicadores;

    [HttpGet]
    public async Task<IActionResult> Obtener([FromQuery] string? mes)
    {
        try
        {
            var (anio, m) = IndicadorService.Parsear(mes, DateTime.UtcNow);
            return Ok(await _indicadores.ObtenerAsync(anio, m));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
