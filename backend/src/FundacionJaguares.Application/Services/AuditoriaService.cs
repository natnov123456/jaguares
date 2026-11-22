using System.Text.Json;
using FundacionJaguares.Application.Reportes;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

public record ActorAuditoria(Guid? UsuarioId, string Nombre, string? Rol);
public record RegistroAuditoriaDto(Guid Id, Guid DonacionId, long NumeroComprobante, string Accion, Guid? UsuarioId,
                                   string UsuarioNombre, string? Rol, string? Motivo, string? ValorAnterior, string ValorNuevo,
                                   DateTime CreadoEn);
public record PaginaAuditoriaDto(List<RegistroAuditoriaDto> Registros, int Total, int Pagina, int TamanoPagina);
public record UsuarioAuditoriaDto(Guid UsuarioId, string Nombre);
public record FiltroAuditoria(DateOnly? Desde = null, DateOnly? Hasta = null, Guid? UsuarioId = null, string? Accion = null,
                              string? Q = null, int Pagina = 1, int TamanoPagina = 25);

// JAG-88 / FUJ-125: bitácora de auditoría de donaciones.
//   - Registrar(): lo llama DonacionService en la MISMA transacción que la operación, así cada alta, edición o
//     anulación deja exactamente un registro (si la operación falla, tampoco queda el registro).
//   - No existe ningún método ni endpoint para modificar o borrar registros; además la base de datos lo impide
//     con un trigger (db/17_fuj125_auditoria_donaciones.sql).
public interface IAuditoriaService
{
    Task<PaginaAuditoriaDto> ConsultarAsync(FiltroAuditoria f);
    Task<List<UsuarioAuditoriaDto>> UsuariosAsync();
}

public class AuditoriaService(AppDbContext db) : IAuditoriaService
{
    public static readonly string[] Acciones = [AuditoriaDonacion.Alta, AuditoriaDonacion.Edicion, AuditoriaDonacion.Anulacion,
                                                AuditoriaDonacion.Verificacion, AuditoriaDonacion.Rechazo];
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Fotografía de la donación que se guarda como valor anterior / nuevo
    public static string Instantanea(Donacion d, string? donante, string? campana) => JsonSerializer.Serialize(new
    {
        numero = d.NumeroComprobante, fecha = d.Fecha, monto = d.Monto, moneda = d.Moneda.Trim(), medio = d.Medio.ToString(),
        referencia = d.Referencia, estado = d.Estado, campana, donante = donante ?? "Donante anónimo"
    }, Json);

    public static void Registrar(AppDbContext db, Donacion d, string accion, ActorAuditoria actor, string? anterior, string nuevo,
                                 string? motivo = null)
    {
        if (!Acciones.Contains(accion)) throw new ArgumentException("Acción de auditoría no válida");
        db.AuditoriaDonaciones.Add(new AuditoriaDonacion
        {
            DonacionId = d.Id, NumeroComprobante = d.NumeroComprobante, Accion = accion,
            UsuarioId = actor.UsuarioId, UsuarioNombre = actor.Nombre, Rol = actor.Rol, Motivo = motivo,
            ValorAnterior = anterior, ValorNuevo = nuevo
        });
    }

    public async Task<PaginaAuditoriaDto> ConsultarAsync(FiltroAuditoria f)
    {
        FiltroCombinado.Validar(new FiltrosReporte(Q: f.Q, Desde: f.Desde, Hasta: f.Hasta));
        if (!string.IsNullOrWhiteSpace(f.Accion) && !Acciones.Contains(f.Accion.ToUpperInvariant()))
            throw new ArgumentException("Acción no válida: ALTA, EDICION, ANULACION, VERIFICACION o RECHAZO");
        var pagina = Math.Max(1, f.Pagina);
        var tamano = Math.Clamp(f.TamanoPagina, 1, 100);

        var q = db.AuditoriaDonaciones.AsNoTracking();
        // Rango de fechas en hora de Bolivia (UTC-4), inclusivo
        if (f.Desde is { } desde) q = q.Where(a => a.CreadoEn >= desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(4));
        if (f.Hasta is { } hasta) q = q.Where(a => a.CreadoEn < hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(4));
        if (f.UsuarioId is { } uid) q = q.Where(a => a.UsuarioId == uid);
        if (!string.IsNullOrWhiteSpace(f.Accion)) { var acc = f.Accion.ToUpperInvariant(); q = q.Where(a => a.Accion == acc); }
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var t = $"%{f.Q.Trim()}%";
            q = long.TryParse(f.Q.Trim(), out var numero)
                ? q.Where(a => a.NumeroComprobante == numero || EF.Functions.ILike(a.UsuarioNombre, t))
                : q.Where(a => EF.Functions.ILike(a.UsuarioNombre, t) || (a.Motivo != null && EF.Functions.ILike(a.Motivo, t)));
        }

        var total = await q.CountAsync();
        var registros = await q.OrderByDescending(a => a.CreadoEn).ThenByDescending(a => a.Id)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(a => new RegistroAuditoriaDto(a.Id, a.DonacionId, a.NumeroComprobante, a.Accion, a.UsuarioId, a.UsuarioNombre,
                a.Rol, a.Motivo, a.ValorAnterior, a.ValorNuevo, a.CreadoEn))
            .ToListAsync();
        return new PaginaAuditoriaDto(registros, total, pagina, tamano);
    }

    // Personas que aparecen en la bitácora, para el filtro por usuario
    public async Task<List<UsuarioAuditoriaDto>> UsuariosAsync() =>
        (await db.AuditoriaDonaciones.Where(a => a.UsuarioId != null)
            .Select(a => new { UsuarioId = a.UsuarioId!.Value, a.UsuarioNombre }).Distinct().ToListAsync())
        .OrderBy(u => u.UsuarioNombre).Select(u => new UsuarioAuditoriaDto(u.UsuarioId, u.UsuarioNombre)).ToList();
}
