using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

// Consulta del registro de actividad (solo la Dirección): quién entró, a qué hora y qué hizo.
public record RegistroActividadDto(long Id, DateTime Fecha, Guid? UsuarioId, string? Usuario, string? Email, string? Rol,
                                   string Accion, string Metodo, string Ruta, string? Recurso, int Estado, bool Exito,
                                   string? Ip, string? Navegador, int? DuracionMs);
public record PaginaRegistroActividadDto(List<RegistroActividadDto> Registros, int Total, int Pagina, int TamanoPagina);

// resultado = "EXITO" | "ERROR"; desde/hasta en fecha de Bolivia (inclusive)
public record FiltroRegistroActividad(string? Q = null, string? Resultado = null, DateOnly? Desde = null, DateOnly? Hasta = null,
                                      Guid? UsuarioId = null, int Pagina = 1, int TamanoPagina = 50);

public interface IRegistroActividadService
{
    Task<PaginaRegistroActividadDto> ListarAsync(FiltroRegistroActividad f);
}

public class RegistroActividadService(AppDbContext db) : IRegistroActividadService
{
    public const int MaxPagina = 200;
    private static readonly TimeSpan Huso = TimeSpan.FromHours(4); // Bolivia = UTC-4

    public static DateTime InicioDelDiaUtc(DateOnly d) => DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue) + Huso, DateTimeKind.Utc);

    public async Task<PaginaRegistroActividadDto> ListarAsync(FiltroRegistroActividad f)
    {
        if (f.Desde is { } d1 && f.Hasta is { } d2 && d2 < d1)
            throw new ArgumentException("La fecha \"hasta\" no puede ser anterior a \"desde\"");
        var tamano = Math.Clamp(f.TamanoPagina, 1, MaxPagina);
        var pagina = Math.Max(1, f.Pagina);

        var q = from r in db.RegistroActividad
                join u in db.Usuarios on r.UsuarioId equals (Guid?)u.Id into us
                from u in us.DefaultIfEmpty()
                select new { r, Nombre = u == null ? null : u.NombreCompleto };
        if (f.Desde is { } desde) { var ini = InicioDelDiaUtc(desde); q = q.Where(x => x.r.Fecha >= ini); }
        if (f.Hasta is { } hasta) { var fin = InicioDelDiaUtc(hasta.AddDays(1)); q = q.Where(x => x.r.Fecha < fin); }
        if (f.UsuarioId is { } uid) q = q.Where(x => x.r.UsuarioId == uid);
        if (f.Resultado?.ToUpperInvariant() == "EXITO") q = q.Where(x => x.r.Exito);
        else if (f.Resultado?.ToUpperInvariant() == "ERROR") q = q.Where(x => !x.r.Exito);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var t = $"%{f.Q.Trim()}%";
            q = q.Where(x => EF.Functions.ILike(x.r.Accion, t) || EF.Functions.ILike(x.r.Email ?? "", t) ||
                             EF.Functions.ILike(x.Nombre ?? "", t) || EF.Functions.ILike(x.r.Ip ?? "", t) ||
                             EF.Functions.ILike(x.r.Ruta, t));
        }

        var total = await q.CountAsync();
        var filas = await q.OrderByDescending(x => x.r.Fecha).ThenByDescending(x => x.r.Id)
            .Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
        return new PaginaRegistroActividadDto(
            filas.Select(x => new RegistroActividadDto(x.r.Id, x.r.Fecha, x.r.UsuarioId, x.Nombre, x.r.Email, x.r.Rol, x.r.Accion,
                x.r.Metodo, x.r.Ruta, x.r.Recurso, x.r.Estado, x.r.Exito, x.r.Ip, x.r.Navegador, x.r.DuracionMs)).ToList(),
            total, pagina, tamano);
    }
}
