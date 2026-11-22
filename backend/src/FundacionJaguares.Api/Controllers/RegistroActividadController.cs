using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

// Registro de actividad: quién entró, a qué hora, desde dónde y qué hizo. Solo la Dirección lo consulta.
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "DIRECTORA")]
public class RegistroActividadController(IRegistroActividadService registro) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? q, [FromQuery] string? resultado, [FromQuery] DateOnly? desde,
                                            [FromQuery] DateOnly? hasta, [FromQuery] Guid? usuarioId,
                                            [FromQuery] int pagina = 1, [FromQuery] int tamano = 50)
    {
        try { return Ok(await registro.ListarAsync(new FiltroRegistroActividad(q, resultado, desde, hasta, usuarioId, pagina, tamano))); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Vigilancia de actividad sospechosa de las últimas horas (por defecto 24)
    [HttpGet("seguridad")]
    public async Task<IActionResult> Seguridad([FromQuery] int horas = 24) => Ok(await registro.ResumenSeguridadAsync(horas));
}
