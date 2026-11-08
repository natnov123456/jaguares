using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

// FUJ-114: calendario público. Une cursos y eventos publicados en una sola lista por rango de fechas.
public interface IActividadService
{
    Task<List<ActividadDto>> ListarPorRangoAsync(DateOnly desde, DateOnly hasta);
}

public class ActividadService : IActividadService
{
    public const int MaxDiasRango = 93; // un trimestre: cubre la vista mensual con margen

    private static readonly string[] EstadosVisibles = ["PUBLICADO", "EN_CURSO"];
    private readonly AppDbContext _db;

    public ActividadService(AppDbContext db) => _db = db;

    public async Task<List<ActividadDto>> ListarPorRangoAsync(DateOnly desde, DateOnly hasta)
    {
        if (hasta < desde) throw new ArgumentException("La fecha 'hasta' no puede ser anterior a 'desde'");
        if (hasta.DayNumber - desde.DayNumber > MaxDiasRango)
            throw new ArgumentException($"El rango no puede superar {MaxDiasRango} días");

        // Los cursos usan fechas sin hora: se ubican en el calendario por su fecha de inicio
        var cursos = await _db.Cursos
            .Where(c => EstadosVisibles.Contains(c.Estado) && c.FechaInicio >= desde && c.FechaInicio <= hasta)
            .ToListAsync();
        var actividadesCursos = cursos.Select(c => new ActividadDto(c.Id, "CURSO", c.Titulo, c.Descripcion, null,
            c.FechaInicio.ToDateTime(TimeOnly.MinValue), c.FechaFin.ToDateTime(TimeOnly.MinValue),
            true, c.CupoMaximo, c.CupoDisponible, c.Estado));

        // Los eventos se guardan en UTC; se amplía un día por lado para no perder los que caen
        // cerca de medianoche en hora de Bolivia (UTC-4). El frontend filtra por día local.
        var desdeUtc = DateTime.SpecifyKind(desde.AddDays(-1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var hastaUtc = DateTime.SpecifyKind(hasta.AddDays(2).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var eventos = await _db.Eventos
            .Where(e => EstadosVisibles.Contains(e.Estado) && e.FechaInicio >= desdeUtc && e.FechaInicio < hastaUtc)
            .Select(e => new ActividadDto(e.Id, "EVENTO", e.Titulo, e.Descripcion, e.Lugar,
                e.FechaInicio, e.FechaFin, false, null, null, e.Estado))
            .ToListAsync();

        return actividadesCursos.Concat(eventos).OrderBy(a => a.Inicio).ToList();
    }
}
