namespace FundacionJaguares.Application.Reportes;

// JAG-86 / FUJ-124: modelo común de todos los reportes exportables (PDF y Excel).
// Cada pantalla arma una TablaReporte; la plantilla institucional y el generador de Excel la dibujan igual.

// Peso: ancho relativo de la columna. Numerica: se alinea a la derecha.
public record ColumnaReporte(string Titulo, float Peso = 1, bool Numerica = false);

public record ParReporte(string Etiqueta, string Valor);

public record TablaReporte(
    string Titulo,
    string? Subtitulo,
    IReadOnlyList<ColumnaReporte> Columnas,
    IReadOnlyList<string[]> Filas,
    IReadOnlyList<ParReporte> Filtros,
    IReadOnlyList<ParReporte>? Totales = null)
{
    public bool Vacia => Filas.Count == 0;
}

// Filtros combinados que acepta cualquier reporte (mismos nombres que usan las pantallas):
//   q: texto libre · estado · desde/hasta: rango de fechas inclusivo · actividadId: curso o evento · rol · accion · usuarioId
public record FiltrosReporte(
    string? Q = null,
    string? Estado = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    Guid? ActividadId = null,
    string? Rol = null,
    string? Accion = null,
    Guid? UsuarioId = null);

public enum FormatoReporte { PDF, XLSX, JSON }

// Quién pide el reporte: se usa para verificar permisos ANTES de consultar los datos
public record SolicitanteReporte(Guid UsuarioId, string Rol)
{
    public bool Es(params string[] roles) => roles.Contains(Rol);
}

public class SinDatosException() : Exception("No hay datos para exportar");

// Un reporte disponible: clave en la URL, roles que pueden pedirlo y cómo se construye.
// Construir puede lanzar UnauthorizedAccessException si el solicitante no puede ver ESA actividad (p. ej. curso ajeno).
public record DefinicionReporte(
    string Clave,
    string Titulo,
    string[] Roles,
    Func<SolicitanteReporte, FiltrosReporte, Task<TablaReporte>> Construir);

// Cada módulo aporta sus reportes implementando esta interfaz (se registran en Program.cs)
public interface IProveedorReportes
{
    IEnumerable<DefinicionReporte> Reportes();
}
