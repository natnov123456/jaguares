using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FundacionJaguares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventosController : ControllerBase
{
    private readonly IEventoService _eventos;
    private readonly IAsistenciaService _asistencia;
    private readonly ICambiosActividadService _cambios;
    public EventosController(IEventoService eventos, IAsistenciaService asistencia, ICambiosActividadService cambios)
    { _eventos = eventos; _asistencia = asistencia; _cambios = cambios; }

    // ---- FUJ-119: reprogramar o cancelar avisa por correo a todos los inscritos ----

    [HttpPut("{id:guid}/reprogramar")]
    [Authorize(Roles = "ENCARGADO_EVENTOS,DIRECTORA")]
    public async Task<IActionResult> Reprogramar(Guid id, ReprogramarEventoDto dto)
    {
        try { return Ok(await _cambios.ReprogramarEventoAsync(id, dto)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Roles = "ENCARGADO_EVENTOS,DIRECTORA")]
    public async Task<IActionResult> CancelarEvento(Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelarActividadDto? dto)
    {
        try { return Ok(await _cambios.CancelarEventoAsync(id, dto?.Motivo)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

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

    // FUJ-112: cancelar la inscripción a un evento desde "Mi panel"
    [HttpDelete("{id:guid}/inscripcion")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        try { await _asistencia.CancelarAsync(id, UsuarioId()); return NoContent(); }
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
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // FUJ-120: cierra la asistencia, finaliza el evento y envía la encuesta a los asistentes.
    // El cuerpo es opcional: sin preguntas se usan las de por defecto.
    [HttpPost("{id:guid}/cerrar-asistencia")]
    [Authorize(Roles = "ENCARGADO_EVENTOS,DIRECTORA")]
    public async Task<IActionResult> CerrarAsistencia(Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CerrarActividadDto? dto)
    {
        try { return Ok(await _eventos.CerrarAsistenciaAsync(id, dto?.Preguntas, UsuarioId())); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // Eventos para la gestión: incluye los ya realizados y cerrados (el listado público solo muestra los abiertos)
    [HttpGet("gestion")]
    [Authorize(Roles = "ENCARGADO_EVENTOS,DIRECTORA")]
    public async Task<IActionResult> Gestion() => Ok(await _eventos.ListarGestionAsync());

    [HttpGet("{id:guid}/asistencias")]
    [Authorize(Roles = "ENCARGADO_EVENTOS,DIRECTORA")]
    public async Task<IActionResult> Asistencias(Guid id) => Ok(await _eventos.ListarAsistenciasAsync(id));
}