using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

// FUJ-121: sección pública de transparencia (uso de las donaciones por mes)
[ApiController]
[Route("api/[controller]")]
public class TransparenciaController : ControllerBase
{
    private readonly ITransparenciaService _transparencia;
    public TransparenciaController(ITransparenciaService transparencia) => _transparencia = transparencia;

    // Meses publicados (para el selector del sitio)
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Meses() => Ok(await _transparencia.ListarMesesPublicadosAsync());

    // Resumen público de un mes: solo totales y destinos por categoría
    [HttpGet("{anio:int}/{mes:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Resumen(int anio, int mes)
    {
        try
        {
            var r = await _transparencia.ObtenerPublicadoAsync(anio, mes);
            return r is null ? NotFound(new { error = "Ese mes todavía no fue publicado" }) : Ok(r);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Totales actuales del reporte financiero + lo ya publicado, para preparar la publicación
    [HttpGet("vista-previa/{anio:int}/{mes:int}")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> VistaPrevia(int anio, int mes)
    {
        try { return Ok(await _transparencia.VistaPreviaAsync(anio, mes)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Publica (o vuelve a publicar con datos actualizados) el resumen del mes
    [HttpPut("{anio:int}/{mes:int}")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> Publicar(int anio, int mes, PublicarTransparenciaDto dto)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);
        try { return Ok(await _transparencia.PublicarAsync(anio, mes, dto, id)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{anio:int}/{mes:int}")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> Despublicar(int anio, int mes)
    {
        try { return await _transparencia.DespublicarAsync(anio, mes) ? NoContent() : NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
