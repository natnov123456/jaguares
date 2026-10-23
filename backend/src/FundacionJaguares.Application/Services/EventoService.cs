using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

public interface IEventoService
{
    Task<List<Evento>> ListarPublicadosAsync();
    Task<Evento> CrearAsync(CrearEventoDto dto, Guid encargadoId);
    Task<AsistenciaEvento> RegistrarAsistenciaAsync(Guid eventoId, Guid participanteId, Guid registradoPor);
    Task<List<AsistenciaEvento>> ListarAsistenciasAsync(Guid eventoId);
}

public class EventoService : IEventoService
{
    private readonly EventoRepository _eventos;
    private readonly AppDbContext _db;

    public EventoService(EventoRepository eventos, AppDbContext db)
    { _eventos = eventos; _db = db; }

    public Task<List<Evento>> ListarPublicadosAsync() => _eventos.GetPublicadosAsync();

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
}