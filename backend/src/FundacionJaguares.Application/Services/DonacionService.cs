using System.Net.Mail;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using FundacionJaguares.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FundacionJaguares.Application.Services;

public interface IDonacionService
{
    Task<Donacion> RegistrarAsync(CrearDonacionDto dto, Guid? registradoPor);
    Task<Donacion> RegistrarDonacionEnLineaAsync(CrearDonacionDto dto, Guid? usuarioId);
    Task<decimal> TotalPeriodoAsync(int anio, int mes);
    Task<List<Donacion>> ListarPeriodoAsync(int anio, int mes);
    Task<List<Donacion>> ListarRegistradasPorAsync(Guid usuarioId);
    Task<List<MiDonacionDto>> ListarMiasAsync(Guid usuarioId);
    Task<ComprobanteDto> ObtenerComprobanteAsync(Guid donacionId, Guid usuarioId, bool esDirectora);
}

public class DonacionService : IDonacionService
{
    private const int MaxNombre = 150;

    private readonly DonacionRepository _donaciones;
    private readonly IPaymentAdapter _payment;
    private readonly AppDbContext _db;
    private readonly IEmailAdapter _email;
    private readonly ILogger<DonacionService> _log;

    public DonacionService(DonacionRepository donaciones, IPaymentAdapter payment, AppDbContext db,
                           IEmailAdapter email, ILogger<DonacionService> log)
    { _donaciones = donaciones; _payment = payment; _db = db; _email = email; _log = log; }

    public async Task<Donacion> RegistrarAsync(CrearDonacionDto dto, Guid? registradoPor)
    {
        if (dto.Monto <= 0) throw new ArgumentException("El monto debe ser mayor a 0");
        if (!Enum.TryParse<MedioPago>(dto.Medio, true, out var medio))
            throw new ArgumentException("Medio de pago inválido");

        // En oficina los datos del donante son opcionales: alguien puede entregar efectivo sin identificarse
        var donante = dto.DonanteId is { } id
            ? await _db.Donantes.FindAsync(id) ?? throw new ArgumentException("El donante indicado no existe")
            : NuevoDonante(dto, obligatorio: false);

        var d = new Donacion
        {
            Monto = dto.Monto,
            Medio = medio,
            Referencia = dto.Referencia,
            RegistradoPor = registradoPor,
            Estado = "CONFIRMADA"
        };

        // Lo carga el personal, así que es seguro enlazarla a la cuenta que tenga ese correo
        if (donante?.Email is { } email)
        {
            var minusculas = email.ToLowerInvariant();
            d.UsuarioId = await _db.Usuarios.Where(u => u.Email.ToLower() == minusculas)
                                            .Select(u => (Guid?)u.Id).FirstOrDefaultAsync();
        }
        return await GuardarYEnviarAsync(d, donante);
    }

    public async Task<Donacion> RegistrarDonacionEnLineaAsync(CrearDonacionDto dto, Guid? usuarioId)
    {
        if (dto.Monto <= 0) throw new ArgumentException("El monto debe ser mayor a 0");

        // En línea solo se acepta QR, pasarela o transferencia (el efectivo se registra en oficina)
        var medio = MedioPago.PASARELA;
        if (!string.IsNullOrWhiteSpace(dto.Medio)
            && (!Enum.TryParse(dto.Medio, true, out medio) || medio == MedioPago.EFECTIVO))
            throw new ArgumentException("Medio de pago inválido para donación en línea");

        // Este endpoint es público: nunca se acepta un DonanteId ni se enlaza por correo, para que nadie
        // pueda enviar comprobantes a nombre de otro donante ni agregar donaciones a una cuenta ajena.
        // Se valida antes de cobrar para no rechazar datos de un pago ya hecho.
        var usuario = usuarioId is { } uid ? await _db.Usuarios.FindAsync(uid) : null;
        var donante = usuario is not null
            ? new Donante { Nombre = usuario.NombreCompleto, Email = usuario.Email }
            : NuevoDonante(dto, obligatorio: true);

        var ref_ = dto.Referencia ?? $"DON-{Guid.NewGuid().ToString("N")[..10].ToUpper()}";
        var ok = await _payment.ProcesarPagoAsync(dto.Monto, ref_);
        if (!ok) throw new InvalidOperationException("Pago rechazado");

        if (usuario is not null) _db.Donantes.Add(donante!);
        return await GuardarYEnviarAsync(new Donacion
        {
            Monto = dto.Monto,
            Medio = medio,
            Referencia = ref_,
            UsuarioId = usuario?.Id,
            Estado = "CONFIRMADA"
        }, donante);
    }

    public Task<decimal> TotalPeriodoAsync(int anio, int mes) => _donaciones.TotalPeriodoAsync(anio, mes);
    public Task<List<Donacion>> ListarPeriodoAsync(int anio, int mes) => _donaciones.GetByPeriodoAsync(anio, mes);
    public Task<List<Donacion>> ListarRegistradasPorAsync(Guid usuarioId) => _donaciones.GetByRegistradorAsync(usuarioId);

    public async Task<List<MiDonacionDto>> ListarMiasAsync(Guid usuarioId) =>
        (await _db.Donaciones.Where(d => d.UsuarioId == usuarioId).OrderByDescending(d => d.Fecha).ToListAsync())
            .Select(d => new MiDonacionDto(d.Id, d.NumeroComprobante, d.Fecha, d.Monto, d.Moneda,
                                           d.Medio.ToString(), d.Referencia, d.Estado))
            .ToList();

    // Pueden descargarlo el donante, quien registró la donación y la Directora
    public async Task<ComprobanteDto> ObtenerComprobanteAsync(Guid donacionId, Guid usuarioId, bool esDirectora)
    {
        var d = await _db.Donaciones.FindAsync(donacionId)
                ?? throw new KeyNotFoundException("Donación no encontrada");
        if (!esDirectora && d.UsuarioId != usuarioId && d.RegistradoPor != usuarioId)
            throw new UnauthorizedAccessException("No puedes descargar este comprobante");
        if (d.Estado != "CONFIRMADA")
            throw new InvalidOperationException("La donación no está confirmada");

        var donante = d.DonanteId is { } id ? await _db.Donantes.FindAsync(id) : null;
        return new ComprobanteDto($"comprobante-{GeneradorComprobantePdf.Numero(d.NumeroComprobante)}.pdf", GenerarPdf(d, donante));
    }

    // ---------- auxiliares ----------

    // Null si la donación no identifica a nadie
    private Donante? NuevoDonante(CrearDonacionDto dto, bool obligatorio)
    {
        var nombre = string.IsNullOrWhiteSpace(dto.DonanteNombre) ? null : dto.DonanteNombre.Trim();
        var email = string.IsNullOrWhiteSpace(dto.DonanteEmail) ? null : dto.DonanteEmail.Trim();

        if (obligatorio && nombre is null) throw new ArgumentException("Ingresa tu nombre para emitir el comprobante");
        if (obligatorio && email is null) throw new ArgumentException("Ingresa tu correo para enviarte el comprobante");
        if (nombre?.Length > MaxNombre) throw new ArgumentException($"El nombre no puede superar {MaxNombre} caracteres");
        if (email is not null && !ContactoService.EmailValido(email)) throw new ArgumentException("Correo inválido");
        if (email is not null && nombre is null) throw new ArgumentException("Indica el nombre del donante");
        if (nombre is null) return null;

        var donante = new Donante { Nombre = nombre, Email = email };
        _db.Donantes.Add(donante);
        return donante;
    }

    private static byte[] GenerarPdf(Donacion d, Donante? donante) =>
        GeneradorComprobantePdf.Generar(new DatosComprobante(d.NumeroComprobante, d.Fecha, d.Monto, d.Moneda, d.Medio, d.Referencia,
            donante is null || donante.EsAnonimo ? "Donante anónimo" : donante.Nombre));

    private async Task<Donacion> GuardarYEnviarAsync(Donacion d, Donante? donante)
    {
        d.DonanteId = donante?.Id;
        await _donaciones.AddAsync(d);
        await _donaciones.SaveChangesAsync();
        if (string.IsNullOrWhiteSpace(donante?.Email)) return d;

        var numero = GeneradorComprobantePdf.Numero(d.NumeroComprobante);
        try
        {
            await _email.EnviarAsync(donante.Email, $"Comprobante de donación N° {numero}",
                $"Hola,\n\nGracias por tu donación de {GeneradorComprobantePdf.Monto(d.Monto, d.Moneda)} a la Fundación Unión de los Jaguares.\n" +
                $"Adjuntamos tu comprobante N° {numero}.\n\nFundación Unión de los Jaguares",
                adjunto: new AdjuntoCorreo($"comprobante-{numero}.pdf", GenerarPdf(d, donante), "application/pdf"));
            d.ComprobanteEnviadoEn = DateTime.UtcNow;
            await _donaciones.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            // La donación ya está pagada y guardada: un fallo del correo no debe rechazarla
            _log.LogError(ex, "[DONACION] No se pudo enviar el comprobante {numero}", numero);
        }
        return d;
    }
}
