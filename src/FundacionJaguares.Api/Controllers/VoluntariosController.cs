using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FundacionJaguares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VoluntariosController : ControllerBase
{
    private readonly IVoluntarioService _voluntarios;
    public VoluntariosController(IVoluntarioService voluntarios) => _voluntarios = voluntarios;

    [HttpPost("postular")]
    [AllowAnonymous]
    public async Task<IActionResult> Postular(PostulacionVoluntarioDto dto)
    {
        try { return Ok(await _voluntarios.PostularAsync(dto)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet]
    [Authorize(Roles = "DIRECTORA,ENCARGADO_EVENTOS")]
    public async Task<IActionResult> Listar() => Ok(await _voluntarios.ListarAsync());
}