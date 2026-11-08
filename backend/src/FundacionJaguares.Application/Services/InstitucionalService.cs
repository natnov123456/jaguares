using System.Text.Json;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;

namespace FundacionJaguares.Application.Services;

// Errores de validación por campo: el frontend los muestra junto a cada input
public class ValidacionException : Exception
{
    public Dictionary<string, string> Errores { get; }
    public ValidacionException(Dictionary<string, string> errores) : base("Datos inválidos") => Errores = errores;
}

// FUJ-127: contenido de "Quiénes somos"
public interface IInstitucionalService
{
    Task<PaginaInstitucionalDto> ObtenerAsync();
    Task<PaginaInstitucionalDto> ActualizarAsync(PaginaInstitucionalDto dto, Guid actualizadoPor);
}

public class InstitucionalService : IInstitucionalService
{
    public const int MaxMiembros = 30;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;

    public InstitucionalService(AppDbContext db) => _db = db;

    public async Task<PaginaInstitucionalDto> ObtenerAsync()
    {
        var p = await _db.PaginaInstitucional.FindAsync(1);
        return p is null ? new PaginaInstitucionalDto(null, null, null, [], null, null, null, null, null) : ADto(p);
    }

    public async Task<PaginaInstitucionalDto> ActualizarAsync(PaginaInstitucionalDto dto, Guid actualizadoPor)
    {
        var errores = new Dictionary<string, string>();
        string? Texto(string? valor, string campo, int max)
        {
            var v = string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
            if (v?.Length > max) errores[campo] = $"Máximo {max} caracteres";
            return v;
        }

        var mision = Texto(dto.Mision, "mision", 3000);
        var vision = Texto(dto.Vision, "vision", 3000);
        var historia = Texto(dto.Historia, "historia", 10000);
        var direccion = Texto(dto.Direccion, "direccion", 300);
        var telefono = Texto(dto.Telefono, "telefono", 50);
        var email = Texto(dto.EmailContacto, "emailContacto", 150);
        var horario = Texto(dto.Horario, "horario", 200);
        if (email is not null && !ContactoService.EmailValido(email)) errores["emailContacto"] = "Correo inválido";

        var equipo = new List<MiembroEquipoDto>();
        var miembros = dto.Equipo ?? [];
        if (miembros.Count > MaxMiembros) errores["equipo"] = $"Máximo {MaxMiembros} personas";
        foreach (var m in miembros.Take(MaxMiembros))
        {
            var nombre = (m.Nombre ?? "").Trim();
            if (nombre.Length == 0) { errores["equipo"] = "Cada integrante necesita un nombre"; continue; }
            var cargo = string.IsNullOrWhiteSpace(m.Cargo) ? null : m.Cargo.Trim();
            var desc = string.IsNullOrWhiteSpace(m.Descripcion) ? null : m.Descripcion.Trim();
            if (nombre.Length > 150 || cargo?.Length > 150 || desc?.Length > 500)
                errores["equipo"] = "Nombre y cargo hasta 150 caracteres; descripción hasta 500";
            equipo.Add(new MiembroEquipoDto(nombre, cargo, desc));
        }
        if (errores.Count > 0) throw new ValidacionException(errores);

        var p = await _db.PaginaInstitucional.FindAsync(1);
        if (p is null) { p = new PaginaInstitucional(); _db.PaginaInstitucional.Add(p); }
        p.Mision = mision;
        p.Vision = vision;
        p.Historia = historia;
        p.EquipoJson = JsonSerializer.Serialize(equipo, Json);
        p.Direccion = direccion;
        p.Telefono = telefono;
        p.EmailContacto = email;
        p.Horario = horario;
        p.ActualizadoPor = actualizadoPor;
        p.ActualizadoEn = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ADto(p);
    }

    private static PaginaInstitucionalDto ADto(PaginaInstitucional p)
    {
        List<MiembroEquipoDto> equipo;
        try { equipo = JsonSerializer.Deserialize<List<MiembroEquipoDto>>(p.EquipoJson, Json) ?? []; }
        catch (JsonException) { equipo = []; }
        return new PaginaInstitucionalDto(p.Mision, p.Vision, p.Historia, equipo, p.Direccion, p.Telefono,
            p.EmailContacto, p.Horario, p.ActualizadoEn);
    }
}
