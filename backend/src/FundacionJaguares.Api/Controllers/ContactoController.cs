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
    private readonly IConsentimientoService _consentimientos;
    public ContactoController(IContactoService contacto, IConsentimientoService consentimientos)
    { _contacto = contacto; _consentimientos = consentimientos; }

    [HttpPost]
    [AllowAnonymous]
    [RequestSizeLimit(32 * 1024)] // un mensaje de texto nunca necesita más
    public async Task<IActionResult> Enviar(ConsultaContactoDto dto)
    {
        HttpContext.Items[RegistroActividadMiddleware.ClaveEmail] = dto.Email?.Trim();
        // Las dos casillas se informan junto con los demás campos marcados
        var faltan = new Dictionary<string, string>();
        if (dto.AceptaTerminos != true) faltan["aceptaTerminos"] = "Debes aceptar los Términos y condiciones";
        if (dto.AceptaPrivacidad != true) faltan["aceptaPrivacidad"] = "Debes aceptar la Política de privacidad";
        try
        {
            if (faltan.Count > 0) throw new ValidacionException(faltan);
            await _contacto.EnviarAsync(dto, HttpContext.Connection.RemoteIpAddress?.ToString());
            await _consentimientos.RegistrarAsync(Formularios.Contacto, null, dto.Email, HttpContext.Origen());
            return Ok(new { mensaje = "¡Gracias! Recibimos tu consulta y te responderemos a tu correo." });
        }
        catch (ValidacionException ex) { return BadRequest(new { error = "Revisa los datos marcados", errores = ex.Errores }); }
        catch (LimiteExcedidoException ex) { return StatusCode(StatusCodes.Status429TooManyRequests, new { error = ex.Message }); }
    }
}
