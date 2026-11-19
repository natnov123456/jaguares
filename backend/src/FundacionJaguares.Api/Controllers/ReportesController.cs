using System.Security.Claims;
using FundacionJaguares.Application.Reportes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

// JAG-86 / FUJ-124: exportación de reportes en PDF (plantilla institucional) o Excel.
//   GET /api/Reportes                         → reportes que el rol actual puede exportar
//   GET /api/Reportes/{clave}?formato=pdf|xlsx|json&q=&estado=&desde=&hasta=&actividadId=&rol=&accion=&usuarioId=
// Los permisos se verifican en ReporteService antes de consultar los datos.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportesController : ControllerBase
{
    private readonly IReporteService _reportes;
    public ReportesController(IReporteService reportes) => _reportes = reportes;

    private SolicitanteReporte Solicitante() => new(
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!),
        User.FindFirstValue(ClaimTypes.Role) ?? "");

    [HttpGet]
    public IActionResult Disponibles() => Ok(_reportes.Disponibles(Solicitante()));

    [HttpGet("{clave}")]
    public async Task<IActionResult> Exportar(string clave, [FromQuery] string formato = "pdf",
        [FromQuery] string? q = null, [FromQuery] string? estado = null, [FromQuery] DateOnly? desde = null,
        [FromQuery] DateOnly? hasta = null, [FromQuery] Guid? actividadId = null, [FromQuery] string? rol = null,
        [FromQuery] string? accion = null, [FromQuery] Guid? usuarioId = null)
    {
        if (!Enum.TryParse<FormatoReporte>(formato, true, out var fmt))
            return BadRequest(new { error = "Formato no válido: usa pdf, xlsx o json" });
        var filtros = new FiltrosReporte(q, estado, desde, hasta, actividadId, rol, accion, usuarioId);
        try
        {
            if (fmt == FormatoReporte.JSON) return Ok(await _reportes.VistaPreviaAsync(clave, Solicitante(), filtros));
            var archivo = await _reportes.ExportarAsync(clave, fmt, Solicitante(), filtros);
            Response.Headers.CacheControl = "no-store"; // contiene datos personales: que no quede en cachés intermedias
            Response.Headers["X-Filas-Exportadas"] = archivo.Filas.ToString();
            return File(archivo.Contenido, archivo.TipoContenido, archivo.NombreArchivo);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (SinDatosException ex) { return UnprocessableEntity(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return StatusCode(500, new { error = ex.Message }); }
    }
}
