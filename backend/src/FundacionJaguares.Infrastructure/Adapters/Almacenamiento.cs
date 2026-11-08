using System.Text.RegularExpressions;

namespace FundacionJaguares.Infrastructure.Adapters;

// Almacenamiento de archivos subidos (FUJ-126). La implementación local guarda en disco y la API
// los sirve como archivos estáticos bajo PrefijoUrl; se puede cambiar por un almacenamiento en la nube
// implementando esta misma interfaz.
public interface IAlmacenamientoArchivos
{
    Task<string> GuardarAsync(string carpeta, string nombreArchivo, Stream contenido);
    void Eliminar(string? urlPublica);
    Task<byte[]?> LeerAsync(string? urlPublica);
}

public partial class AlmacenamientoLocal : IAlmacenamientoArchivos
{
    private readonly string _raiz;
    private readonly string _prefijoUrl;

    public AlmacenamientoLocal(string rutaRaiz, string prefijoUrl)
    {
        _raiz = Path.GetFullPath(rutaRaiz);
        _prefijoUrl = prefijoUrl.TrimEnd('/');
        Directory.CreateDirectory(_raiz);
    }

    public string Raiz => _raiz;

    // Solo letras, números, guion, guion bajo y punto: evita rutas como "../" en nombres o carpetas
    [GeneratedRegex(@"^[A-Za-z0-9_\-\.]+$")]
    private static partial Regex SegmentoSeguro();

    public async Task<string> GuardarAsync(string carpeta, string nombreArchivo, Stream contenido)
    {
        var segmentos = carpeta.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segmentos.Length == 0 || segmentos.Any(s => !SegmentoSeguro().IsMatch(s) || s.StartsWith('.'))
            || !SegmentoSeguro().IsMatch(nombreArchivo) || nombreArchivo.StartsWith('.'))
            throw new ArgumentException("Nombre de archivo o carpeta no permitido");

        var dir = Path.Combine([_raiz, .. segmentos]);
        Directory.CreateDirectory(dir);
        var ruta = Path.Combine(dir, nombreArchivo);
        await using (var fs = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write))
            await contenido.CopyToAsync(fs);
        return $"{_prefijoUrl}/{string.Join('/', segmentos)}/{nombreArchivo}";
    }

    public void Eliminar(string? urlPublica)
    {
        var ruta = RutaLocal(urlPublica);
        if (ruta is null) return;
        try { if (File.Exists(ruta)) File.Delete(ruta); }
        catch (IOException) { /* el archivo puede estar en uso; queda huérfano pero no rompe la operación */ }
    }

    public async Task<byte[]?> LeerAsync(string? urlPublica)
    {
        var ruta = RutaLocal(urlPublica);
        return ruta is not null && File.Exists(ruta) ? await File.ReadAllBytesAsync(ruta) : null;
    }

    // Null si la URL no corresponde a un archivo dentro de la carpeta de subidas (nunca se accede fuera de ella)
    private string? RutaLocal(string? urlPublica)
    {
        if (string.IsNullOrEmpty(urlPublica) || !urlPublica.StartsWith(_prefijoUrl + "/")) return null;
        var relativa = urlPublica[(_prefijoUrl.Length + 1)..].Replace('/', Path.DirectorySeparatorChar);
        var ruta = Path.GetFullPath(Path.Combine(_raiz, relativa));
        return ruta.StartsWith(_raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? ruta : null;
    }
}
