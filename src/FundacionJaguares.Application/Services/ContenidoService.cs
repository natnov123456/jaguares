using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

public interface IContenidoService
{
    Task<List<Noticia>> ListarPublicadasAsync();
    Task<Noticia> CrearNoticiaAsync(CrearNoticiaDto dto, Guid autorId);
    Task<List<GaleriaItem>> ListarGaleriaAsync();
}

public class ContenidoService : IContenidoService
{
    private readonly ContenidoRepository _noticias;
    private readonly AppDbContext _db;

    public ContenidoService(ContenidoRepository noticias, AppDbContext db)
    {
        _noticias = noticias;
        _db = db;
    }

    public Task<List<Noticia>> ListarPublicadasAsync() => _noticias.GetPublicadasAsync();

    public async Task<Noticia> CrearNoticiaAsync(CrearNoticiaDto dto, Guid autorId)
    {
        var n = new Noticia
        {
            Titulo = dto.Titulo,
            Resumen = dto.Resumen,
            Contenido = dto.Contenido,
            ImagenUrl = dto.ImagenUrl,
            Publicada = dto.Publicada,
            AutorId = autorId,
            PublicadoEn = dto.Publicada ? DateTime.UtcNow : null
        };
        await _noticias.AddAsync(n);
        await _noticias.SaveChangesAsync();
        return n;
    }

    public Task<List<GaleriaItem>> ListarGaleriaAsync() =>
        _db.Galeria.OrderBy(g => g.Orden).ToListAsync();
}