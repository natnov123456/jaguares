using System.Globalization;
using System.Text.Json;
using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace FundacionJaguares.Application.Services;

public record SerieMesDto(string Mes, decimal Total);
public record InscritosCursoDto(Guid CursoId, string Curso, int Inscritos);
public record IndicadoresDto(
    string Mes, DateTime CalculadoEn, bool DesdeCache,
    int InscritosMes, int InscritosCursos, int InscritosEventos,
    double? AsistenciaPromedio,
    int CertificadosEmitidos,
    decimal DonacionesMes, decimal DonacionesMesAnterior, double? VariacionDonacionesPct, int CantidadDonaciones,
    int VoluntariosActivos, int VoluntariosConActividad,
    double? SatisfaccionPromedio, int RespuestasEncuestas,
    List<SerieMesDto> DonacionesUltimosMeses, List<InscritosCursoDto> InscritosPorCurso);

// JAG-84 / FUJ-123: indicadores del mes para la Directora, en una sola consulta al endpoint.
// El resultado se guarda en caché (Redis en producción, memoria en desarrollo) por 10 minutos por mes.
//   - Inscritos del mes: inscripciones a cursos y eventos hechas en el mes.
//   - Asistencia promedio: (clases presentes + asistentes a eventos) / (registros de asistencia + inscritos a eventos) del mes.
//   - Certificados emitidos en el mes.
//   - Donaciones confirmadas del mes vs. el mes anterior (las anuladas no suman).
//   - Voluntarios activos registrados hasta el fin de mes, y cuántos participaron en el mes.
//   - Satisfacción promedio (1 a 5) de las respuestas de encuestas recibidas en el mes.
// Todas las fechas se cortan en hora de Bolivia (UTC-4).
public interface IIndicadorService
{
    Task<IndicadoresDto> ObtenerAsync(int anio, int mes);
}

public class IndicadorService(AppDbContext db, IDistributedCache cache) : IIndicadorService
{
    public static readonly TimeSpan DuracionCache = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Huso = TimeSpan.FromHours(4); // Bolivia = UTC-4

    public static string Clave(int anio, int mes) => $"indicadores:{anio:0000}-{mes:00}";

    // Inicio del mes en hora de Bolivia, expresado en UTC
    public static DateTime InicioUtc(int anio, int mes) => DateTime.SpecifyKind(new DateTime(anio, mes, 1) + Huso, DateTimeKind.Utc);

    public static (int Anio, int Mes) Parsear(string? mes, DateTime ahoraUtc)
    {
        if (string.IsNullOrWhiteSpace(mes))
        {
            var bo = ahoraUtc - Huso;
            return (bo.Year, bo.Month);
        }
        if (!DateTime.TryParseExact(mes.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
            || f.Year < 2000 || f.Year > 2100)
            throw new ArgumentException("Mes no válido: usa el formato AAAA-MM");
        return (f.Year, f.Month);
    }

    public async Task<IndicadoresDto> ObtenerAsync(int anio, int mes)
    {
        var clave = Clave(anio, mes);
        var guardado = await cache.GetStringAsync(clave);
        if (guardado is not null)
            return JsonSerializer.Deserialize<IndicadoresDto>(guardado)! with { DesdeCache = true };

        var dto = await CalcularAsync(anio, mes);
        await cache.SetStringAsync(clave, JsonSerializer.Serialize(dto),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = DuracionCache });
        return dto;
    }

    private async Task<IndicadoresDto> CalcularAsync(int anio, int mes)
    {
        var desde = InicioUtc(anio, mes);
        var hasta = desde.AddMonths(1);
        var primerDia = new DateOnly(anio, mes, 1);
        var ultimoDia = primerDia.AddMonths(1).AddDays(-1);

        // Inscritos
        var inscritosCursos = await db.Inscripciones.CountAsync(i => i.FechaInscripcion >= desde && i.FechaInscripcion < hasta && i.Estado != "CANCELADA");
        var inscritosEventos = await db.InscripcionesEvento.CountAsync(i => i.InscritoEn >= desde && i.InscritoEn < hasta && i.Estado != "CANCELADA");

        // Asistencia: clases del mes (cursos) y eventos realizados en el mes
        var registros = await db.AsistenciasCurso.Where(a => a.Fecha >= primerDia && a.Fecha <= ultimoDia)
            .GroupBy(_ => 1).Select(g => new { Total = g.Count(), Presentes = g.Count(a => a.Presente) }).FirstOrDefaultAsync();
        var eventosMes = db.Eventos.Where(e => e.FechaInicio >= desde && e.FechaInicio < hasta && e.Estado != "CANCELADO" && e.FechaInicio <= DateTime.UtcNow)
            .Select(e => e.Id);
        var inscritosEv = await db.InscripcionesEvento.CountAsync(i => eventosMes.Contains(i.EventoId) && i.Estado != "CANCELADA");
        var asistentesEv = await db.InscripcionesEvento.CountAsync(i => eventosMes.Contains(i.EventoId) &&
            (i.Estado == "ASISTIO" || db.AsistenciasEvento.Any(a => a.EventoId == i.EventoId && a.ParticipanteId == i.ParticipanteId)));
        var denominador = (registros?.Total ?? 0) + inscritosEv;
        double? asistencia = denominador == 0 ? null
            : Math.Round(((registros?.Presentes ?? 0) + asistentesEv) * 100.0 / denominador, 1);

        var certificados = await db.Certificados.CountAsync(c => c.EmitidoEn >= desde && c.EmitidoEn < hasta);

        // Donaciones: mes, mes anterior y serie de los últimos 6 meses
        var inicioSerie = desde.AddMonths(-5);
        var donaciones = await db.Donaciones.Where(d => d.Estado == "CONFIRMADA" && d.Fecha >= inicioSerie && d.Fecha < hasta)
            .Select(d => new { d.Fecha, d.Monto }).ToListAsync();
        var serie = Enumerable.Range(0, 6).Select(i =>
        {
            var ini = inicioSerie.AddMonths(i);
            var fin = ini.AddMonths(1);
            var local = ini - Huso;
            return new SerieMesDto($"{local:yyyy-MM}", donaciones.Where(d => d.Fecha >= ini && d.Fecha < fin).Sum(d => d.Monto));
        }).ToList();
        var delMes = serie[^1].Total;
        var anterior = serie[^2].Total;
        double? variacion = anterior == 0 ? null : Math.Round((double)((delMes - anterior) / anterior * 100), 1);
        var cantidad = donaciones.Count(d => d.Fecha >= desde);

        // Voluntarios
        var activos = await db.Voluntarios.CountAsync(v => v.Estado == "ACTIVO" && v.PostuladoEn < hasta);
        var conActividad = await db.ParticipacionesVoluntario.Where(p => p.Fecha >= desde && p.Fecha < hasta)
            .Select(p => p.VoluntarioId).Distinct().CountAsync();

        // Satisfacción de las respuestas recibidas en el mes (preguntas de escala 1 a 5)
        var valores = await (from r in db.EncuestaRespuestas
                             join i in db.EncuestaInvitaciones on r.InvitacionId equals i.Id
                             join p in db.EncuestaPreguntas on r.PreguntaId equals p.Id
                             where p.Tipo == "ESCALA" && r.Valor != null && i.RespondidaEn >= desde && i.RespondidaEn < hasta
                             select (int)r.Valor!.Value).ToListAsync();
        var respuestas = await db.EncuestaInvitaciones.CountAsync(i => i.RespondidaEn >= desde && i.RespondidaEn < hasta);

        // Inscritos por curso de los cursos que estuvieron activos en el mes
        var porCurso = await db.Cursos
            .Where(c => c.FechaInicio <= ultimoDia && c.FechaFin >= primerDia && c.Estado != "CANCELADO")
            .Select(c => new { c.Id, c.Titulo, Inscritos = db.Inscripciones.Count(i => i.CursoId == c.Id && i.Estado == "CONFIRMADA") })
            .OrderByDescending(x => x.Inscritos).ThenBy(x => x.Titulo).Take(15)
            .Select(x => new InscritosCursoDto(x.Id, x.Titulo, x.Inscritos)).ToListAsync();

        return new IndicadoresDto($"{anio:0000}-{mes:00}", DateTime.UtcNow, false,
            inscritosCursos + inscritosEventos, inscritosCursos, inscritosEventos,
            asistencia, certificados,
            delMes, anterior, variacion, cantidad,
            activos, conActividad,
            valores.Count == 0 ? null : Math.Round(valores.Average(), 2), respuestas,
            serie, porCurso);
    }
}
