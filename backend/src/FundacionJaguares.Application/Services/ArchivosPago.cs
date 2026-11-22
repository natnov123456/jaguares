namespace FundacionJaguares.Application.Services;

// Comprobante de pago que sube el donante (foto del celular o PDF del banco).
// Se comprueba la firma real del archivo; las fotos se normalizan (orientación, tamaño máximo y sin metadatos
// como la ubicación GPS) para que pesen poco en la base de datos.
public static class ArchivosPago
{
    public const int LadoMaximo = 1800;

    public static async Task<(byte[] Contenido, string Tipo, string Nombre)> PrepararAsync(ArchivoSubido? archivo)
    {
        if (archivo is null || archivo.Tamano == 0)
            throw new ArgumentException("Sube la foto o el PDF del comprobante de tu banco");
        if (archivo.Tamano > ProcesadorImagen.MaxBytes)
            throw new ArgumentException("El comprobante no puede pesar más de 5 MB");
        var nombre = Path.GetFileName(archivo.Nombre ?? "comprobante");
        if (nombre.Length > 150) nombre = nombre[^150..];
        var ext = Path.GetExtension(nombre).ToLowerInvariant();

        if (ext == ".pdf")
        {
            using var ms = new MemoryStream();
            await ProcesadorImagen.CopiarConLimiteAsync(archivo.Contenido, ms, ProcesadorImagen.MaxBytes);
            var datos = ms.ToArray();
            if (datos.Length < 5 || datos[0] != '%' || datos[1] != 'P' || datos[2] != 'D' || datos[3] != 'F')
                throw new ArgumentException("El archivo no es un PDF válido");
            return (datos, "application/pdf", nombre);
        }
        if (ext is not (".jpg" or ".jpeg" or ".png"))
            throw new ArgumentException("Formato no permitido: sube una foto (JPG o PNG) o un PDF");

        var (bytes, esPng) = await ProcesadorImagen.LeerValidadoAsync(archivo);
        var (imagen, extension) = ProcesadorImagen.Normalizar(bytes, esPng, LadoMaximo);
        return (imagen, esPng ? "image/png" : "image/jpeg", Path.ChangeExtension(nombre, extension));
    }
}
