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

    // Formulario de donación en línea (multipart: datos + foto o PDF del comprobante del banco)
    public class DonacionEnLineaForm
    {
        // Texto con punto decimal ("50.5"): se interpreta igual en cualquier idioma del servidor
        public string? Monto { get; set; }
        public string? Medio { get; set; }
        public string? Referencia { get; set; }
        public string? DonanteNombre { get; set; }
        public string? DonanteEmail { get; set; }
        public Guid? CampanaId { get; set; }
        public bool? AceptaTerminos { get; set; }
        public bool? AceptaPrivacidad { get; set; }
        public IFormFile? ComprobantePago { get; set; }
    }

    // RF-09: donación en línea por QR o transferencia — pública. Queda "en verificación" hasta que Contabilidad
    // o la Dirección confirme el pago en el banco. Con sesión iniciada queda en la cuenta del usuario.
    [HttpPost("en-linea")]
    [AllowAnonymous]
    [EnableRateLimiting("donacion-en-linea")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> EnLinea([FromForm] DonacionEnLineaForm form)
    {
        var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        Guid? usuarioId = Guid.TryParse(claimId, out var id) ? id : null;
        try
        {
            if (!decimal.TryParse(form.Monto, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var monto))
                throw new ArgumentException("Escribe un monto válido");
            var dto = new CrearDonacionDto(monto, form.Medio ?? "", form.Referencia, null, form.DonanteNombre, form.DonanteEmail,
                                           form.CampanaId, form.AceptaTerminos, form.AceptaPrivacidad);
            // La donación en línea pide datos personales: exige las dos aceptaciones
            ConsentimientoService.Exigir(dto.AceptaTerminos, dto.AceptaPrivacidad);
            var archivo = form.ComprobantePago is { } f ? new ArchivoSubido(f.FileName, f.Length, f.OpenReadStream()) : null;
            var d = await _donaciones.RegistrarDonacionEnLineaAsync(dto, usuarioId, archivo);
            var email = dto.DonanteEmail ?? User.FindFirstValue(ClaimTypes.Email);
            await _consentimientos.RegistrarAsync(Formularios.Donacion, usuarioId, email, HttpContext.Origen());
            return Ok(d);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // Donaciones en línea que esperan la verificación del pago en el banco
    [HttpGet("por-verificar")]
    [Authorize(Roles = "DIRECTORA,CONTADOR")]
    public async Task<IActionResult> PorVerificar() => Ok(await _donaciones.ListarPendientesAsync());

    // Foto o PDF del comprobante que subió el donante (privado)
    [HttpGet("{id:guid}/comprobante-pago")]
    [Authorize(Roles = "DIRECTORA,CONTADOR")]
    public async Task<IActionResult> ComprobantePago(Guid id)
    {
        try
        {
            var c = await _donaciones.ObtenerComprobantePagoAsync(id);
            Response.Headers.CacheControl = "no-store";
            return File(c.Contenido, c.Tipo, c.Nombre);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // Confirmar (el dinero llegó: suma y se envía el comprobante) o rechazar (con motivo, se avisa al donante)
    [HttpPost("{id:guid}/verificar")]
    [Authorize(Roles = "DIRECTORA,CONTADOR")]
    public async Task<IActionResult> Verificar(Guid id, VerificarDonacionDto dto)
    {
        try { return Ok(await _donaciones.VerificarAsync(id, dto, UsuarioId())); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
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
