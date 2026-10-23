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
    Task<Voluntario> PostularAsync(PostulacionVoluntarioDto dto);
    Task<ParticipacionVoluntario> RegistrarParticipacionAsync(Guid voluntarioId, Guid? eventoId, string descripcion);
}

public class VoluntarioService : IVoluntarioService
{
    private readonly VoluntarioRepository _voluntarios;
    private readonly AppDbContext _db;

    public VoluntarioService(VoluntarioRepository voluntarios, AppDbContext db)
    { _voluntarios = voluntarios; _db = db; }

    public Task<List<Voluntario>> ListarAsync() => _voluntarios.GetAllAsync();

    public async Task<List<VoluntarioConHistorialDto>> ListarConHistorialAsync()
    {
        var voluntarios = await _db.Voluntarios.OrderBy(v => v.NombreCompleto).ToListAsync();
        var historial = await (
            from p in _db.ParticipacionesVoluntario
            join e in _db.Eventos on p.EventoId equals e.Id into ev
            from e in ev.DefaultIfEmpty()
            orderby p.Fecha descending
            select new { p.VoluntarioId, Dto = new ParticipacionDto(p.Id, p.EventoId, e != null ? e.Titulo : null, p.Descripcion, p.Fecha) }
        ).ToListAsync();

        return voluntarios.Select(v => new VoluntarioConHistorialDto(
            v.Id, v.NombreCompleto, v.Email, v.Telefono, v.Estado, v.PostuladoEn,
            historial.Where(h => h.VoluntarioId == v.Id).Select(h => h.Dto).ToList()
        )).ToList();
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

    public async Task<ParticipacionVoluntario> RegistrarParticipacionAsync(Guid voluntarioId, Guid? eventoId, string descripcion)
    {
        if (await _voluntarios.GetByIdAsync(voluntarioId) == null)
            throw new KeyNotFoundException("Voluntario no encontrado");

        var p = new ParticipacionVoluntario
        {
            VoluntarioId = voluntarioId,
            EventoId = eventoId,
            Descripcion = descripcion
        };
        _db.ParticipacionesVoluntario.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }
}