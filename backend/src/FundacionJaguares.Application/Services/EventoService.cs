using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

public interface IEventoService
{
    Task<List<Evento>> ListarPublicadosAsync();
    Task<List<Evento>> ListarGestionAsync();
    Task<Evento> CrearAsync(CrearEventoDto dto, Guid encargadoId);
    Task<AsistenciaEvento> RegistrarAsistenciaAsync(Guid eventoId, Guid participanteId, Guid registradoPor);
    Task<List<AsistenciaEvento>> ListarAsistenciasAsync(Guid eventoId);
    Task<CierreActividadDto> CerrarAsistenciaAsync(Guid eventoId, List<PreguntaEncuestaDto>? preguntas, Guid usuarioId);
}

public class EventoService : IEventoService
{
    private readonly EventoRepository _eventos;
    private readonly AppDbContext _db;
    private readonly IEncuestaService _encuestas;

    public EventoService(EventoRepository eventos, AppDbContext db, IEncuestaService encuestas)
    { _eventos = eventos; _db = db; _encuestas = encuestas; }

    public Task<List<Evento>> ListarPublicadosAsync() => _eventos.GetPublicadosAsync();

    public Task<List<Evento>> ListarGestionAsync() =>
        _db.Eventos.Where(e => e.Estado != "BORRADOR").OrderByDescending(e => e.FechaInicio).ToListAsync();

    public async Task<Evento> CrearAsync(CrearEventoDto dto, Guid encargadoId)
    {
        var e = new Evento
        {
            Titulo = dto.Titulo,
            Descripcion = dto.Descripcion,
            Lugar = dto.Lugar,
            FechaInicio = dto.FechaInicio,
            FechaFin = dto.FechaFin,
            EncargadoId = encargadoId,
            Estado = "PUBLICADO"
        };
        await _eventos.AddAsync(e);
        await _eventos.SaveChangesAsync();
        return e;
    }

    public async Task<AsistenciaEvento> RegistrarAsistenciaAsync(Guid eventoId, Guid participanteId, Guid registradoPor)
    {
        var evento = await _eventos.GetByIdAsync(eventoId) ?? throw new KeyNotFoundException("Evento no encontrado");
        if (evento.Estado == "FINALIZADO")
            throw new InvalidOperationException("La asistencia de este evento ya fue cerrada");
        if (await _db.AsistenciasEvento.AnyAsync(a => a.EventoId == eventoId && a.ParticipanteId == participanteId))
            throw new InvalidOperationException("Asistencia ya registrada");

        var a = new AsistenciaEvento
        {
            EventoId = eventoId,
            ParticipanteId = participanteId,
            RegistradoPor = registradoPor
        };
        _db.AsistenciasEvento.Add(a);
        await _db.SaveChangesAsync();
        return a;
    }

    public Task<List<AsistenciaEvento>> ListarAsistenciasAsync(Guid eventoId) =>
        _db.AsistenciasEvento.Where(a => a.EventoId == eventoId).ToListAsync();

    // FUJ-120: cerrar la asistencia finaliza el evento y envía la encuesta de satisfacción a quienes asistieron
    public async Task<CierreActividadDto> CerrarAsistenciaAsync(Guid eventoId, List<PreguntaEncuestaDto>? preguntas, Guid usuarioId)
    {
        var lista = EncuestaService.ValidarPreguntas(preguntas);
        var evento = await _eventos.GetByIdAsync(eventoId) ?? throw new KeyNotFoundException("Evento no encontrado");
        if (evento.Estado == "CANCELADO") throw new InvalidOperationException("El evento fue cancelado");
        if (evento.Estado == "FINALIZADO") throw new InvalidOperationException("La asistencia de este evento ya fue cerrada");
        if (evento.FechaInicio > DateTime.UtcNow)
            throw new InvalidOperationException("La asistencia se cierra cuando el evento ya comenzó");

        EncuestaPreparada encuesta;
        List<Guid> asistentes;
        // Cierre y encuesta en una sola transacción: el evento nunca queda cerrado sin su encuesta
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            // Condicional: si dos personas cierran a la vez, solo una envía la encuesta
            var cerrados = await _db.Eventos.Where(e => e.Id == eventoId && e.Estado != "FINALIZADO")
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Estado, "FINALIZADO"));
            if (cerrados == 0) throw new InvalidOperationException("La asistencia de este evento ya fue cerrada");

            // Asistentes: check-in con QR (FUJ-113) o registro manual (FUJ-102)
            asistentes = (await _db.AsistenciasEvento.Where(a => a.EventoId == eventoId).Select(a => a.ParticipanteId).ToListAsync())
                .Union(await _db.InscripcionesEvento.Where(i => i.EventoId == eventoId && i.Estado == "ASISTIO")
                    .Select(i => i.ParticipanteId).ToListAsync())
                .ToList();

            encuesta = await _encuestas.PrepararAsync("EVENTO", eventoId, evento.Titulo, asistentes, lista, usuarioId);
            await tx.CommitAsync();
        }

        await _encuestas.NotificarAsync(encuesta);
        return new CierreActividadDto(asistentes.Count, encuesta.Invitados.Count, encuesta.EncuestaId);
    }
}