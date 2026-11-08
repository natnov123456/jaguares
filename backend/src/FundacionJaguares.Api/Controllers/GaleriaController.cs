using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

// FUJ-126: galería multimedia institucional (álbumes de fotos y videos)
[ApiController]
[Route("api/[controller]")]
public class GaleriaController : ControllerBase
{
    // Hasta 10 imágenes de 5 MB por envío (más el margen del formulario)
    private const long LimiteEnvio = 10 * GaleriaService.MaxBytes + 1024 * 1024;

    private readonly IGaleriaService _galeria;
    public GaleriaController(IGaleriaService galeria) => _galeria = galeria;

    private bool EsDirectora => User.IsInRole("DIRECTORA");

    // ---- Público ----

    [HttpGet("albumes")]
    [AllowAnonymous]
    public async Task<IActionResult> Albumes() => Ok(await _galeria.ListarAlbumesAsync(false));

    // La Directora también puede ver álbumes no publicados (vista previa)
    [HttpGet("albumes/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Album(Guid id)
    {
        var a = await _galeria.ObtenerAlbumAsync(id, EsDirectora);
        return a is null ? NotFound(new { error = "Álbum no encontrado" }) : Ok(a);
    }

    // ---- Administración (Directora) ----

    [HttpGet("admin/albumes")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> AlbumesAdmin() => Ok(await _galeria.ListarAlbumesAsync(true));

    [HttpPost("albumes")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> CrearAlbum(GuardarAlbumDto dto)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        try { return Ok(await _galeria.CrearAlbumAsync(dto, id)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("albumes/{id:guid}")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> ActualizarAlbum(Guid id, GuardarAlbumDto dto)
    {
        try { return Ok(await _galeria.ActualizarAlbumAsync(id, dto)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("albumes/{id:guid}")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> EliminarAlbum(Guid id)
    {
        try { await _galeria.EliminarAlbumAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // multipart/form-data con uno o varios campos "archivos"
    [HttpPost("albumes/{id:guid}/imagenes")]
    [Authorize(Roles = "DIRECTORA")]
    [RequestSizeLimit(LimiteEnvio)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteEnvio)]
    public async Task<IActionResult> SubirImagenes(Guid id, [FromForm] List<IFormFile> archivos)
    {
        if (archivos.Count > 10) return BadRequest(new { error = "Máximo 10 imágenes por envío" });
        var streams = new List<Stream>();
        try
        {
            var lista = archivos.Select(f =>
            {
                var s = f.OpenReadStream();
                streams.Add(s);
                return new ArchivoSubido(f.FileName, f.Length, s);
            }).ToList();
            var r = await _galeria.SubirImagenesAsync(id, lista);
            // Si ninguna imagen se pudo guardar, se responde 400 con el detalle de cada error
            return r.Subidas.Count == 0 && r.Errores.Count > 0 ? BadRequest(r) : Ok(r);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        finally { streams.ForEach(s => s.Dispose()); }
    }

    [HttpPost("albumes/{id:guid}/videos")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> AgregarVideo(Guid id, AgregarVideoDto dto)
    {
        try { return Ok(await _galeria.AgregarVideoAsync(id, dto)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("items/{id:guid}")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> EliminarItem(Guid id)
    {
        try { await _galeria.EliminarItemAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}
