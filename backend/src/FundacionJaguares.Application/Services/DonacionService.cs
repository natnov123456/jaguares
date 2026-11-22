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
    Task<Donacion> RegistrarDonacionEnLineaAsync(CrearDonacionDto dto, Guid? usuarioId, ArchivoSubido? comprobantePago);
    // Verificación del pago de las donaciones en línea (Contabilidad y Dirección)
    Task<List<DonacionPendienteDto>> ListarPendientesAsync();
    Task<ArchivoComprobantePagoDto> ObtenerComprobantePagoAsync(Guid donacionId);
    Task<Donacion> VerificarAsync(Guid donacionId, VerificarDonacionDto dto, Guid usuarioId);
    Task<decimal> TotalPeriodoAsync(int anio, int mes);
    Task<List<Donacion>> ListarPeriodoAsync(int anio, int mes);
    Task<List<Donacion>> ListarRegistradasPorAsync(Guid usuarioId);
    Task<List<MiDonacionDto>> ListarMiasAsync(Guid usuarioId);
    Task<ComprobanteDto> ObtenerComprobanteAsync(Guid donacionId, Guid usuarioId, bool esDirectora);
    Task<List<CampanaDto>> ListarCampanasAsync(bool soloVigentes);
    Task<CampanaDto> CrearCampanaAsync(GuardarCampanaDto dto, Guid creadoPor);
    Task<CampanaDto> ActualizarCampanaAsync(Guid id, GuardarCampanaDto dto);
    // FUJ-125: cada cambio queda en la bitácora de auditoría
    Task<Donacion> EditarAsync(Guid donacionId, EditarDonacionDto dto, Guid usuarioId);
    Task<Donacion> AnularAsync(Guid donacionId, string? motivo, Guid usuarioId);
}

public partial class DonacionService : IDonacionService
{
    private const int MaxNombre = 150, MaxDescripcion = 2000;
    private const decimal MaxMeta = 9_999_999_999.99m; // NUMERIC(12,2)

    private readonly DonacionRepository _donaciones;
    private readonly CampanaRepository _campanas;
    private readonly AppDbContext _db;
    private readonly IEmailAdapter _email;
    private readonly ContactoOpciones _contacto;
    private readonly ILogger<DonacionService> _log;

    public DonacionService(DonacionRepository donaciones, CampanaRepository campanas, AppDbContext db,
                           IEmailAdapter email, ContactoOpciones contacto, ILogger<DonacionService> log)
    { _donaciones = donaciones; _campanas = campanas; _db = db; _email = email; _contacto = contacto; _log = log; }

    // La fundación ya no usa pasarela de pago: en línea solo QR o transferencia, y en oficina además efectivo
    private static void RechazarPasarela(MedioPago medio)
    {
        if (medio == MedioPago.PASARELA) throw new ArgumentException("La fundación no usa pasarela de pago: elige QR, transferencia o efectivo");
    }

    public async Task<Donacion> RegistrarAsync(CrearDonacionDto dto, Guid? registradoPor)
    {
        if (dto.Monto <= 0) throw new ArgumentException("El monto debe ser mayor a 0");
        if (!Enum.TryParse<MedioPago>(dto.Medio, true, out var medio) || !Enum.IsDefined(medio))
            throw new ArgumentException("Medio de pago inválido");
        RechazarPasarela(medio);
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
        var guardada = await GuardarAsync(d, donante, await ActorAsync(registradoPor));
        await EnviarComprobanteAsync(guardada, donante);
        return guardada;
    }

    public const decimal MontoMaximoEnLinea = 1_000_000m;

    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9\-/ .]{3,59}$")]
    private static partial System.Text.RegularExpressions.Regex OperacionValida();

    // Donación en línea por QR o transferencia: el donante ya pagó desde su banco y envía el número de operación
    // y la foto o PDF del comprobante. Queda PENDIENTE (no suma ni tiene comprobante de donación) hasta que
    // Contabilidad o la Dirección confirme en el extracto del banco que el dinero llegó.
    public async Task<Donacion> RegistrarDonacionEnLineaAsync(CrearDonacionDto dto, Guid? usuarioId, ArchivoSubido? comprobantePago)
    {
        if (dto.Monto <= 0) throw new ArgumentException("El monto debe ser mayor a 0");
        if (dto.Monto > MontoMaximoEnLinea) throw new ArgumentException("Para montos tan altos comunícate con la fundación");
        if (decimal.Round(dto.Monto, 2) != dto.Monto) throw new ArgumentException("El monto admite como máximo 2 decimales");
        if (string.IsNullOrWhiteSpace(dto.Medio) || !Enum.TryParse<MedioPago>(dto.Medio, true, out var medio)
            || medio is not (MedioPago.QR or MedioPago.TRANSFERENCIA))
            throw new ArgumentException("Elige cómo pagaste: QR o transferencia bancaria");
        var referencia = (dto.Referencia ?? "").Trim();
        if (!OperacionValida().IsMatch(referencia))
            throw new ArgumentException("Escribe el número de operación o de transacción que figura en el comprobante de tu banco");
        await ValidarCampanaAsync(dto.CampanaId);

        // El mismo número de operación no puede usarse para dos donaciones
        var refMin = referencia.ToLower();
        if (await _db.Donaciones.AnyAsync(d => d.Medio == medio && d.RegistradoPor == null && d.Referencia != null
                                               && d.Referencia.ToLower() == refMin && d.Estado != EstadoDonacion.Rechazada))
            throw new ArgumentException("Ese número de operación ya fue registrado en otra donación");

        // Este endpoint es público: nunca se acepta un DonanteId ni se enlaza por correo, para que nadie
        // pueda enviar comprobantes a nombre de otro donante ni agregar donaciones a una cuenta ajena.
        var usuario = usuarioId is { } uid ? await _db.Usuarios.FindAsync(uid) : null;
        var donante = usuario is not null
            ? new Donante { Nombre = usuario.NombreCompleto, Email = usuario.Email }
            : NuevoDonante(dto, obligatorio: true);
        var archivo = await ArchivosPago.PrepararAsync(comprobantePago);

        if (usuario is not null) _db.Donantes.Add(donante!);
        var actor = usuario is not null
            ? new ActorAuditoria(usuario.Id, usuario.NombreCompleto, usuario.Rol.ToString())
            : new ActorAuditoria(null, "Donación en línea (sin cuenta)", null);
        var d = new Donacion
        {
            Monto = dto.Monto,
            Medio = medio,
            Referencia = referencia,
            CampanaId = dto.CampanaId,
            UsuarioId = usuario?.Id,
            Estado = EstadoDonacion.Pendiente
        };
        _db.ComprobantesPago.Add(new ComprobantePago { DonacionId = d.Id, Contenido = archivo.Contenido, Tipo = archivo.Tipo, Nombre = archivo.Nombre });
        try { await GuardarAsync(d, donante, actor); }
        catch (DbUpdateException) // carrera: otra donación con el mismo número de operación se guardó a la vez
        {
            throw new ArgumentException("Ese número de operación ya fue registrado en otra donación");
        }

        var monto = GeneradorComprobantePdf.Monto(d.Monto, d.Moneda);
        await EnviarSinFallarAsync(donante!.Email!, "Recibimos tu donación: la estamos verificando",
            $"Hola {donante.Nombre},\n\nRecibimos el aviso de tu donación de {monto} ({MedioTexto(medio)}, operación {referencia}).\n" +
            "La fundación revisará en su cuenta bancaria que el pago haya llegado y, al confirmarlo, te enviaremos el comprobante de donación por correo.\n" +
            "Suele tardar de 1 a 2 días hábiles.\n\n¡Gracias por tu apoyo!\nFundación Unión de los Jaguares");
        if (await DestinoFundacionAsync() is { } destino)
            await EnviarSinFallarAsync(destino, $"Donación por verificar: {monto} ({MedioTexto(medio)})",
                $"Llegó una donación en línea que hay que verificar en el banco.\n\nDonante: {donante.Nombre} <{donante.Email}>\n" +
                $"Monto: {monto}\nMedio: {MedioTexto(medio)}\nN° de operación: {referencia}\n\n" +
                "Revísala en Panel → Donaciones → Por verificar: compara el comprobante con el extracto del banco y confírmala o recházala.");
        return d;
    }

    public async Task<List<DonacionPendienteDto>> ListarPendientesAsync() =>
        await (from d in _db.Donaciones
               where d.Estado == EstadoDonacion.Pendiente
               join dn in _db.Donantes on d.DonanteId equals (Guid?)dn.Id into dns
               from dn in dns.DefaultIfEmpty()
               join c in _db.Campanas on d.CampanaId equals (Guid?)c.Id into cs
               from c in cs.DefaultIfEmpty()
               join cp in _db.ComprobantesPago on d.Id equals cp.DonacionId into cps
               from cp in cps.DefaultIfEmpty()
               orderby d.Fecha
               select new DonacionPendienteDto(d.Id, d.NumeroComprobante, d.Fecha, d.Monto, d.Moneda, d.Medio.ToString(), d.Referencia,
                   dn == null ? null : dn.Nombre, dn == null ? null : dn.Email, c == null ? null : c.Nombre, cp == null ? null : cp.Tipo))
            .ToListAsync();

    public async Task<ArchivoComprobantePagoDto> ObtenerComprobantePagoAsync(Guid donacionId)
    {
        var c = await _db.ComprobantesPago.AsNoTracking().FirstOrDefaultAsync(x => x.DonacionId == donacionId)
                ?? throw new KeyNotFoundException("Esta donación no tiene comprobante de pago");
        return new ArchivoComprobantePagoDto(c.Contenido, c.Tipo, c.Nombre);
    }

    // Confirmar: el dinero llegó a la cuenta de la fundación → suma y se envía el comprobante de donación.
    // Rechazar: no llegó o el comprobante no corresponde → se avisa al donante con el motivo.
    public async Task<Donacion> VerificarAsync(Guid donacionId, VerificarDonacionDto dto, Guid usuarioId)
    {
        var d = await _db.Donaciones.FindAsync(donacionId) ?? throw new KeyNotFoundException("Donación no encontrada");
        if (d.Estado != EstadoDonacion.Pendiente) throw new InvalidOperationException("Esta donación ya fue verificada");
        var motivo = dto.Confirmar ? Motivo(dto.Motivo, obligatorio: false)
                                   : MotivoRechazo(dto.Motivo);
        var donante = d.DonanteId is { } did ? await _db.Donantes.FindAsync(did) : null;
        var campana = await NombreCampanaAsync(d.CampanaId);
        var antes = AuditoriaService.Instantanea(d, NombreDonante(donante), campana);

        d.Estado = dto.Confirmar ? EstadoDonacion.Confirmada : EstadoDonacion.Rechazada;
        d.VerificadoPor = usuarioId;
        d.VerificadoEn = DateTime.UtcNow;
        d.MotivoRechazo = dto.Confirmar ? null : motivo;
        AuditoriaService.Registrar(_db, d, dto.Confirmar ? AuditoriaDonacion.Verificacion : AuditoriaDonacion.Rechazo,
            await ActorAsync(usuarioId), antes, AuditoriaService.Instantanea(d, NombreDonante(donante), campana), motivo);
        await _db.SaveChangesAsync();

        if (dto.Confirmar) await EnviarComprobanteAsync(d, donante);
        else if (!string.IsNullOrWhiteSpace(donante?.Email))
            await EnviarSinFallarAsync(donante.Email, "No pudimos verificar tu donación",
                $"Hola {donante.Nombre},\n\nRevisamos tu aviso de donación de {GeneradorComprobantePdf.Monto(d.Monto, d.Moneda)} " +
                $"(operación {d.Referencia}) y no pudimos confirmarlo en nuestra cuenta bancaria.\n\nMotivo: {motivo}\n\n" +
                "Si crees que es un error, responde a este correo o comunícate con la fundación y lo revisamos juntos.\n\nFundación Unión de los Jaguares");
        return d;
    }

    private static string? MotivoRechazo(string? motivo)
    {
        var m = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        if (m is null || m.Length < 5) throw new ArgumentException("Indica el motivo del rechazo (al menos 5 caracteres): se le envía al donante");
        if (m.Length > MaxMotivo) throw new ArgumentException($"El motivo no puede superar {MaxMotivo} caracteres");
        return m;
    }

    private static string MedioTexto(MedioPago m) => m switch
    {
        MedioPago.QR => "pago por QR", MedioPago.TRANSFERENCIA => "transferencia bancaria", MedioPago.EFECTIVO => "efectivo", _ => m.ToString()
    };

    private async Task<string?> DestinoFundacionAsync() =>
        await _db.PaginaInstitucional.Where(p => p.Id == 1).Select(p => p.EmailContacto).FirstOrDefaultAsync()
        ?? (string.IsNullOrWhiteSpace(_contacto.EmailDestino) ? null : _contacto.EmailDestino);

    // Un fallo del correo nunca deshace una donación ya guardada
    private async Task EnviarSinFallarAsync(string para, string asunto, string cuerpo)
    {
        try { await _email.EnviarAsync(para, asunto, cuerpo); }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            _log.LogError(ex, "[DONACION] No se pudo enviar el correo '{asunto}'", asunto);
        }
    }

    public Task<decimal> TotalPeriodoAsync(int anio, int mes) => _donaciones.TotalPeriodoAsync(anio, mes);
    public Task<List<Donacion>> ListarPeriodoAsync(int anio, int mes) => _donaciones.GetByPeriodoAsync(anio, mes);
    public Task<List<Donacion>> ListarRegistradasPorAsync(Guid usuarioId) => _donaciones.GetByRegistradorAsync(usuarioId);

    public async Task<List<MiDonacionDto>> ListarMiasAsync(Guid usuarioId) =>
        (await _db.Donaciones.Where(d => d.UsuarioId == usuarioId).OrderByDescending(d => d.Fecha).ToListAsync())
            .Select(d => new MiDonacionDto(d.Id, d.NumeroComprobante, d.Fecha, d.Monto, d.Moneda,
                                           d.Medio.ToString(), d.Referencia, d.Estado, d.MotivoRechazo))
            .ToList();

    // Pueden descargarlo el donante, quien registró la donación y la Directora
    public async Task<ComprobanteDto> ObtenerComprobanteAsync(Guid donacionId, Guid usuarioId, bool esDirectora)
    {
        var d = await _db.Donaciones.FindAsync(donacionId)
                ?? throw new KeyNotFoundException("Donación no encontrada");
        if (!esDirectora && d.UsuarioId != usuarioId && d.RegistradoPor != usuarioId)
            throw new UnauthorizedAccessException("No puedes descargar este comprobante");
        if (d.Estado == EstadoDonacion.Pendiente)
            throw new InvalidOperationException("La donación todavía se está verificando: el comprobante se emite cuando se confirma el pago");
        if (d.Estado != EstadoDonacion.Confirmada)
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

    private async Task<Donacion> GuardarAsync(Donacion d, Donante? donante, ActorAuditoria actor)
    {
        d.DonanteId = donante?.Id;
        // FUJ-125: la donación y su registro de auditoría se guardan juntos (o ninguno)
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            await _donaciones.AddAsync(d);
            await _donaciones.SaveChangesAsync(); // asigna el número de comprobante
            AuditoriaService.Registrar(_db, d, AuditoriaDonacion.Alta, actor, null,
                AuditoriaService.Instantanea(d, NombreDonante(donante), await NombreCampanaAsync(d.CampanaId)));
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        return d;
    }

    // Comprobante de donación en PDF por correo (solo donaciones confirmadas)
    private async Task EnviarComprobanteAsync(Donacion d, Donante? donante)
    {
        if (string.IsNullOrWhiteSpace(donante?.Email)) return;

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
    }

    // ---------- Edición y anulación (FUJ-125) ----------

    public const int MaxMotivo = 300;

    public async Task<Donacion> EditarAsync(Guid donacionId, EditarDonacionDto dto, Guid usuarioId)
    {
        var d = await _db.Donaciones.FindAsync(donacionId) ?? throw new KeyNotFoundException("Donación no encontrada");
        if (d.Estado != "CONFIRMADA") throw new InvalidOperationException("Solo se pueden corregir donaciones confirmadas");
        if (dto.Monto <= 0) throw new ArgumentException("El monto debe ser mayor a 0");
        if (!Enum.TryParse<MedioPago>(dto.Medio, true, out var medio) || !Enum.IsDefined(medio)) throw new ArgumentException("Medio de pago inválido");
        if (medio != d.Medio) RechazarPasarela(medio);
        if (dto.Referencia?.Length > 100) throw new ArgumentException("La referencia no puede superar 100 caracteres");
        var motivo = Motivo(dto.Motivo, obligatorio: false);
        if (dto.CampanaId != d.CampanaId) await ValidarCampanaAsync(dto.CampanaId);

        var referencia = string.IsNullOrWhiteSpace(dto.Referencia) ? null : dto.Referencia.Trim();
        if (d.Monto == dto.Monto && d.Medio == medio && d.Referencia == referencia && d.CampanaId == dto.CampanaId)
            throw new ArgumentException("No hay cambios para guardar");

        var donante = await NombreDonanteAsync(d.DonanteId);
        var antes = AuditoriaService.Instantanea(d, donante, await NombreCampanaAsync(d.CampanaId));
        d.Monto = dto.Monto;
        d.Medio = medio;
        d.Referencia = referencia;
        d.CampanaId = dto.CampanaId;
        AuditoriaService.Registrar(_db, d, AuditoriaDonacion.Edicion, await ActorAsync(usuarioId), antes,
            AuditoriaService.Instantanea(d, donante, await NombreCampanaAsync(d.CampanaId)), motivo);
        await _db.SaveChangesAsync(); // un solo SaveChanges: el cambio y su auditoría son atómicos
        return d;
    }

    public async Task<Donacion> AnularAsync(Guid donacionId, string? motivo, Guid usuarioId)
    {
        var m = Motivo(motivo, obligatorio: true);
        var d = await _db.Donaciones.FindAsync(donacionId) ?? throw new KeyNotFoundException("Donación no encontrada");
        if (d.Estado == "ANULADA") throw new InvalidOperationException("La donación ya está anulada");
        if (d.Estado != EstadoDonacion.Confirmada)
            throw new InvalidOperationException("Solo se anulan donaciones confirmadas; las que están en verificación se confirman o se rechazan");

        var donante = await NombreDonanteAsync(d.DonanteId);
        var campana = await NombreCampanaAsync(d.CampanaId);
        var antes = AuditoriaService.Instantanea(d, donante, campana);
        d.Estado = "ANULADA"; // deja de sumar en reportes, campañas y transparencia
        AuditoriaService.Registrar(_db, d, AuditoriaDonacion.Anulacion, await ActorAsync(usuarioId), antes,
            AuditoriaService.Instantanea(d, donante, campana), m);
        await _db.SaveChangesAsync();
        return d;
    }

    private static string? Motivo(string? motivo, bool obligatorio)
    {
        var m = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        if (obligatorio && (m is null || m.Length < 5)) throw new ArgumentException("Indica el motivo de la anulación (al menos 5 caracteres)");
        if (m?.Length > MaxMotivo) throw new ArgumentException($"El motivo no puede superar {MaxMotivo} caracteres");
        return m;
    }

    private async Task<ActorAuditoria> ActorAsync(Guid? usuarioId)
    {
        if (usuarioId is null) return new ActorAuditoria(null, "Sistema", null);
        var u = await _db.Usuarios.Where(x => x.Id == usuarioId).Select(x => new { x.NombreCompleto, x.Rol }).FirstOrDefaultAsync();
        return new ActorAuditoria(usuarioId, u?.NombreCompleto ?? "Usuario eliminado", u?.Rol.ToString());
    }

    private static string? NombreDonante(Donante? d) => d is null || d.EsAnonimo ? null : d.Nombre;

    private async Task<string?> NombreDonanteAsync(Guid? donanteId) =>
        donanteId is { } id ? NombreDonante(await _db.Donantes.FindAsync(id)) : null;

    private async Task<string?> NombreCampanaAsync(Guid? campanaId) =>
        campanaId is { } id ? await _db.Campanas.Where(c => c.Id == id).Select(c => c.Nombre).FirstOrDefaultAsync() : null;
}
