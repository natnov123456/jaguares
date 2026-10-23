using FundacionJaguares.Application.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CertificadosController : ControllerBase
{
    private readonly ICertificadoService _certs;
    public CertificadosController(ICertificadoService certs) => _certs = certs;

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
}