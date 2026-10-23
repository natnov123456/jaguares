using System.Security.Claims;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundacionJaguares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DonacionesController : ControllerBase
{
    private readonly IDonacionService _donaciones;
    public DonacionesController(IDonacionService donaciones) => _donaciones = donaciones;

    // RF-05: registro manual (efectivo en oficina, transferencias recibidas)
    [HttpPost]
    [Authorize(Roles = "DIRECTORA,ENCARGADO_EVENTOS")]
    public async Task<IActionResult> Registrar(CrearDonacionDto dto)
    {
        var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        Guid? registradoPor = Guid.TryParse(claimId, out var id) ? id : null;
        try { return Ok(await _donaciones.RegistrarAsync(dto, registradoPor)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // RF-09: donación en línea (QR / pasarela) — pública
    [HttpPost("en-linea")]
    [AllowAnonymous]
    public async Task<IActionResult> EnLinea(CrearDonacionDto dto)
    {
        try { return Ok(await _donaciones.RegistrarDonacionEnLineaAsync(dto)); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // RF-06: reporte financiero mensual
    [HttpGet("reporte/{anio:int}/{mes:int}")]
    [Authorize(Roles = "DIRECTORA,ENCARGADO_EVENTOS")]
    public async Task<IActionResult> Reporte(int anio, int mes)
    {
        if (mes is < 1 or > 12) return BadRequest(new { error = "Mes inválido" });
        var detalle = await _donaciones.ListarPeriodoAsync(anio, mes);
        var total = await _donaciones.TotalPeriodoAsync(anio, mes);
        return Ok(new ReporteDonacionesDto(anio, mes, total, detalle.Count,
            detalle.OrderByDescending(d => d.Fecha).ToList()));
    }
}
