namespace FundacionJaguares.Tests.Infraestructura;

// Pruebas de regresión por "snapshot": se compara el resultado con un archivo guardado en tests/.../Snapshots.
// Si un cambio en la plantilla es intencional, se regeneran con:  ACTUALIZAR_SNAPSHOTS=1 dotnet test
public static class Snapshot
{
    private static string Carpeta()
    {
        // Sube desde la carpeta de compilación hasta encontrar el .csproj del proyecto de pruebas
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            foreach (var proyecto in new[] { dir.FullName, Path.Combine(dir.FullName, "backend", "tests", "FundacionJaguares.Tests") })
                if (File.Exists(Path.Combine(proyecto, "FundacionJaguares.Tests.csproj"))) return Path.Combine(proyecto, "Snapshots");
        }
        throw new DirectoryNotFoundException("No se encontró el proyecto de pruebas");
    }

    public static void Verificar(string nombre, string actual)
    {
        var carpeta = Carpeta();
        var archivo = Path.Combine(carpeta, nombre + ".txt");
        actual = actual.Replace("\r\n", "\n");
        if (Environment.GetEnvironmentVariable("ACTUALIZAR_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(carpeta);
            File.WriteAllText(archivo, actual);
            return;
        }
        Assert.True(File.Exists(archivo), $"Falta el snapshot {archivo}. Generalo con ACTUALIZAR_SNAPSHOTS=1.");
        var esperado = File.ReadAllText(archivo).Replace("\r\n", "\n");
        Assert.Equal(esperado, actual);
    }
}
