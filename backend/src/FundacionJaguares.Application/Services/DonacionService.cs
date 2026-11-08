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
    Task<List<CampanaDto>> ListarCampanasAsync(bool soloVigentes);
    Task<CampanaDto> CrearCampanaAsync(GuardarCampanaDto dto, Guid creadoPor);
    Task<CampanaDto> ActualizarCampanaAsync(Guid id, GuardarCampanaDto dto);
}

public class DonacionService : IDonacionService
{
    private const int MaxNombre = 150, MaxDescripcion = 2000;
    private const decimal MaxMeta = 9_999_999_999.99m; // NUMERIC(12,2)

    private readonly DonacionRepository _donaciones;
    private readonly CampanaRepository _campanas;
    private readonly IPaymentAdapter _payment;
    private readonly AppDbContext _db;
    private readonly IEmailAdapter _email;
    private readonly ILogger<DonacionService> _log;

    public DonacionService(DonacionRepository donaciones, CampanaRepository campanas, IPaymentAdapter payment, AppDbContext db,
                           IEmailAdapter email, ILogger<DonacionService> log)
    { _donaciones = donaciones; _campanas = campanas; _payment = payment; _db = db; _email = email; _log = log; }

    public async Task<Donacion> RegistrarAsync(CrearDonacionDto dto, Guid? registradoPor)
    {
        if (dto.Monto <= 0) throw new ArgumentException("El monto debe ser mayor a 0");
        if (!Enum.TryParse<MedioPago>(dto.Medio, true, out var medio))
            throw new ArgumentException("Medio de pago inválido");
        await ValidarCampanaAsync(dto.CampanaId);

        // En oficina los datos del donante son opcionales: alguien puede entregar efectivo sin identificarse
        var donante = dto.DonanteId is { } id
            ? await _db.Donantes.FindAsync(id) ?? throw new ArgumentException("El donante indicado no existe")
            : NuevoDonante(dto, obligatorio: false);

        var d = new Donacion
        {
            Monto = dto.Monto,
            Medio = medio,
            Referencia = dto.Referencia,
            CampanaId = dto.CampanaId,
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
        await ValidarCampanaAsync(dto.CampanaId); // antes de cobrar

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
            CampanaId = dto.CampanaId,
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

    // ---------- Campañas (FUJ-117) ----------

    public async Task<List<CampanaDto>> ListarCampanasAsync(bool soloVigentes)
    {
        var hoy = HoyBolivia();
        var campanas = await _campanas.ListarAsync(soloVigentes, hoy);
        var totales = await _campanas.TotalesAsync(campanas.Select(c => c.Id).ToList());
        return campanas.Select(c => ADto(c, totales.GetValueOrDefault(c.Id), hoy)).ToList();
    }

    public async Task<CampanaDto> CrearCampanaAsync(GuardarCampanaDto dto, Guid creadoPor)
    {
        var hoy = HoyBolivia();
        if (dto.FechaLimite < hoy) throw new ArgumentException("La fecha límite no puede estar en el pasado");

        var c = new Campana { CreadoPor = creadoPor };
        Aplicar(c, dto);
        await _campanas.AddAsync(c);
        await _campanas.SaveChangesAsync();
        return ADto(c, default, hoy);
    }

    // Una fecha ya vencida se permite al editar para poder corregir otros datos de una campaña terminada
    public async Task<CampanaDto> ActualizarCampanaAsync(Guid id, GuardarCampanaDto dto)
    {
        var c = await _campanas.GetByIdAsync(id) ?? throw new KeyNotFoundException("Campaña no encontrada");
        Aplicar(c, dto);
        await _campanas.SaveChangesAsync();
        var totales = await _campanas.TotalesAsync([id]);
        return ADto(c, totales.GetValueOrDefault(id), HoyBolivia());
    }

    private static void Aplicar(Campana c, GuardarCampanaDto dto)
    {
        var nombre = (dto.Nombre ?? "").Trim();
        if (nombre.Length == 0) throw new ArgumentException("La campaña necesita un nombre");
        if (nombre.Length > MaxNombre) throw new ArgumentException($"El nombre no puede superar {MaxNombre} caracteres");
        var descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        if (descripcion?.Length > MaxDescripcion) throw new ArgumentException($"La descripción no puede superar {MaxDescripcion} caracteres");
        if (dto.Meta <= 0) throw new ArgumentException("La meta debe ser mayor a 0");
        if (dto.Meta > MaxMeta) throw new ArgumentException("La meta es demasiado alta");

        c.Nombre = nombre;
        c.Descripcion = descripcion;
        c.Meta = dto.Meta;
        c.FechaLimite = dto.FechaLimite;
        c.Activa = dto.Activa;
    }

    private async Task ValidarCampanaAsync(Guid? campanaId)
    {
        if (campanaId is null) return;
        var c = await _campanas.GetByIdAsync(campanaId.Value) ?? throw new ArgumentException("La campaña indicada no existe");
        if (!EsVigente(c, HoyBolivia())) throw new ArgumentException("La campaña ya no está activa");
    }

    private static DateOnly HoyBolivia() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4));

    private static bool EsVigente(Campana c, DateOnly hoy) => c.Activa && c.FechaLimite >= hoy;

    private static CampanaDto ADto(Campana c, (decimal Total, int Cantidad) totales, DateOnly hoy) =>
        new(c.Id, c.Nombre, c.Descripcion, c.Meta, c.FechaLimite, c.Activa, EsVigente(c, hoy),
            totales.Total, Math.Round(totales.Total / c.Meta * 100, 1, MidpointRounding.AwayFromZero), totales.Cantidad, c.CreadoEn);

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
