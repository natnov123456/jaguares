using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

public interface IVoluntarioService
{
    Task<List<Voluntario>> ListarAsync();
    Task<List<VoluntarioConHistorialDto>> ListarConHistorialAsync();
    Task<VoluntarioConHistorialDto?> ObtenerPorUsuarioAsync(Guid usuarioId, string? email);
    Task<Voluntario> PostularAsync(PostulacionVoluntarioDto dto);
    Task<ParticipacionVoluntario> RegistrarParticipacionAsync(Guid voluntarioId, Guid? eventoId, string descripcion,
                                                              string? horaInicio = null, string? horaFin = null);
}

public class VoluntarioService : IVoluntarioService
{
    private readonly VoluntarioRepository _voluntarios;
    private readonly AppDbContext _db;

    public VoluntarioService(VoluntarioRepository voluntarios, AppDbContext db)
    { _voluntarios = voluntarios; _db = db; }

    public Task<List<Voluntario>> ListarAsync() => _voluntarios.GetAllAsync();

    public Task<List<VoluntarioConHistorialDto>> ListarConHistorialAsync() =>
        ConHistorialAsync(_db.Voluntarios.OrderBy(v => v.NombreCompleto));

    // El voluntario se vincula por usuario_id; si se postuló antes de tener cuenta, por email
    public async Task<VoluntarioConHistorialDto?> ObtenerPorUsuarioAsync(Guid usuarioId, string? email) =>
        (await ConHistorialAsync(_db.Voluntarios.Where(v =>
            v.UsuarioId == usuarioId || (email != null && v.Email == email)).Take(1)))
        .FirstOrDefault();

    private async Task<List<VoluntarioConHistorialDto>> ConHistorialAsync(IQueryable<Voluntario> query)
    {
        var voluntarios = await query.ToListAsync();
        var ids = voluntarios.Select(v => v.Id).ToList();
        var historial = await (
            from p in _db.ParticipacionesVoluntario
            where ids.Contains(p.VoluntarioId)
            join e in _db.Eventos on p.EventoId equals e.Id into ev
            from e in ev.DefaultIfEmpty()
            join c in _db.Cursos on p.CursoId equals c.Id into cu
            from c in cu.DefaultIfEmpty()
            orderby p.Fecha descending
            select new
            {
                p.VoluntarioId, p.Id, p.EventoId, EventoTitulo = e != null ? e.Titulo : null, p.Descripcion, p.Fecha,
                p.Horas, p.CursoId, CursoTitulo = c != null ? c.Titulo : null, p.HoraInicio, p.HoraFin, p.Dias
            }
        ).ToListAsync();
        var conDto = historial.Select(h => new
        {
            h.VoluntarioId,
            Dto = new ParticipacionDto(h.Id, h.EventoId, h.EventoTitulo, h.Descripcion, h.Fecha, h.Horas, h.CursoId, h.CursoTitulo,
                                       h.HoraInicio?.ToString("HH:mm"), h.HoraFin?.ToString("HH:mm"), h.Dias)
        }).ToList();

        return voluntarios.Select(v =>
        {
            var propio = conDto.Where(h => h.VoluntarioId == v.Id).Select(h => h.Dto).ToList();
            // FUJ-118: las horas registradas se acumulan en el historial del voluntario
            return new VoluntarioConHistorialDto(v.Id, v.NombreCompleto, v.Email, v.Telefono, v.Estado, v.PostuladoEn,
                propio, propio.Sum(h => h.Horas ?? 0));
        }).ToList();
    }

    public async Task<Voluntario> PostularAsync(PostulacionVoluntarioDto dto)
    {
        if (await _voluntarios.GetByEmailAsync(dto.Email) != null)
            throw new InvalidOperationException("Este email ya está postulado");

        var v = new Voluntario
        {
            NombreCompleto = dto.NombreCompleto,
            Email = dto.Email,
            Telefono = dto.Telefono,
            Estado = "ACTIVO"
        };
        await _voluntarios.AddAsync(v);
        await _voluntarios.SaveChangesAsync();
        return v;
    }

    // El horario es opcional: sin horario se registra la actividad sin horas
    public async Task<ParticipacionVoluntario> RegistrarParticipacionAsync(Guid voluntarioId, Guid? eventoId, string descripcion,
                                                                           string? horaInicio = null, string? horaFin = null)
    {
        if (await _voluntarios.GetByIdAsync(voluntarioId) == null)
            throw new KeyNotFoundException("Voluntario no encontrado");

        var p = new ParticipacionVoluntario { VoluntarioId = voluntarioId, EventoId = eventoId, Descripcion = descripcion };
        if (!string.IsNullOrWhiteSpace(horaInicio) || !string.IsNullOrWhiteSpace(horaFin))
        {
            var (inicio, fin, dias, horas) = VoluntariadoService.CalcularHoras(horaInicio, horaFin);
            (p.HoraInicio, p.HoraFin, p.Dias, p.Horas) = (inicio, fin, dias, horas);
        }
        _db.ParticipacionesVoluntario.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }
}
