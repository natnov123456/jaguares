using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

// Consulta del registro de actividad (solo la Dirección): quién entró, a qué hora y qué hizo.
public record RegistroActividadDto(long Id, DateTime Fecha, Guid? UsuarioId, string? Usuario, string? Email, string? Rol,
                                   string Accion, string Metodo, string Ruta, string? Recurso, int Estado, bool Exito,
                                   string? Ip, string? Navegador, int? DuracionMs);
public record PaginaRegistroActividadDto(List<RegistroActividadDto> Registros, int Total, int Pagina, int TamanoPagina);

// Vigilancia: resumen de lo sospechoso en las últimas horas (inicios de sesión fallidos, accesos denegados, bloqueos)
public record IpSospechosaDto(string Ip, int Fallos, int CuentasDistintas, DateTime Ultimo);
public record CuentaSospechosaDto(string Email, int Fallos, DateTime Ultimo);
public record ResumenSeguridadDto(int Horas, int LoginsFallidos, int AccesosDenegados, int Bloqueos,
                                  List<IpSospechosaDto> Ips, List<CuentaSospechosaDto> Cuentas);

// resultado = "EXITO" | "ERROR" | "SOSPECHOSO" (denegados 401/403 y bloqueados 429); desde/hasta en fecha de Bolivia (inclusive)
public record FiltroRegistroActividad(string? Q = null, string? Resultado = null, DateOnly? Desde = null, DateOnly? Hasta = null,
                                      Guid? UsuarioId = null, int Pagina = 1, int TamanoPagina = 50);

public interface IRegistroActividadService
{
    Task<PaginaRegistroActividadDto> ListarAsync(FiltroRegistroActividad f);
    Task<ResumenSeguridadDto> ResumenSeguridadAsync(int horas);
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
        else if (f.Resultado?.ToUpperInvariant() == "SOSPECHOSO") q = q.Where(x => x.r.Estado == 401 || x.r.Estado == 403 || x.r.Estado == 429);
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

    public const string RutaLogin = "/api/auth/login"; // se compara en minúsculas
    // Una IP con 5 o más fallos, o una cuenta con 3 o más inicios de sesión fallidos, aparece en el resumen
    public const int MinFallosIp = 5, MinFallosCuenta = 3;

    public async Task<ResumenSeguridadDto> ResumenSeguridadAsync(int horas)
    {
        horas = Math.Clamp(horas, 1, 24 * 30);
        var desde = DateTime.UtcNow.AddHours(-horas);
        var fallos = db.RegistroActividad.Where(r => r.Fecha >= desde && (r.Estado == 401 || r.Estado == 403 || r.Estado == 429));
        var loginsFallidos = await fallos.CountAsync(r => r.Ruta.ToLower() == RutaLogin && r.Estado == 401);
        var denegados = await fallos.CountAsync(r => r.Ruta.ToLower() != RutaLogin && (r.Estado == 401 || r.Estado == 403));
        var bloqueos = await fallos.CountAsync(r => r.Estado == 429);
        var ips = await fallos.Where(r => r.Ip != null)
            .GroupBy(r => r.Ip!)
            .Select(g => new { Ip = g.Key, Fallos = g.Count(), Cuentas = g.Select(r => r.Email).Distinct().Count(), Ultimo = g.Max(r => r.Fecha) })
            .Where(x => x.Fallos >= MinFallosIp)
            .OrderByDescending(x => x.Fallos).Take(10).ToListAsync();
        var cuentas = await fallos.Where(r => r.Ruta.ToLower() == RutaLogin && r.Email != null)
            .GroupBy(r => r.Email!.ToLower())
            .Select(g => new { Email = g.Key, Fallos = g.Count(), Ultimo = g.Max(r => r.Fecha) })
            .Where(x => x.Fallos >= MinFallosCuenta)
            .OrderByDescending(x => x.Fallos).Take(10).ToListAsync();
        return new ResumenSeguridadDto(horas, loginsFallidos, denegados, bloqueos,
            ips.Select(x => new IpSospechosaDto(x.Ip, x.Fallos, x.Cuentas, x.Ultimo)).ToList(),
            cuentas.Select(x => new CuentaSospechosaDto(x.Email, x.Fallos, x.Ultimo)).ToList());
    }
}
