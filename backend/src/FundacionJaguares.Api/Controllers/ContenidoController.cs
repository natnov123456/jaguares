using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContenidoController : ControllerBase
{
    private readonly IContenidoService _contenido;

    public ContenidoController(IContenidoService contenido)
    {
        _contenido = contenido;
    }

    [HttpGet("noticias")]
    [AllowAnonymous]
    public async Task<IActionResult> Noticias()
    {
        return Ok(await _contenido.ListarPublicadasAsync());
    }

    [HttpPost("noticias")]
    [Authorize(Roles = "DIRECTORA")]
    public async Task<IActionResult> CrearNoticia(CrearNoticiaDto dto)
    {
        var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? throw new UnauthorizedAccessException("Token sin ID de usuario");
        var autorId = Guid.Parse(claimId);
        return Ok(await _contenido.CrearNoticiaAsync(dto, autorId));
    }

    [HttpGet("galeria")]
    [AllowAnonymous]
    public async Task<IActionResult> Galeria()
    {
        return Ok(await _contenido.ListarGaleriaAsync());
    }
}