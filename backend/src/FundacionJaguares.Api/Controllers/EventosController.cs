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
    private readonly IAsistenciaService _asistencia;
    public EventosController(IEventoService eventos, IAsistenciaService asistencia)
    { _eventos = eventos; _asistencia = asistencia; }

    private Guid UsuarioId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                                           ?? User.FindFirstValue("sub")!);

    // ---- FUJ-113: inscripción con QR y check-in ----

    [HttpPost("{id:guid}/inscribir")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> Inscribir(Guid id)
    {
        try { return Ok(await _asistencia.InscribirAsync(id, UsuarioId())); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("mis-inscripciones")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> MisInscripciones() => Ok(await _asistencia.MisInscripcionesAsync(UsuarioId()));

    [HttpGet("mis-inscripciones/{inscripcionId:guid}/qr")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> Qr(Guid inscripcionId)
    {
        try { return Content(await _asistencia.GenerarQrSvgAsync(inscripcionId, UsuarioId()), "image/svg+xml"); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/checkin")]
    [Authorize(Roles = "ENCARGADO_EVENTOS,DIRECTORA")]
    public async Task<IActionResult> Checkin(Guid id, CheckinDto dto)
    {
        var r = await _asistencia.CheckinAsync(id, dto.Codigo, UsuarioId());
        return r.Resultado switch
        {
            "REGISTRADO" => Ok(r),
            "YA_REGISTRADO" => Conflict(r),
            "INVALIDO" => NotFound(r),
            _ => BadRequest(r)
        };
    }

    [HttpGet("{id:guid}/inscritos")]
    [Authorize(Roles = "ENCARGADO_EVENTOS,DIRECTORA")]
    public async Task<IActionResult> Inscritos(Guid id) => Ok(await _asistencia.ListarInscritosAsync(id));

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