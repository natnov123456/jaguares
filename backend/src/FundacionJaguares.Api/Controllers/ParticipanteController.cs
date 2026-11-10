using System.Security.Claims;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

// FUJ-112: panel del participante ("Mi panel")
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "PARTICIPANTE,VOLUNTARIO")]
public class ParticipanteController : ControllerBase
{
    private readonly IParticipanteService _participantes;
    public ParticipanteController(IParticipanteService participantes) => _participantes = participantes;

    private Guid UsuarioId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                                           ?? User.FindFirstValue("sub")!);

    // Cursos y eventos (próximos y pasados), certificados y lista de espera en una sola llamada
    [HttpGet("panel")]
    public async Task<IActionResult> Panel() => Ok(await _participantes.PanelAsync(UsuarioId()));
}
