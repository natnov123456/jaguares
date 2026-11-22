using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

// FUJ-127: página "Quiénes somos" (pública) y su edición por la Directora
[ApiController]
[Route("api/[controller]")]
public class InstitucionalController : ControllerBase
{
    private readonly IInstitucionalService _institucional;
    public InstitucionalController(IInstitucionalService institucional) => _institucional = institucional;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Obtener() => Ok(await _institucional.ObtenerAsync());

    [HttpPut]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> Actualizar(PaginaInstitucionalDto dto)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        try { return Ok(await _institucional.ActualizarAsync(dto, id)); }
        catch (ValidacionException ex) { return BadRequest(new { error = "Revisa los datos marcados", errores = ex.Errores }); }
    }

    private Guid UsuarioId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    // Datos para donar (cuenta bancaria y si hay QR): públicos, se muestran en la página de donaciones
    [HttpGet("datos-pago")]
    [AllowAnonymous]
    public async Task<IActionResult> DatosPago() => Ok(await _institucional.ObtenerDatosPagoAsync());

    [HttpPut("datos-pago")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> GuardarDatosPago(GuardarDatosPagoDto dto)
    {
        try { return Ok(await _institucional.GuardarDatosPagoAsync(dto, UsuarioId())); }
        catch (ValidacionException ex) { return BadRequest(new { error = "Revisa los datos marcados", errores = ex.Errores }); }
    }

    // Imagen del QR de la cuenta de la fundación (pública)
    [HttpGet("qr-donacion")]
    [AllowAnonymous]
    public async Task<IActionResult> QrDonacion()
    {
        var qr = await _institucional.ObtenerQrAsync();
        if (qr is null) return NotFound();
        Response.Headers.CacheControl = "no-cache";
        return File(qr.Value.Imagen, qr.Value.Tipo);
    }

    // Subir (con archivo) o quitar (sin archivo) el QR
    [HttpPost("qr-donacion")]
    [Authorize(Roles = "DIRECTORA")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> SubirQr(IFormFile? archivo)
    {
        try
        {
            var a = archivo is null ? null : new ArchivoSubido(archivo.FileName, archivo.Length, archivo.OpenReadStream());
            return Ok(await _institucional.GuardarQrAsync(a, UsuarioId()));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
