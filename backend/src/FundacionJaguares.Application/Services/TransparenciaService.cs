using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

// FUJ-121: la Directora publica el resumen mensual del reporte financiero (FUJ-106) y el sitio lo
// muestra al público. Solo se exponen montos agregados por categoría: ningún nombre, correo,
// referencia ni detalle de donaciones individuales.
public interface ITransparenciaService
{
    Task<List<MesPublicadoDto>> ListarMesesPublicadosAsync();
    Task<ResumenTransparenciaDto?> ObtenerPublicadoAsync(int anio, int mes);
    Task<ResumenTransparenciaDto> VistaPreviaAsync(int anio, int mes);
    Task<ResumenTransparenciaDto> PublicarAsync(int anio, int mes, PublicarTransparenciaDto dto, Guid publicadoPor);
    Task<bool> DespublicarAsync(int anio, int mes);
}

public class TransparenciaService : ITransparenciaService
{
    public const int MaxDestinos = 15;

    private readonly AppDbContext _db;
    private readonly DonacionRepository _donaciones;

    public TransparenciaService(AppDbContext db, DonacionRepository donaciones)
    { _db = db; _donaciones = donaciones; }

    public Task<List<MesPublicadoDto>> ListarMesesPublicadosAsync() =>
        _db.ReportesTransparencia
            .OrderByDescending(r => r.Anio).ThenByDescending(r => r.Mes)
            .Select(r => new MesPublicadoDto(r.Anio, r.Mes, r.TotalRecaudado))
            .ToListAsync();

    public async Task<ResumenTransparenciaDto?> ObtenerPublicadoAsync(int anio, int mes)
    {
        ValidarPeriodo(anio, mes);
        var r = await BuscarAsync(anio, mes);
        return r is null ? null : ADto(r);
    }

    // Para el formulario de la Directora: totales actuales del reporte financiero + destinos ya publicados
    public async Task<ResumenTransparenciaDto> VistaPreviaAsync(int anio, int mes)
    {
        ValidarPeriodo(anio, mes);
        var (total, cantidad) = await TotalesDelMesAsync(anio, mes);
        var publicado = await BuscarAsync(anio, mes);
        var destinos = publicado?.Destinos.Select(d => new DestinoDto(d.Categoria, d.Monto, d.Descripcion)).ToList() ?? [];
        return Resumen(anio, mes, total, cantidad, destinos, publicado?.Nota, publicado?.PublicadoEn, publicado is not null);
    }

    public async Task<ResumenTransparenciaDto> PublicarAsync(int anio, int mes, PublicarTransparenciaDto dto, Guid publicadoPor)
    {
        ValidarPeriodo(anio, mes);
        var hoy = DateTime.UtcNow;
        if (anio > hoy.Year || (anio == hoy.Year && mes > hoy.Month))
            throw new ArgumentException("No se puede publicar un mes que todavía no empezó");

        var destinos = NormalizarDestinos(dto.Destinos);
        var nota = string.IsNullOrWhiteSpace(dto.Nota) ? null : dto.Nota.Trim();
        if (nota?.Length > 500) throw new ArgumentException("La nota no puede superar 500 caracteres");

        // El total se toma del reporte financiero al momento de publicar (foto del mes)
        var (total, cantidad) = await TotalesDelMesAsync(anio, mes);
        var asignado = destinos.Sum(d => d.Monto);
        if (asignado > total)
            throw new ArgumentException($"Los destinos suman Bs. {asignado:N2} y superan lo recaudado en el mes (Bs. {total:N2})");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var reporte = await BuscarAsync(anio, mes);
        if (reporte is null)
        {
            reporte = new ReporteTransparencia { Anio = anio, Mes = mes };
            _db.ReportesTransparencia.Add(reporte);
        }
        else
        {
            // Se borran primero los destinos anteriores para no chocar con UNIQUE (reporte_id, categoria)
            _db.DestinosTransparencia.RemoveRange(reporte.Destinos);
            await _db.SaveChangesAsync();
            reporte.Destinos.Clear();
        }

        reporte.TotalRecaudado = total;
        reporte.CantidadDonaciones = cantidad;
        reporte.Nota = nota;
        reporte.PublicadoPor = publicadoPor;
        reporte.PublicadoEn = DateTime.UtcNow;
        foreach (var d in destinos)
            reporte.Destinos.Add(new DestinoTransparencia
            {
                ReporteId = reporte.Id, Categoria = d.Categoria, Monto = d.Monto, Descripcion = d.Descripcion
            });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return ADto(reporte);
    }

    public async Task<bool> DespublicarAsync(int anio, int mes)
    {
        ValidarPeriodo(anio, mes);
        var filas = await _db.ReportesTransparencia.Where(r => r.Anio == anio && r.Mes == mes).ExecuteDeleteAsync();
        return filas > 0;
    }

    // ---- auxiliares ----

    private static void ValidarPeriodo(int anio, int mes)
    {
        if (mes is < 1 or > 12) throw new ArgumentException("Mes inválido");
        if (anio is < 2000 or > 2100) throw new ArgumentException("Año inválido");
    }

    private static List<DestinoDto> NormalizarDestinos(List<DestinoDto>? destinos)
    {
        if (destinos is null || destinos.Count == 0) throw new ArgumentException("Agrega al menos un destino");
        if (destinos.Count > MaxDestinos) throw new ArgumentException($"Máximo {MaxDestinos} categorías");

        var lista = new List<DestinoDto>();
        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in destinos)
        {
            var categoria = (d.Categoria ?? "").Trim();
            if (categoria.Length == 0) throw new ArgumentException("Cada destino necesita una categoría");
            if (categoria.Length > 80) throw new ArgumentException("La categoría no puede superar 80 caracteres");
            if (!vistas.Add(categoria)) throw new ArgumentException($"La categoría \"{categoria}\" está repetida");
            if (d.Monto < 0) throw new ArgumentException("Los montos no pueden ser negativos");
            if (decimal.Round(d.Monto, 2) != d.Monto) throw new ArgumentException("Los montos admiten como máximo 2 decimales");
            var descripcion = string.IsNullOrWhiteSpace(d.Descripcion) ? null : d.Descripcion.Trim();
            if (descripcion?.Length > 300) throw new ArgumentException("La descripción no puede superar 300 caracteres");
            lista.Add(new DestinoDto(categoria, d.Monto, descripcion));
        }
        return lista;
    }

    private async Task<(decimal Total, int Cantidad)> TotalesDelMesAsync(int anio, int mes)
    {
        var total = await _donaciones.TotalPeriodoAsync(anio, mes);
        var cantidad = await _db.Donaciones
            .CountAsync(d => d.Estado == "CONFIRMADA" && d.Fecha.Year == anio && d.Fecha.Month == mes);
        return (total, cantidad);
    }

    private Task<ReporteTransparencia?> BuscarAsync(int anio, int mes) =>
        _db.ReportesTransparencia.Include(r => r.Destinos).FirstOrDefaultAsync(r => r.Anio == anio && r.Mes == mes);

    private static ResumenTransparenciaDto ADto(ReporteTransparencia r) =>
        Resumen(r.Anio, r.Mes, r.TotalRecaudado, r.CantidadDonaciones,
            r.Destinos.Select(d => new DestinoDto(d.Categoria, d.Monto, d.Descripcion)).ToList(),
            r.Nota, r.PublicadoEn, true);

    private static ResumenTransparenciaDto Resumen(int anio, int mes, decimal total, int cantidad,
        List<DestinoDto> destinos, string? nota, DateTime? publicadoEn, bool publicado)
    {
        var asignado = destinos.Sum(d => d.Monto);
        var detalle = destinos
            .OrderByDescending(d => d.Monto)
            .Select(d => new DestinoResumenDto(d.Categoria, d.Monto, d.Descripcion,
                total > 0 ? Math.Round(d.Monto * 100 / total, 1) : 0))
            .ToList();
        return new ResumenTransparenciaDto(anio, mes, total, cantidad, asignado, detalle, nota, publicadoEn, publicado);
    }
}
