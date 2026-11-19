using System.Diagnostics;
using System.Text.Json;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using Microsoft.Extensions.Logging;

namespace FundacionJaguares.Application.Reportes;

public record ArchivoReporte(byte[] Contenido, string TipoContenido, string NombreArchivo, int Filas);
public record ReporteDisponibleDto(string Clave, string Titulo);

// JAG-86 / FUJ-124: punto único por el que salen TODOS los reportes exportables.
//   1) Verifica permisos ANTES de consultar datos (rol permitido para el reporte; la definición además
//      rechaza actividades ajenas, p. ej. el curso de otro docente).
//   2) Valida los filtros combinados y arma la tabla.
//   3) Si no hay filas, avisa "No hay datos para exportar" (no genera un archivo vacío).
//   4) Genera PDF (plantilla institucional) o Excel.
//   5) Registra cada exportación — usuario, rol, fecha, filtros, filas y duración — y los errores de render,
//      en la tabla exportaciones y en el log de la aplicación.
public interface IReporteService
{
    IReadOnlyList<ReporteDisponibleDto> Disponibles(SolicitanteReporte s);
    Task<TablaReporte> VistaPreviaAsync(string clave, SolicitanteReporte s, FiltrosReporte f);
    Task<ArchivoReporte> ExportarAsync(string clave, FormatoReporte formato, SolicitanteReporte s, FiltrosReporte f);
}

public class ReporteService : IReporteService
{
    private static readonly JsonSerializerOptions JsonFiltros = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly Dictionary<string, DefinicionReporte> _reportes;
    private readonly AppDbContext _db;
    private readonly ILogger<ReporteService> _log;
    private readonly Func<DateTime> _ahora;
    private readonly Func<TablaReporte, FormatoReporte, DateTime, byte[]> _renderizar;

    public static byte[] Renderizar(TablaReporte t, FormatoReporte formato, DateTime generado) =>
        formato == FormatoReporte.PDF ? PlantillaPdfInstitucional.Generar(t, generado) : GeneradorExcel.Generar(t, generado);

    public ReporteService(IEnumerable<IProveedorReportes> proveedores, AppDbContext db, ILogger<ReporteService> log)
        : this(proveedores, db, log, () => DateTime.UtcNow, Renderizar) { }

    // Las pruebas inyectan la hora y el renderizador (p. ej. uno que falla, para verificar el registro del error)
    public ReporteService(IEnumerable<IProveedorReportes> proveedores, AppDbContext db, ILogger<ReporteService> log,
                          Func<DateTime> ahora, Func<TablaReporte, FormatoReporte, DateTime, byte[]> renderizar)
    {
        _reportes = proveedores.SelectMany(p => p.Reportes()).ToDictionary(r => r.Clave, StringComparer.OrdinalIgnoreCase);
        _db = db; _log = log; _ahora = ahora; _renderizar = renderizar;
    }

    public IReadOnlyList<ReporteDisponibleDto> Disponibles(SolicitanteReporte s) =>
        _reportes.Values.Where(r => s.Es(r.Roles)).OrderBy(r => r.Titulo).Select(r => new ReporteDisponibleDto(r.Clave, r.Titulo)).ToList();

    private DefinicionReporte Autorizar(string clave, SolicitanteReporte s)
    {
        if (!_reportes.TryGetValue(clave, out var def)) throw new KeyNotFoundException("Reporte no encontrado");
        if (!s.Es(def.Roles)) throw new UnauthorizedAccessException("No tienes permiso para exportar este reporte");
        return def;
    }

    public async Task<TablaReporte> VistaPreviaAsync(string clave, SolicitanteReporte s, FiltrosReporte f)
    {
        var def = Autorizar(clave, s);
        FiltroCombinado.Validar(f);
        return await def.Construir(s, f);
    }

    public async Task<ArchivoReporte> ExportarAsync(string clave, FormatoReporte formato, SolicitanteReporte s, FiltrosReporte f)
    {
        var def = Autorizar(clave, s); // 403 sin tocar los datos ni dejar rastro de contenido
        FiltroCombinado.Validar(f);
        if (formato == FormatoReporte.JSON) throw new ArgumentException("Formato no válido: usa pdf o xlsx");

        var reloj = Stopwatch.StartNew();
        var tabla = await def.Construir(s, f);
        if (tabla.Vacia) throw new SinDatosException();

        var generado = _ahora();
        try
        {
            var bytes = _renderizar(tabla, formato, generado);
            reloj.Stop();
            await RegistrarAsync(s, def.Clave, formato, f, tabla.Filas.Count, true, null, reloj.ElapsedMilliseconds);
            _log.LogInformation("[EXPORTACION] Usuario {UsuarioId} ({Rol}) exportó {Reporte} en {Formato} con filtros {Filtros}: {Filas} filas en {Ms} ms",
                s.UsuarioId, s.Rol, def.Clave, formato, SerializarFiltros(f), tabla.Filas.Count, reloj.ElapsedMilliseconds);

            var ext = formato == FormatoReporte.PDF ? "pdf" : "xlsx";
            var tipo = formato == FormatoReporte.PDF ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return new ArchivoReporte(bytes, tipo, $"{def.Clave}-{FiltroCombinado.FechaBolivia(generado):yyyy-MM-dd}.{ext}", tabla.Filas.Count);
        }
        catch (Exception ex) when (ex is not SinDatosException)
        {
            // Error de render: queda registrado con el reporte y los filtros para poder reproducirlo
            reloj.Stop();
            _log.LogError(ex, "[EXPORTACION] Error al generar {Reporte} en {Formato} para {UsuarioId} ({Rol}) con filtros {Filtros}",
                def.Clave, formato, s.UsuarioId, s.Rol, SerializarFiltros(f));
            await RegistrarAsync(s, def.Clave, formato, f, tabla.Filas.Count, false, ex.GetType().Name + ": " + ex.Message, reloj.ElapsedMilliseconds);
            throw new InvalidOperationException("No se pudo generar el archivo. El error quedó registrado.", ex);
        }
    }

    public static string SerializarFiltros(FiltrosReporte f) => JsonSerializer.Serialize(f, JsonFiltros);

    private async Task RegistrarAsync(SolicitanteReporte s, string reporte, FormatoReporte formato, FiltrosReporte f,
                                      int filas, bool exito, string? error, long ms)
    {
        _db.Exportaciones.Add(new Exportacion
        {
            UsuarioId = s.UsuarioId, Rol = s.Rol, Reporte = reporte, Formato = formato.ToString(),
            Filtros = SerializarFiltros(f), Filas = filas, Exito = exito,
            Error = error is null ? null : error[..Math.Min(error.Length, 500)], DuracionMs = (int)Math.Min(ms, int.MaxValue)
        });
        await _db.SaveChangesAsync();
    }
}
