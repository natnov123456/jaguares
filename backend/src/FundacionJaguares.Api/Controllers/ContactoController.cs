using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

// FUJ-127: formulario de contacto público → correo institucional (con protección anti-spam)
[ApiController]
[Route("api/[controller]")]
public class ContactoController : ControllerBase
{
    private readonly IContactoService _contacto;
    public ContactoController(IContactoService contacto) => _contacto = contacto;

    [HttpPost]
    [AllowAnonymous]
    [RequestSizeLimit(32 * 1024)] // un mensaje de texto nunca necesita más
    public async Task<IActionResult> Enviar(ConsultaContactoDto dto)
    {
        try
        {
            await _contacto.EnviarAsync(dto, HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { mensaje = "¡Gracias! Recibimos tu consulta y te responderemos a tu correo." });
        }
        catch (ValidacionException ex) { return BadRequest(new { error = "Revisa los datos marcados", errores = ex.Errores }); }
        catch (LimiteExcedidoException ex) { return StatusCode(StatusCodes.Status429TooManyRequests, new { error = ex.Message }); }
    }
}
