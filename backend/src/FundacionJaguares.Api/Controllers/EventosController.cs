using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventosController : ControllerBase
{
    private readonly IEventoService _eventos;
    public EventosController(IEventoService eventos) => _eventos = eventos;

    [HttpGet]
    public async Task<IActionResult> Listar() => Ok(await _eventos.ListarPublicadosAsync());

    [HttpPost]
    [Authorize(Roles = "ENCARGADO_EVENTOS,DIRECTORA")]
    public async Task<IActionResult> Crear(CrearEventoDto dto)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("sub")!);
        return Ok(await _eventos.CrearAsync(dto, id));
    }

    [HttpPost("{id:guid}/asistencia")]
    [Authorize(Roles = "ENCARGADO_EVENTOS,DIRECTORA")]
    public async Task<IActionResult> RegistrarAsistencia(Guid id, AsistenciaDto dto)
    {
        var quien = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub")!);
        try { return Ok(await _eventos.RegistrarAsistenciaAsync(id, dto.ParticipanteId, quien)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}/asistencias")]
    [Authorize(Roles = "ENCARGADO_EVENTOS,DIRECTORA")]
    public async Task<IActionResult> Asistencias(Guid id) => Ok(await _eventos.ListarAsistenciasAsync(id));
}