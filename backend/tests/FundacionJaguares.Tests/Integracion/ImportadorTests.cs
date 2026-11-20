using FundacionJaguares.Application.Importacion;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FundacionJaguares.Tests.Integracion;

// JAG-76: carga de datos iniciales — normalización, duplicados, errores por línea, idempotencia y conteos origen vs. destino
[Collection(ColeccionApi.Nombre)]
public class ImportadorTests(ApiFixture api)
{
    private static string Carpeta(string marca)
    {
        var dir = Path.Combine(Path.GetTempPath(), "jaguares-importacion", marca);
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, "cuentas_internas.csv"),
        [
            "nombre;correo;rol;telefono",
            $"ana  maría   pérez;Docente.{marca}@Fundacion.org;Docente;7 123-4567",
            $"Carlos Ruiz;contador.{marca}@fundacion.org;Contador;59170000001",
            $"Repetida;DOCENTE.{marca}@fundacion.org;Docente;",     // duplicado (mismo correo)
            $"Sin rol;sinrol.{marca}@fundacion.org;Jardinero;",      // rol no reconocido
        ]);
        File.WriteAllLines(Path.Combine(dir, "voluntarios.csv"),
        [
            "nombre,correo,telefono,estado,fecha_postulacion",        // separado por comas
            $"luis gómez,luis.{marca}@correo.com,70011223,activo,2026-03-15",
            $"\"Rosa, la del mercado\",rosa.{marca}@correo.com,,INACTIVO,01/02/2026",
            $"Mal correo,no-es-correo,70000000,ACTIVO,01/02/2026",
        ]);
        File.WriteAllLines(Path.Combine(dir, "cursos.csv"),
        [
            "titulo;descripcion;fecha_inicio;fecha_fin;cupo;asistencia_minima;correo_docente",
            $"Taller {marca};Reciclaje;01/12/2026;20/12/2026;25;75%;docente.{marca}@fundacion.org",
            $"Fechas al revés {marca};;20/12/2026;01/12/2026;25;80;",
            $"Sin docente válido {marca};;01/12/2026;20/12/2026;10;80;nadie.{marca}@fundacion.org",
        ]);
        File.WriteAllLines(Path.Combine(dir, "donaciones.csv"),
        [
            "fecha;monto;medio;referencia;donante;correo_donante",
            $"10/10/2025;1.500,50;TRANSFERENCIA;REC-{marca};empresa amiga srl;donante.{marca}@correo.com",
            $"11/10/2025;200;efectivo;REC2-{marca};;",
            $"11/10/2025;-5;EFECTIVO;MAL-{marca};X;",
        ]);
        File.WriteAllLines(Path.Combine(dir, "noticias.csv"),
        [
            "titulo;resumen;contenido;imagen_url;fecha_publicacion;publicada",
            $"Noticia {marca};Resumen;Contenido de la noticia;;05/11/2026;si",
            $"Borrador {marca};;Texto;;;no",
        ]);
        return dir;
    }

    [Fact]
    public async Task Verificar_no_escribe_y_aplicar_importa_normalizado_y_es_idempotente()
    {
        var marca = Guid.NewGuid().ToString("N")[..6];
        var dir = Carpeta(marca);
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var importador = new ImportadorDatos(db, scope.ServiceProvider.GetRequiredService<IUsuarioService>());

        // 1) Verificación: informa, pero no escribe nada
        var verificacion = await importador.ImportarAsync(dir, aplicar: false);
        Assert.False(verificacion.Aplicado);
        Assert.Equal(0, await db.Usuarios.CountAsync(u => u.Email.Contains(marca)));
        var cuentas = verificacion.Archivos.Single(a => a.Archivo == "cuentas_internas.csv");
        Assert.Equal((4, 2, 1), (cuentas.FilasOrigen, cuentas.Validas, cuentas.DuplicadasEnArchivo));
        Assert.Contains(cuentas.Errores, e => e.StartsWith("línea 5") && e.Contains("rol no reconocido"));

        // 2) Importación
        var r = await importador.ImportarAsync(dir, aplicar: true);
        var porArchivo = r.Archivos.ToDictionary(a => a.Archivo);
        Assert.Equal(2, porArchivo["cuentas_internas.csv"].Importadas);
        Assert.True(porArchivo["cuentas_internas.csv"].ConteoCoincide);
        var docente = await db.Usuarios.SingleAsync(u => u.Email == $"docente.{marca}@fundacion.org");
        Assert.Equal("Ana María Pérez", docente.NombreCompleto);
        Assert.Equal("+591 71234567", docente.Telefono);
        Assert.Single(api.Correos.Para(docente.Email), c => c.Texto.Contains("definir-contrasena?token="));
        Assert.Equal("+591 70000001", (await db.Usuarios.SingleAsync(u => u.Email == $"contador.{marca}@fundacion.org")).Telefono);

        Assert.Equal(2, porArchivo["voluntarios.csv"].Importadas);
        Assert.Contains(porArchivo["voluntarios.csv"].Errores, e => e.Contains("correo no válido"));
        Assert.Equal("INACTIVO", (await db.Voluntarios.SingleAsync(v => v.Email == $"rosa.{marca}@correo.com")).Estado);
        Assert.Equal("Rosa, La Del Mercado", (await db.Voluntarios.SingleAsync(v => v.Email == $"rosa.{marca}@correo.com")).NombreCompleto);

        var curso = await db.Cursos.SingleAsync(c => c.Titulo == $"Taller {marca}");
        Assert.Equal((docente.Id, 75, "PUBLICADO"), (curso.DocenteId!.Value, curso.AsistenciaMinimaPct, curso.Estado));
        Assert.Contains(porArchivo["cursos.csv"].Errores, e => e.Contains("fecha de fin es anterior"));
        Assert.Contains(porArchivo["cursos.csv"].Errores, e => e.Contains("no tiene cuenta con rol Docente"));
        Assert.False(porArchivo["cursos.csv"].ConteoCoincide); // 2 válidas en el archivo, 1 en la base: se informa

        var donacion = await db.Donaciones.SingleAsync(d => d.Referencia == $"IMP:REC-{marca}");
        Assert.Equal(1500.50m, donacion.Monto);
        Assert.Single(await db.AuditoriaDonaciones.Where(a => a.DonacionId == donacion.Id).ToListAsync());
        Assert.Contains(porArchivo["donaciones.csv"].Errores, e => e.Contains("monto no válido"));
        Assert.True(porArchivo["donaciones.csv"].ConteoCoincide);

        Assert.Null((await db.Noticias.SingleAsync(n => n.Titulo == $"Borrador {marca}")).PublicadoEn);
        Assert.True((await db.Noticias.SingleAsync(n => n.Titulo == $"Noticia {marca}")).Publicada);

        // 3) Volver a ejecutar no duplica nada
        var otraVez = await importador.ImportarAsync(dir, aplicar: true);
        Assert.All(otraVez.Archivos, a => Assert.Equal(0, a.Importadas));
        Assert.Equal(1, await db.Donaciones.CountAsync(d => d.Referencia == $"IMP:REC-{marca}"));
        Assert.Equal(1, await db.Usuarios.CountAsync(u => u.Email == $"docente.{marca}@fundacion.org"));

        // Ningún dato de donantes queda expuesto en el sitio público (transparencia solo publica totales)
        var publico = await (await api.Anonimo().GetAsync("/api/Transparencia")).Content.ReadAsStringAsync();
        Assert.DoesNotContain($"donante.{marca}", publico);
        Directory.Delete(dir, true);
    }

    [Theory]
    [InlineData("70011223", "+591 70011223")]
    [InlineData("+591 7001-1223", "+591 70011223")]
    [InlineData("2 2412345", "+591 22412345")]
    [InlineData("", null)]
    public void Telefono_se_normaliza(string entrada, string? esperado) => Assert.Equal(esperado, Normalizador.Telefono(entrada));

    [Theory]
    [InlineData("1.234,50", 1234.50)]
    [InlineData("1234.5", 1234.5)]
    [InlineData("Bs. 200", 200)]
    public void Monto_acepta_formato_boliviano(string entrada, decimal esperado) => Assert.Equal(esperado, Normalizador.Monto(entrada));

    [Fact]
    public void Datos_invalidos_se_rechazan()
    {
        Assert.Throws<FormatException>(() => Normalizador.Telefono("123"));
        Assert.Throws<FormatException>(() => Normalizador.Monto("0"));
        Assert.Throws<FormatException>(() => Normalizador.Fecha("31/02/2026"));
        Assert.Throws<FormatException>(() => Normalizador.Correo("sin-arroba"));
        Assert.Equal(new DateOnly(2026, 3, 5), Normalizador.Fecha("5/3/2026"));
        Assert.Equal("Juan Pérez", Normalizador.Nombre("  JUAN   pérez "));
    }
}
