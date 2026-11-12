using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FundacionJaguares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CursosController : ControllerBase
{
    private readonly ICursoService _cursos;
    private readonly ICambiosActividadService _cambios;
    public CursosController(ICursoService cursos, ICambiosActividadService cambios) { _cursos = cursos; _cambios = cambios; }

    // ---- FUJ-119: reprogramar o cancelar avisa a inscritos y lista de espera. El docente solo sus cursos ----

    [HttpPut("{id:guid}/reprogramar")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> Reprogramar(Guid id, ReprogramarCursoDto dto)
    {
        var docenteId = User.IsInRole("DIRECTORA") ? (Guid?)null : UsuarioId();
        try { return Ok(await _cambios.ReprogramarCursoAsync(id, dto, docenteId)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> CancelarCurso(Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelarActividadDto? dto)
    {
        var docenteId = User.IsInRole("DIRECTORA") ? (Guid?)null : UsuarioId();
        try { return Ok(await _cambios.CancelarCursoAsync(id, dto?.Motivo, docenteId)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet]
    public async Task<IActionResult> Listar() => Ok(await _cursos.ListarPublicadosAsync());

    private Guid UsuarioId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                                           ?? User.FindFirstValue("sub")!);

    // null: la directora gestiona cualquier curso; el docente, solo los que le asignaron
    private Guid? DocenteFiltro() => User.IsInRole("DIRECTORA") ? null : UsuarioId();

    // Solo la directora crea cursos y elige al docente a cargo
    [HttpPost]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> Crear(CrearCursoDto dto)
    {
        try { return Ok(await _cursos.CrearAsync(dto)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("docentes")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> Docentes() => Ok(await _cursos.ListarDocentesAsync());

    [HttpPut("{id:guid}/docente")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> AsignarDocente(Guid id, AsignarDocenteDto dto)
    {
        try { await _cursos.AsignarDocenteAsync(id, dto.DocenteId); return NoContent(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}/gestion")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> Detalle(Guid id)
    {
        try { return Ok(await _cursos.DetalleAsync(id, DocenteFiltro())); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> Editar(Guid id, EditarCursoDto dto)
    {
        try { return Ok(await _cursos.EditarAsync(id, dto, DocenteFiltro())); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // Asistencia de un día: inscritos con su marca de ese día y su porcentaje acumulado
    [HttpGet("{id:guid}/asistencia")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> Asistencia(Guid id, [FromQuery] DateOnly fecha)
    {
        try { return Ok(await _cursos.AsistenciaDiaAsync(id, fecha, DocenteFiltro())); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/asistencia")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> RegistrarAsistencia(Guid id, RegistrarAsistenciaCursoDto dto)
    {
        try { return Ok(await _cursos.RegistrarAsistenciaAsync(id, dto, DocenteFiltro(), UsuarioId())); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}/mi-asistencia")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> MiAsistencia(Guid id)
    {
        try { return Ok(await _cursos.MiAsistenciaAsync(id, UsuarioId())); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // Panel de gestión: el docente ve solo sus cursos, la directora todos
    [HttpGet("gestion")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> Gestion() =>
        Ok(await _cursos.ListarGestionAsync(User.IsInRole("DIRECTORA") ? null : UsuarioId()));

    [HttpGet("mis-inscripciones")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> MisInscripciones() =>
        Ok(await _cursos.ListarInscripcionesAsync(UsuarioId()));

    [HttpPost("{id:guid}/inscribir")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> Inscribir(Guid id)
    {
        var participanteId = UsuarioId();
        try { return Ok(await _cursos.InscribirAsync(id, participanteId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}/inscripcion")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        var participanteId = UsuarioId();
        try { await _cursos.CancelarInscripcionAsync(id, participanteId); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ---- FUJ-111: lista de espera ----

    [HttpGet("mi-lista-espera")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> MiListaEspera() => Ok(await _cursos.MiListaEsperaAsync(UsuarioId()));

    [HttpPost("{id:guid}/lista-espera")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> UnirseListaEspera(Guid id)
    {
        try { return Ok(await _cursos.UnirseListaEsperaAsync(id, UsuarioId())); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("{id:guid}/lista-espera")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> SalirListaEspera(Guid id)
    {
        try { await _cursos.SalirListaEsperaAsync(id, UsuarioId()); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // El docente ve la lista de sus cursos; la directora, la de todos
    [HttpGet("{id:guid}/lista-espera")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> ListaEspera(Guid id)
    {
        var docenteId = User.IsInRole("DIRECTORA") ? (Guid?)null : UsuarioId();
        try { return Ok(await _cursos.ListarEsperaGestionAsync(id, docenteId)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // FUJ-115: plantilla con la que se emitirán los certificados del curso (null = diseño estándar)
    [HttpPut("{id:guid}/plantilla")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> AsignarPlantilla(Guid id, AsignarPlantillaDto dto)
    {
        var docenteId = User.IsInRole("DIRECTORA") ? (Guid?)null : UsuarioId();
        try
        {
            await _cursos.AsignarPlantillaAsync(id, dto.PlantillaId, docenteId);
            return NoContent();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/finalizar")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> Finalizar(Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CerrarActividadDto? dto)
    {
        var docenteId = User.IsInRole("DIRECTORA") ? (Guid?)null : UsuarioId();
        try
        {
            // FUJ-120: el cuerpo es opcional; sin preguntas se envía la encuesta por defecto
            return Ok(await _cursos.FinalizarYGenerarCertificadosAsync(id, docenteId, UsuarioId(), dto?.Preguntas));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}