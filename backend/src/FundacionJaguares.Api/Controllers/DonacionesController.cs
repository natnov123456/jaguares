using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FundacionJaguares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DonacionesController : ControllerBase
{
    private readonly IDonacionService _donaciones;
    private readonly IConsentimientoService _consentimientos;
    public DonacionesController(IDonacionService donaciones, IConsentimientoService consentimientos)
    { _donaciones = donaciones; _consentimientos = consentimientos; }

    // RF-05: registro manual (efectivo en oficina, transferencias recibidas)
    [HttpPost]
    [Authorize(Roles = "DIRECTORA,CONTADOR,ENCARGADO_EVENTOS")]
    public async Task<IActionResult> Registrar(CrearDonacionDto dto)
    {
        var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        Guid? registradoPor = Guid.TryParse(claimId, out var id) ? id : null;
        try { return Ok(await _donaciones.RegistrarAsync(dto, registradoPor)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // RF-09: donación en línea (QR / pasarela) — pública
    // Con sesión iniciada la donación queda en la cuenta; el límite evita usarlo para enviar correos a terceros
    [HttpPost("en-linea")]
    [AllowAnonymous]
    [EnableRateLimiting("donacion-en-linea")]
    public async Task<IActionResult> EnLinea(CrearDonacionDto dto)
    {
        var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        Guid? usuarioId = Guid.TryParse(claimId, out var id) ? id : null;
        try
        {
            // La donación en línea pide datos personales: exige las dos aceptaciones
            ConsentimientoService.Exigir(dto.AceptaTerminos, dto.AceptaPrivacidad);
            var d = await _donaciones.RegistrarDonacionEnLineaAsync(dto, usuarioId);
            var email = dto.DonanteEmail ?? User.FindFirstValue(ClaimTypes.Email);
            await _consentimientos.RegistrarAsync(Formularios.Donacion, usuarioId, email, HttpContext.Origen());
            return Ok(d);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // FUJ-116: donaciones del usuario con sesión
    [HttpGet("mias")]
    [Authorize]
    public async Task<IActionResult> Mias() => Ok(await _donaciones.ListarMiasAsync(UsuarioId()));

    // FUJ-116: comprobante en PDF (donante, quien registró la donación o Directora)
    [HttpGet("{id:guid}/comprobante")]
    [Authorize]
    public async Task<IActionResult> Comprobante(Guid id)
    {
        try
        {
            var c = await _donaciones.ObtenerComprobanteAsync(id, UsuarioId(), User.IsInRole("DIRECTORA") || User.IsInRole("CONTADOR"));
            return File(c.Pdf, "application/pdf", c.NombreArchivo);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    private Guid UsuarioId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                                           ?? User.FindFirstValue("sub")!);

    // Donaciones que registró el usuario actual (el encargado no ve el reporte financiero completo)
    [HttpGet("mis-registros")]
    [Authorize(Roles = "DIRECTORA,CONTADOR,ENCARGADO_EVENTOS")]
    public async Task<IActionResult> MisRegistros()
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("sub")!);
        return Ok(await _donaciones.ListarRegistradasPorAsync(id));
    }

    // FUJ-125: corregir o anular una donación (queda en la bitácora de auditoría)
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "DIRECTORA,CONTADOR")]
    public async Task<IActionResult> Editar(Guid id, EditarDonacionDto dto)
    {
        try { return Ok(await _donaciones.EditarAsync(id, dto, UsuarioId())); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/anular")]
    [Authorize(Roles = "DIRECTORA,CONTADOR")]
    public async Task<IActionResult> Anular(Guid id, AnularDonacionDto dto)
    {
        try { return Ok(await _donaciones.AnularAsync(id, dto.Motivo, UsuarioId())); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // RF-06: reporte financiero mensual (solo dirección)
    [HttpGet("reporte/{anio:int}/{mes:int}")]
    [Authorize(Roles = "DIRECTORA,CONTADOR")]
    public async Task<IActionResult> Reporte(int anio, int mes)
    {
        if (mes is < 1 or > 12) return BadRequest(new { error = "Mes inválido" });
        var detalle = await _donaciones.ListarPeriodoAsync(anio, mes);
        var total = await _donaciones.TotalPeriodoAsync(anio, mes);
        return Ok(new ReporteDonacionesDto(anio, mes, total, detalle.Count,
            detalle.OrderByDescending(d => d.Fecha).ToList()));
    }
}
