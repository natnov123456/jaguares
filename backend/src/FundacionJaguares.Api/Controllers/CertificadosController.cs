using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CertificadosController : ControllerBase
{
    private const long LimiteEnvio = ProcesadorImagen.MaxBytes + 1024 * 1024; // una imagen más el margen del formulario

    private readonly ICertificadoService _certs;
    private readonly IPlantillaService _plantillas;

    public CertificadosController(ICertificadoService certs, IPlantillaService plantillas)
    { _certs = certs; _plantillas = plantillas; }

    [HttpGet("verificar/{codigo}")]
    [AllowAnonymous]
    public async Task<IActionResult> Verificar(string codigo) =>
        Ok(await _certs.VerificarAsync(codigo));

    [HttpGet("mios")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> Mios()
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("sub")!);
        return Ok(await _certs.ListarPorParticipanteAsync(id));
    }

    // FUJ-112: descarga del PDF desde "Mi panel" (solo el dueño del certificado)
    [HttpGet("{id:guid}/pdf")]
    [Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
    public async Task<IActionResult> Pdf(Guid id)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub")!);
        try
        {
            var pdf = await _certs.DescargarPdfAsync(id, usuarioId);
            return File(pdf.Pdf, "application/pdf", pdf.NombreArchivo);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---- Plantillas (FUJ-115) ----

    // Los docentes las consultan para asignarlas a sus cursos; solo la Directora las modifica
    [HttpGet("plantillas")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> Plantillas() => Ok(await _plantillas.ListarAsync());

    [HttpPost("plantillas")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> CrearPlantilla(GuardarPlantillaDto dto)
    {
        try { return Ok(await _plantillas.CrearAsync(dto)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("plantillas/{id:guid}")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> ActualizarPlantilla(Guid id, GuardarPlantillaDto dto)
    {
        try { return Ok(await _plantillas.ActualizarAsync(id, dto)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("plantillas/{id:guid}")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> EliminarPlantilla(Guid id)
    {
        try { await _plantillas.EliminarAsync(id); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // campo: logo | firmaUno | firmaDos. multipart/form-data con el campo "archivo"
    [HttpPost("plantillas/{id:guid}/imagenes/{campo}")]
    [Authorize(Roles = "DIRECTORA")]
    [RequestSizeLimit(LimiteEnvio)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteEnvio)]
    public async Task<IActionResult> SubirImagen(Guid id, string campo, IFormFile archivo)
    {
        try
        {
            await using var contenido = archivo.OpenReadStream();
            return Ok(await _plantillas.SubirImagenAsync(id, campo, new ArchivoSubido(archivo.FileName, archivo.Length, contenido)));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("plantillas/{id:guid}/imagenes/{campo}")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> QuitarImagen(Guid id, string campo)
    {
        try { return Ok(await _plantillas.QuitarImagenAsync(id, campo)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("plantillas/{id:guid}/vista-previa")]
    [Authorize(Roles = "DOCENTE,DIRECTORA")]
    public async Task<IActionResult> VistaPrevia(Guid id)
    {
        try { return File(await _plantillas.PrevisualizarAsync(id), "application/pdf"); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}
