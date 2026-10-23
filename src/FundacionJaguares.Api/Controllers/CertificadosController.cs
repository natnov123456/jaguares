using FundacionJaguares.Application.Services;
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
}