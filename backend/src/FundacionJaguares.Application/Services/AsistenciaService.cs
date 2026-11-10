using System.Security.Cryptography;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace FundacionJaguares.Application.Services;

// FUJ-113: inscripción a eventos con código QR y check-in escaneando ese código
public interface IAsistenciaService
{
    Task<MiInscripcionEventoDto> InscribirAsync(Guid eventoId, Guid participanteId);
    Task<List<MiInscripcionEventoDto>> MisInscripcionesAsync(Guid participanteId);
    Task CancelarAsync(Guid eventoId, Guid participanteId);
    Task<string> GenerarQrSvgAsync(Guid inscripcionId, Guid participanteId);
    Task<CheckinResultadoDto> CheckinAsync(Guid eventoId, string codigo, Guid registradoPor);
    Task<List<InscritoEventoDto>> ListarInscritosAsync(Guid eventoId);
}

public class AsistenciaService : IAsistenciaService
{
    // Prefijo del contenido del QR: permite al lector distinguir nuestros códigos de cualquier otro QR
    public const string PrefijoQr = "JAGUARES-EV:";
    // El check-in se acepta desde 24 h antes del inicio hasta 24 h después del fin del evento
    public static readonly TimeSpan VentanaCheckin = TimeSpan.FromHours(24);

    private readonly AppDbContext _db;
    private readonly IEmailAdapter _email;

    public AsistenciaService(AppDbContext db, IEmailAdapter email) { _db = db; _email = email; }

    // 128 bits aleatorios en base64url (22 caracteres): imposible de adivinar o derivar del id
    public static string NuevoCodigo()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // Acepta el contenido completo del QR ("JAGUARES-EV:xxxx") o solo el código escrito a mano
    public static string NormalizarCodigo(string? codigo)
    {
        var c = (codigo ?? "").Trim();
        return c.StartsWith(PrefijoQr, StringComparison.OrdinalIgnoreCase) ? c[PrefijoQr.Length..].Trim() : c;
    }

    public async Task<MiInscripcionEventoDto> InscribirAsync(Guid eventoId, Guid participanteId)
    {
        var evento = await _db.Eventos.FindAsync(eventoId)
                     ?? throw new KeyNotFoundException("Evento no encontrado");
        if (evento.Estado is not ("PUBLICADO" or "EN_CURSO"))
            throw new InvalidOperationException("El evento no está abierto a inscripciones");
        if ((evento.FechaFin ?? evento.FechaInicio) < DateTime.UtcNow)
            throw new InvalidOperationException("El evento ya finalizó");

        var existente = await _db.InscripcionesEvento
            .FirstOrDefaultAsync(i => i.EventoId == eventoId && i.ParticipanteId == participanteId);
        if (existente is { Estado: not "CANCELADA" })
            throw new InvalidOperationException("Ya estás inscrito/a en este evento");

        var insc = existente ?? new InscripcionEvento { EventoId = eventoId, ParticipanteId = participanteId };
        insc.Estado = "CONFIRMADA";
        insc.CodigoQr = NuevoCodigo(); // si se reinscribe tras cancelar, el código anterior deja de servir
        insc.InscritoEn = DateTime.UtcNow;
        insc.CheckinEn = null;
        if (existente is null) _db.InscripcionesEvento.Add(insc);
        await _db.SaveChangesAsync();

        var usuario = await _db.Usuarios.FindAsync(participanteId);
        if (usuario is not null)
            await _email.EnviarAsync(usuario.Email, $"Inscripción confirmada: {evento.Titulo}",
                $"Hola {usuario.NombreCompleto}, tu inscripción a \"{evento.Titulo}\" está confirmada. " +
                $"Presenta tu código QR al ingresar (lo encuentras en \"Mi panel\" del sitio). Código: {insc.CodigoQr}");

        return ADto(insc, evento);
    }

    // FUJ-112: cancelar desde "Mi panel". El QR deja de servir y el lugar queda libre;
    // si cambia de opinión puede volver a inscribirse y recibe un código nuevo.
    public async Task CancelarAsync(Guid eventoId, Guid participanteId)
    {
        var insc = await _db.InscripcionesEvento
            .FirstOrDefaultAsync(i => i.EventoId == eventoId && i.ParticipanteId == participanteId && i.Estado != "CANCELADA")
            ?? throw new KeyNotFoundException("Inscripción no encontrada");
        if (insc.Estado == "ASISTIO")
            throw new InvalidOperationException("Tu asistencia ya fue registrada: la inscripción no se puede cancelar");

        var evento = await _db.Eventos.FindAsync(eventoId)
                     ?? throw new KeyNotFoundException("Evento no encontrado");
        if (evento.FechaInicio <= DateTime.UtcNow)
            throw new InvalidOperationException("El evento ya comenzó: la inscripción no se puede cancelar");

        insc.Estado = "CANCELADA";
        await _db.SaveChangesAsync();
    }

    public async Task<List<MiInscripcionEventoDto>> MisInscripcionesAsync(Guid participanteId)
    {
        var filas = await (from i in _db.InscripcionesEvento
                           join e in _db.Eventos on i.EventoId equals e.Id
                           where i.ParticipanteId == participanteId && i.Estado != "CANCELADA"
                           orderby e.FechaInicio
                           select new { i, e }).ToListAsync();
        return filas.Select(x => ADto(x.i, x.e)).ToList();
    }

    public async Task<string> GenerarQrSvgAsync(Guid inscripcionId, Guid participanteId)
    {
        // Solo el dueño de la inscripción puede ver su QR
        var insc = await _db.InscripcionesEvento
            .FirstOrDefaultAsync(i => i.Id == inscripcionId && i.ParticipanteId == participanteId && i.Estado != "CANCELADA")
            ?? throw new KeyNotFoundException("Inscripción no encontrada");

        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(PrefijoQr + insc.CodigoQr, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(datos).GetGraphic(8);
    }

    public async Task<CheckinResultadoDto> CheckinAsync(Guid eventoId, string codigo, Guid registradoPor)
    {
        var cod = NormalizarCodigo(codigo);
        if (cod.Length == 0) return new("INVALIDO", "Código QR no válido", null, null);

        var fila = await (from i in _db.InscripcionesEvento
                          join u in _db.Usuarios on i.ParticipanteId equals u.Id
                          join e in _db.Eventos on i.EventoId equals e.Id
                          where i.CodigoQr == cod
                          select new { i, u.NombreCompleto, e }).FirstOrDefaultAsync();

        if (fila is null || fila.i.Estado == "CANCELADA")
            return new("INVALIDO", "Código QR no válido o inscripción cancelada", null, null);
        if (fila.i.EventoId != eventoId)
            return new("OTRO_EVENTO", $"Este QR corresponde a otro evento: {fila.e.Titulo}", fila.NombreCompleto, null);
        if (fila.i.Estado == "ASISTIO")
            return new("YA_REGISTRADO", "La asistencia ya fue registrada", fila.NombreCompleto, fila.i.CheckinEn);
        // FUJ-120: una vez cerrada la asistencia (y enviada la encuesta) no se aceptan más ingresos
        if (fila.e.Estado == "FINALIZADO")
            return new("FUERA_DE_FECHA", "La asistencia de este evento ya fue cerrada", fila.NombreCompleto, null);

        var ahora = DateTime.UtcNow;
        if (ahora < fila.e.FechaInicio - VentanaCheckin || ahora > (fila.e.FechaFin ?? fila.e.FechaInicio) + VentanaCheckin)
            return new("FUERA_DE_FECHA", "El check-in solo está habilitado el día del evento", fila.NombreCompleto, null);

        await using var tx = await _db.Database.BeginTransactionAsync();

        // Actualización condicional: si dos lectores escanean el mismo QR a la vez, solo uno gana
        var filas = await _db.InscripcionesEvento
            .Where(i => i.Id == fila.i.Id && i.Estado == "CONFIRMADA")
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Estado, "ASISTIO").SetProperty(i => i.CheckinEn, ahora));
        if (filas == 0)
            return new("YA_REGISTRADO", "La asistencia ya fue registrada", fila.NombreCompleto, null);

        // Si ya se había registrado manualmente (FUJ-102) no se duplica la asistencia
        if (!await _db.AsistenciasEvento.AnyAsync(a => a.EventoId == eventoId && a.ParticipanteId == fila.i.ParticipanteId))
        {
            _db.AsistenciasEvento.Add(new AsistenciaEvento
            {
                EventoId = eventoId,
                ParticipanteId = fila.i.ParticipanteId,
                RegistradoPor = registradoPor,
                RegistradoEn = ahora
            });
            await _db.SaveChangesAsync();
        }
        await tx.CommitAsync();

        return new("REGISTRADO", "Asistencia registrada", fila.NombreCompleto, ahora);
    }

    public Task<List<InscritoEventoDto>> ListarInscritosAsync(Guid eventoId) =>
        (from i in _db.InscripcionesEvento
         join u in _db.Usuarios on i.ParticipanteId equals u.Id
         where i.EventoId == eventoId && i.Estado != "CANCELADA"
         orderby u.NombreCompleto
         select new InscritoEventoDto(i.Id, u.NombreCompleto, u.Email, i.Estado, i.InscritoEn, i.CheckinEn))
        .ToListAsync();

    private static MiInscripcionEventoDto ADto(InscripcionEvento i, Evento e) =>
        new(i.Id, e.Id, e.Titulo, e.Lugar, e.FechaInicio, e.FechaFin, i.Estado, i.CodigoQr, i.InscritoEn, i.CheckinEn);
}
