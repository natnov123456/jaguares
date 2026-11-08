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
}
