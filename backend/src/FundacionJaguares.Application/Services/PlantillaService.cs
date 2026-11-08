using System.Text.RegularExpressions;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

// FUJ-115: plantillas de certificado por tipo de actividad (logo, firmas y texto) y generación del PDF
public interface IPlantillaService
{
    Task<List<PlantillaCertificadoDto>> ListarAsync();
    Task<PlantillaCertificadoDto> CrearAsync(GuardarPlantillaDto dto);
    Task<PlantillaCertificadoDto> ActualizarAsync(Guid id, GuardarPlantillaDto dto);
    Task EliminarAsync(Guid id);
    Task<PlantillaCertificadoDto> SubirImagenAsync(Guid id, string campo, ArchivoSubido archivo);
    Task<PlantillaCertificadoDto> QuitarImagenAsync(Guid id, string campo);
    Task<byte[]> PrevisualizarAsync(Guid id);
    Task<DisenoCertificado> CargarDisenoAsync(Guid? plantillaId);
    Task<string> GuardarPdfAsync(DisenoCertificado diseno, DatosCertificado datos, Guid cursoId, Guid certificadoId);
}

public partial class PlantillaService : IPlantillaService
{
    public const int LadoMaximoImagen = 1200;
    private const int MaxTexto = 1000, MaxCampo = 120;

    private static readonly string[] Tipos = ["TALLER", "CAPACITACION", "VOLUNTARIADO", "EVENTO", "OTRO"];

    // Diseño usado por los cursos sin plantilla asignada
    private static readonly DisenoCertificado DisenoEstandar = new("CERTIFICADO",
        "La Fundación Unión de los Jaguares certifica que {participante} completó satisfactoriamente la actividad {actividad}, el {fecha}.",
        null, []);

    private readonly AppDbContext _db;
    private readonly IAlmacenamientoArchivos _archivos;

    public PlantillaService(AppDbContext db, IAlmacenamientoArchivos archivos) { _db = db; _archivos = archivos; }

    public async Task<List<PlantillaCertificadoDto>> ListarAsync() =>
        (await _db.PlantillasCertificado.OrderBy(p => p.TipoActividad).ThenBy(p => p.Nombre).ToListAsync())
            .Select(ADto).ToList();

    public async Task<PlantillaCertificadoDto> CrearAsync(GuardarPlantillaDto dto)
    {
        var p = new PlantillaCertificado();
        await AplicarAsync(p, dto);
        _db.PlantillasCertificado.Add(p);
        await _db.SaveChangesAsync();
        return ADto(p);
    }

    public async Task<PlantillaCertificadoDto> ActualizarAsync(Guid id, GuardarPlantillaDto dto)
    {
        var p = await ObtenerAsync(id);
        await AplicarAsync(p, dto);
        p.ActualizadoEn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ADto(p);
    }

    public async Task EliminarAsync(Guid id)
    {
        var p = await ObtenerAsync(id);
        var enUso = await _db.Cursos.CountAsync(c => c.PlantillaId == id && c.Estado != "FINALIZADO" && c.Estado != "CANCELADO");
        if (enUso > 0)
            throw new InvalidOperationException($"La plantilla está asignada a {enUso} curso(s) activo(s). Cámbiales la plantilla antes de eliminarla.");

        _db.PlantillasCertificado.Remove(p);
        await _db.SaveChangesAsync();
        // Los PDF ya emitidos llevan las imágenes incrustadas: no dependen de estos archivos
        _archivos.Eliminar(p.LogoUrl);
        _archivos.Eliminar(p.FirmaUnoUrl);
        _archivos.Eliminar(p.FirmaDosUrl);
    }

    public async Task<PlantillaCertificadoDto> SubirImagenAsync(Guid id, string campo, ArchivoSubido archivo)
    {
        var p = await ObtenerAsync(id);
        var (actual, asignar) = CampoImagen(p, campo);

        var (bytes, esPng) = await ProcesadorImagen.LeerValidadoAsync(archivo);
        var (imagen, extension) = ProcesadorImagen.Normalizar(bytes, esPng, LadoMaximoImagen);
        string url;
        using (var datos = new MemoryStream(imagen))
            url = await _archivos.GuardarAsync($"certificados/plantillas/{id:N}", Guid.NewGuid().ToString("N") + extension, datos);

        asignar(url);
        p.ActualizadoEn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _archivos.Eliminar(actual);
        return ADto(p);
    }

    public async Task<PlantillaCertificadoDto> QuitarImagenAsync(Guid id, string campo)
    {
        var p = await ObtenerAsync(id);
        var (actual, asignar) = CampoImagen(p, campo);
        asignar(null);
        p.ActualizadoEn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _archivos.Eliminar(actual);
        return ADto(p);
    }

    public async Task<byte[]> PrevisualizarAsync(Guid id)
    {
        var p = await ObtenerAsync(id);
        return GeneradorCertificadoPdf.Generar(await DisenoDeAsync(p),
            new DatosCertificado("Nombre del Participante", "Actividad de ejemplo", DateTime.UtcNow, "FUJ-VISTA-PREVIA"));
    }

    public async Task<DisenoCertificado> CargarDisenoAsync(Guid? plantillaId)
    {
        if (plantillaId is null) return DisenoEstandar;
        var p = await _db.PlantillasCertificado.FindAsync(plantillaId.Value);
        return p is null ? DisenoEstandar : await DisenoDeAsync(p);
    }

    public async Task<string> GuardarPdfAsync(DisenoCertificado diseno, DatosCertificado datos, Guid cursoId, Guid certificadoId)
    {
        using var pdf = new MemoryStream(GeneradorCertificadoPdf.Generar(diseno, datos));
        return await _archivos.GuardarAsync($"certificados/{cursoId:N}", $"{certificadoId:N}.pdf", pdf);
    }

    // ---------- auxiliares ----------

    private async Task<PlantillaCertificado> ObtenerAsync(Guid id) =>
        await _db.PlantillasCertificado.FindAsync(id) ?? throw new KeyNotFoundException("Plantilla no encontrada");

    private async Task<DisenoCertificado> DisenoDeAsync(PlantillaCertificado p) =>
        new(p.Titulo, p.Texto, await _archivos.LeerAsync(p.LogoUrl),
        [
            new(await _archivos.LeerAsync(p.FirmaUnoUrl), p.FirmaUnoNombre, p.FirmaUnoCargo),
            new(await _archivos.LeerAsync(p.FirmaDosUrl), p.FirmaDosNombre, p.FirmaDosCargo)
        ]);

    private static (string? Actual, Action<string?> Asignar) CampoImagen(PlantillaCertificado p, string campo) => campo switch
    {
        "logo" => (p.LogoUrl, v => p.LogoUrl = v),
        "firmaUno" => (p.FirmaUnoUrl, v => p.FirmaUnoUrl = v),
        "firmaDos" => (p.FirmaDosUrl, v => p.FirmaDosUrl = v),
        _ => throw new ArgumentException("Imagen no válida: usa logo, firmaUno o firmaDos")
    };

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Variable();

    private async Task AplicarAsync(PlantillaCertificado p, GuardarPlantillaDto dto)
    {
        var nombre = Requerido(dto.Nombre, "El nombre", MaxCampo);
        var titulo = Requerido(dto.Titulo, "El título", MaxCampo);
        var texto = Requerido(dto.Texto, "El texto", MaxTexto);

        var tipo = (dto.TipoActividad ?? "").Trim().ToUpperInvariant();
        if (!Tipos.Contains(tipo)) throw new ArgumentException("Tipo de actividad no válido");

        var desconocida = Variable().Matches(texto).Select(m => m.Value)
            .FirstOrDefault(v => !GeneradorCertificadoPdf.Variables.Contains(v));
        if (desconocida is not null)
            throw new ArgumentException($"Variable desconocida {desconocida}. Usa: {string.Join(", ", GeneradorCertificadoPdf.Variables)}");
        if (!texto.Contains("{participante}"))
            throw new ArgumentException("El texto debe incluir {participante} para mostrar el nombre de la persona");

        var minusculas = nombre.ToLowerInvariant();
        if (await _db.PlantillasCertificado.AnyAsync(x => x.Id != p.Id && x.Nombre.ToLower() == minusculas))
            throw new ArgumentException("Ya existe una plantilla con ese nombre");

        p.Nombre = nombre;
        p.TipoActividad = tipo;
        p.Titulo = titulo;
        p.Texto = texto;
        p.FirmaUnoNombre = Opcional(dto.FirmaUnoNombre, "El nombre de la firma 1");
        p.FirmaUnoCargo = Opcional(dto.FirmaUnoCargo, "El cargo de la firma 1");
        p.FirmaDosNombre = Opcional(dto.FirmaDosNombre, "El nombre de la firma 2");
        p.FirmaDosCargo = Opcional(dto.FirmaDosCargo, "El cargo de la firma 2");
    }

    private static string Requerido(string? valor, string campo, int maximo) =>
        Opcional(valor, campo, maximo) ?? throw new ArgumentException($"{campo} es obligatorio");

    private static string? Opcional(string? valor, string campo, int maximo = MaxCampo)
    {
        var v = string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
        if (v?.Length > maximo) throw new ArgumentException($"{campo} no puede superar {maximo} caracteres");
        return v;
    }

    private static PlantillaCertificadoDto ADto(PlantillaCertificado p) =>
        new(p.Id, p.Nombre, p.TipoActividad, p.Titulo, p.Texto, p.LogoUrl,
            p.FirmaUnoUrl, p.FirmaUnoNombre, p.FirmaUnoCargo,
            p.FirmaDosUrl, p.FirmaDosNombre, p.FirmaDosCargo, p.ActualizadoEn);
}
