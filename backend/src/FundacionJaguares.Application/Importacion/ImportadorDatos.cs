using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Importacion;

// JAG-76: carga de los datos iniciales de la Fundación desde planillas CSV (exportadas del Excel actual).
// Archivos (todos opcionales) en una carpeta; plantillas en docs/datos-iniciales/plantillas:
//   cuentas_internas.csv  nombre;correo;rol;telefono                         → cuentas del personal (reciben el enlace para su contraseña)
//   voluntarios.csv       nombre;correo;telefono;estado;fecha_postulacion
//   cursos.csv            titulo;descripcion;fecha_inicio;fecha_fin;cupo;asistencia_minima;correo_docente
//   donaciones.csv        fecha;monto;medio;referencia;donante;correo_donante  → historial de donantes frecuentes
//   noticias.csv          titulo;resumen;contenido;imagen_url;fecha_publicacion;publicada
// Reglas:
//   - Normaliza nombres, correos, teléfonos (+591), fechas (dd/mm/aaaa o aaaa-mm-dd) y montos (1.234,50 o 1234.50).
//   - Quita duplicados dentro del archivo y no vuelve a cargar lo que ya existe: se puede ejecutar varias veces.
//   - Las filas con errores se informan con su número de línea y no detienen el resto.
//   - Modo verificación (aplicar = false): informa todo pero no escribe nada.
//   - Al final compara los conteos: filas válidas del archivo = registros en la base.
public record ResultadoArchivo(string Archivo, int FilasOrigen, int Validas, int DuplicadasEnArchivo, int YaExistian,
                               int Importadas, List<string> Errores, int EnBaseDespues, bool ConteoCoincide);
public record ResultadoImportacion(bool Aplicado, List<ResultadoArchivo> Archivos)
{
    public bool Exito => Archivos.All(a => a.Errores.Count == 0 && a.ConteoCoincide);
}

public static class Normalizador
{
    private static readonly TextInfo Texto = new CultureInfo("es-BO").TextInfo;

    public static string Nombre(string? s)
    {
        var limpio = Regex.Replace((s ?? "").Trim(), @"\s+", " ");
        return limpio.Length == 0 ? "" : Texto.ToTitleCase(limpio.ToLowerInvariant());
    }

    public static string? Correo(string? s)
    {
        var c = (s ?? "").Trim().ToLowerInvariant();
        return c.Length == 0 ? null : ContactoService.EmailValido(c) ? c : throw new FormatException($"correo no válido: '{s}'");
    }

    // Bolivia: celulares de 8 dígitos → "+591 7xxxxxxx". Acepta espacios, guiones y el prefijo 591.
    public static string? Telefono(string? s)
    {
        var digitos = Regex.Replace(s ?? "", @"\D", "");
        if (digitos.Length == 0) return null;
        if (digitos.StartsWith("591") && digitos.Length == 11) digitos = digitos[3..];
        if (digitos.Length is < 7 or > 8) throw new FormatException($"teléfono no válido: '{s}'");
        return "+591 " + digitos;
    }

    public static DateOnly Fecha(string? s)
    {
        var t = (s ?? "").Trim();
        string[] formatos = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy"];
        return DateOnly.TryParseExact(t, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
            ? f : throw new FormatException($"fecha no válida: '{s}'");
    }

    public static decimal Monto(string? s)
    {
        var t = (s ?? "").Trim().Replace("Bs.", "").Replace("Bs", "").Replace(" ", "");
        // "1.234,50" (formato boliviano) o "1234.50"
        if (t.Contains(',')) t = t.Replace(".", "").Replace(',', '.');
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var m) && m > 0
            ? Math.Round(m, 2) : throw new FormatException($"monto no válido: '{s}'");
    }

    public static bool SiNo(string? s) => (s ?? "").Trim().ToLowerInvariant() is "si" or "sí" or "s" or "1" or "true" or "x" or "publicada";

    // CSV separado por ";" o "," (lo que use la primera línea), con comillas dobles. Devuelve (línea, campos por encabezado).
    public static List<(int Linea, Dictionary<string, string> Campos)> LeerCsv(string ruta)
    {
        var lineas = File.ReadAllLines(ruta, Encoding.UTF8).ToList();
        if (lineas.Count == 0) return [];
        var sep = lineas[0].Count(c => c == ';') >= lineas[0].Count(c => c == ',') ? ';' : ',';
        var encabezado = Partir(lineas[0].TrimStart('﻿'), sep).Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var filas = new List<(int, Dictionary<string, string>)>();
        for (var i = 1; i < lineas.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lineas[i])) continue;
            var valores = Partir(lineas[i], sep);
            filas.Add((i + 1, encabezado.Select((h, j) => (h, v: j < valores.Count ? valores[j].Trim() : ""))
                .ToDictionary(x => x.h, x => x.v)));
        }
        return filas;
    }

    private static List<string> Partir(string linea, char sep)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        var comillas = false;
        for (var i = 0; i < linea.Length; i++)
        {
            var c = linea[i];
            if (c == '"') { if (comillas && i + 1 < linea.Length && linea[i + 1] == '"') { actual.Append('"'); i++; } else comillas = !comillas; }
            else if (c == sep && !comillas) { campos.Add(actual.ToString()); actual.Clear(); }
            else actual.Append(c);
        }
        campos.Add(actual.ToString());
        return campos;
    }
}

public class ImportadorDatos(AppDbContext db, IUsuarioService usuarios)
{
    private static readonly Dictionary<string, Rol> RolesInternos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["directora"] = Rol.DIRECTORA, ["direccion"] = Rol.DIRECTORA, ["dirección"] = Rol.DIRECTORA,
        ["contador"] = Rol.CONTADOR, ["contadora"] = Rol.CONTADOR,
        ["encargado de operaciones"] = Rol.ENCARGADO_EVENTOS, ["encargada de operaciones"] = Rol.ENCARGADO_EVENTOS, ["encargado_eventos"] = Rol.ENCARGADO_EVENTOS,
        ["coordinador de voluntariado"] = Rol.COORDINADOR_VOLUNTARIADO, ["coordinadora de voluntariado"] = Rol.COORDINADOR_VOLUNTARIADO,
        ["coordinador_voluntariado"] = Rol.COORDINADOR_VOLUNTARIADO,
        ["docente"] = Rol.DOCENTE,
    };

    public async Task<ResultadoImportacion> ImportarAsync(string carpeta, bool aplicar)
    {
        if (!Directory.Exists(carpeta)) throw new DirectoryNotFoundException($"No existe la carpeta {carpeta}");
        var resultados = new List<ResultadoArchivo>();
        // Orden: primero las cuentas (los cursos necesitan a su docente)
        async Task Procesar(string archivo, Func<List<(int, Dictionary<string, string>)>, bool, Task<ResultadoArchivo>> f)
        {
            var ruta = Path.Combine(carpeta, archivo);
            if (File.Exists(ruta)) resultados.Add(await f(Normalizador.LeerCsv(ruta), aplicar));
        }
        await Procesar("cuentas_internas.csv", CuentasAsync);
        await Procesar("voluntarios.csv", VoluntariosAsync);
        await Procesar("cursos.csv", CursosAsync);
        await Procesar("donaciones.csv", DonacionesAsync);
        await Procesar("noticias.csv", NoticiasAsync);
        return new ResultadoImportacion(aplicar, resultados);
    }

    // Plantilla común: normaliza cada fila, quita duplicados por clave, salta las existentes e importa
    private async Task<ResultadoArchivo> ProcesarAsync<T>(string archivo, List<(int Linea, Dictionary<string, string> Campos)> filas, bool aplicar,
        Func<Dictionary<string, string>, T> normalizar, Func<T, string> clave, Func<List<string>, Task<HashSet<string>>> existentes,
        Func<T, Task> guardar, Func<Task<int>> contarDestino)
    {
        var errores = new List<string>();
        var validas = new List<T>();
        var claves = new HashSet<string>();
        var duplicadas = 0;
        foreach (var (linea, campos) in filas)
        {
            try
            {
                var item = normalizar(campos);
                if (!claves.Add(clave(item))) { duplicadas++; continue; }
                validas.Add(item);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or KeyNotFoundException)
            {
                errores.Add($"línea {linea}: {ex.Message}");
            }
        }
        var yaEstan = await existentes(claves.ToList());
        var nuevas = validas.Where(v => !yaEstan.Contains(clave(v))).ToList();
        var importadas = 0;
        if (aplicar)
            foreach (var n in nuevas)
            {
                try { await guardar(n); importadas++; }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
                {
                    errores.Add($"{clave(n)}: {ex.Message}");
                    db.ChangeTracker.Clear();
                }
            }
        // Verificación origen vs. destino: cada fila válida del archivo debe existir en la base
        var enBase = aplicar ? (await existentes(claves.ToList())).Count : yaEstan.Count;
        var coincide = !aplicar || enBase == validas.Count;
        return new ResultadoArchivo(archivo, filas.Count, validas.Count, duplicadas, yaEstan.Count, importadas, errores,
            await contarDestino(), coincide);
    }

    private static string Campo(Dictionary<string, string> c, string nombre, bool obligatorio = true)
    {
        var v = c.GetValueOrDefault(nombre)?.Trim() ?? "";
        if (obligatorio && v.Length == 0) throw new FormatException($"falta '{nombre}'");
        return v;
    }

    private Task<ResultadoArchivo> CuentasAsync(List<(int, Dictionary<string, string>)> filas, bool aplicar) =>
        ProcesarAsync("cuentas_internas.csv", filas, aplicar,
            c => (Nombre: Normalizador.Nombre(Campo(c, "nombre")), Correo: Normalizador.Correo(Campo(c, "correo"))!,
                  Rol: RolesInternos.TryGetValue(Campo(c, "rol"), out var r) ? r : throw new FormatException($"rol no reconocido: '{c["rol"]}'"),
                  Telefono: Normalizador.Telefono(Campo(c, "telefono", false))),
            x => x.Correo,
            async correos => (await db.Usuarios.Where(u => correos.Contains(u.Email.ToLower())).Select(u => u.Email.ToLower()).ToListAsync()).ToHashSet(),
            async x => await usuarios.CrearAsync(new CrearUsuarioDto(x.Nombre, x.Correo, x.Rol, x.Telefono), Guid.Empty),
            () => db.Usuarios.CountAsync(u => u.Rol != Rol.PARTICIPANTE && u.Rol != Rol.VOLUNTARIO && u.Rol != Rol.VISITANTE));

    private Task<ResultadoArchivo> VoluntariosAsync(List<(int, Dictionary<string, string>)> filas, bool aplicar) =>
        ProcesarAsync("voluntarios.csv", filas, aplicar,
            c => new Voluntario
            {
                NombreCompleto = Normalizador.Nombre(Campo(c, "nombre")), Email = Normalizador.Correo(Campo(c, "correo"))!,
                Telefono = Normalizador.Telefono(Campo(c, "telefono", false)),
                Estado = Campo(c, "estado", false).ToUpperInvariant() is "INACTIVO" ? "INACTIVO" : "ACTIVO",
                PostuladoEn = Campo(c, "fecha_postulacion", false) is { Length: > 0 } f
                    ? Normalizador.Fecha(f).ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc) : DateTime.UtcNow
            },
            v => v.Email,
            async correos => (await db.Voluntarios.Where(v => correos.Contains(v.Email.ToLower())).Select(v => v.Email.ToLower()).ToListAsync()).ToHashSet(),
            async v =>
            {
                v.UsuarioId = await db.Usuarios.Where(u => u.Email.ToLower() == v.Email).Select(u => (Guid?)u.Id).FirstOrDefaultAsync();
                db.Voluntarios.Add(v);
                await db.SaveChangesAsync();
            },
            () => db.Voluntarios.CountAsync());

    private Task<ResultadoArchivo> CursosAsync(List<(int, Dictionary<string, string>)> filas, bool aplicar) =>
        ProcesarAsync("cursos.csv", filas, aplicar,
            c =>
            {
                var inicio = Normalizador.Fecha(Campo(c, "fecha_inicio"));
                var fin = Normalizador.Fecha(Campo(c, "fecha_fin"));
                if (fin < inicio) throw new FormatException("la fecha de fin es anterior a la de inicio");
                var cupo = int.TryParse(Campo(c, "cupo"), out var n) && n > 0 ? n : throw new FormatException($"cupo no válido: '{c["cupo"]}'");
                var minimo = Campo(c, "asistencia_minima", false) is { Length: > 0 } m
                    ? (int.TryParse(m.TrimEnd('%', ' '), out var p) && p is >= 0 and <= 100 ? p : throw new FormatException($"asistencia mínima no válida: '{m}'"))
                    : 80;
                return (Titulo: Regex.Replace(Campo(c, "titulo"), @"\s+", " "), Descripcion: Campo(c, "descripcion", false),
                        Inicio: inicio, Fin: fin, Cupo: cupo, Minimo: minimo, Docente: Normalizador.Correo(Campo(c, "correo_docente", false)));
            },
            x => $"{x.Titulo.ToLowerInvariant()}|{x.Inicio:yyyy-MM-dd}",
            async claves =>
            {
                var existentes = await db.Cursos.Select(c => new { c.Titulo, c.FechaInicio }).ToListAsync();
                return existentes.Select(c => $"{c.Titulo.ToLowerInvariant()}|{c.FechaInicio:yyyy-MM-dd}").Where(claves.Contains).ToHashSet();
            },
            async x =>
            {
                Guid? docente = null;
                if (x.Docente is not null)
                    docente = await db.Usuarios.Where(u => u.Email.ToLower() == x.Docente && u.Rol == Rol.DOCENTE).Select(u => (Guid?)u.Id).FirstOrDefaultAsync()
                              ?? throw new ArgumentException($"el docente {x.Docente} no tiene cuenta con rol Docente (cárgalo en cuentas_internas.csv)");
                var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4));
                db.Cursos.Add(new Curso
                {
                    Titulo = x.Titulo, Descripcion = x.Descripcion.Length == 0 ? null : x.Descripcion, DocenteId = docente,
                    FechaInicio = x.Inicio, FechaFin = x.Fin, CupoMaximo = x.Cupo, CupoDisponible = x.Cupo, AsistenciaMinimaPct = x.Minimo,
                    Estado = x.Fin < hoy ? "FINALIZADO" : x.Inicio <= hoy ? "EN_CURSO" : "PUBLICADO"
                });
                await db.SaveChangesAsync();
            },
            () => db.Cursos.CountAsync());

    private Task<ResultadoArchivo> DonacionesAsync(List<(int, Dictionary<string, string>)> filas, bool aplicar) =>
        ProcesarAsync("donaciones.csv", filas, aplicar,
            c =>
            {
                var medioTexto = Campo(c, "medio", false).ToUpperInvariant();
                var medio = medioTexto.Length == 0 ? MedioPago.EFECTIVO
                    : Enum.TryParse<MedioPago>(medioTexto, out var md) ? md : throw new FormatException($"medio de pago no válido: '{medioTexto}'");
                return (Fecha: Normalizador.Fecha(Campo(c, "fecha")), Monto: Normalizador.Monto(Campo(c, "monto")), Medio: medio,
                        Referencia: Campo(c, "referencia", false), Donante: Normalizador.Nombre(Campo(c, "donante", false)),
                        Correo: Normalizador.Correo(Campo(c, "correo_donante", false)));
            },
            x => $"{x.Fecha:yyyy-MM-dd}|{x.Monto.ToString("0.00", CultureInfo.InvariantCulture)}|{x.Referencia.ToLowerInvariant()}|{x.Donante.ToLowerInvariant()}",
            async claves =>
            {
                var importadas = await (from d in db.Donaciones
                                        join dn in db.Donantes on d.DonanteId equals (Guid?)dn.Id into dns
                                        from dn in dns.DefaultIfEmpty()
                                        where d.Referencia != null && d.Referencia.StartsWith("IMP:")
                                        select new { d.Fecha, d.Monto, d.Referencia, Donante = dn == null ? "" : dn.Nombre }).ToListAsync();
                return importadas.Select(d => $"{FechaLocal(d.Fecha):yyyy-MM-dd}|{d.Monto.ToString("0.00", CultureInfo.InvariantCulture)}|{d.Referencia![4..].ToLowerInvariant()}|{d.Donante.ToLowerInvariant()}")
                    .Where(claves.Contains).ToHashSet();
            },
            async x =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                Donante? donante = null;
                if (x.Donante.Length > 0)
                {
                    donante = new Donante { Nombre = x.Donante, Email = x.Correo };
                    db.Donantes.Add(donante);
                    await db.SaveChangesAsync();
                }
                var d = new Donacion
                {
                    Monto = x.Monto, Medio = x.Medio, Referencia = "IMP:" + x.Referencia, DonanteId = donante?.Id, Estado = "CONFIRMADA",
                    Fecha = x.Fecha.ToDateTime(new TimeOnly(16, 0), DateTimeKind.Utc), // 12:00 de Bolivia
                    UsuarioId = x.Correo is null ? null
                        : await db.Usuarios.Where(u => u.Email.ToLower() == x.Correo).Select(u => (Guid?)u.Id).FirstOrDefaultAsync()
                };
                db.Donaciones.Add(d);
                await db.SaveChangesAsync();
                // También queda en la bitácora de auditoría (FUJ-125)
                AuditoriaService.Registrar(db, d, AuditoriaDonacion.Alta, new ActorAuditoria(null, "Importación de datos iniciales", null),
                    null, AuditoriaService.Instantanea(d, donante?.Nombre, null), "Carga inicial desde planilla");
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            },
            () => db.Donaciones.CountAsync(d => d.Referencia != null && d.Referencia.StartsWith("IMP:")));

    private static DateOnly FechaLocal(DateTime utc) => DateOnly.FromDateTime(utc.AddHours(-4));

    private Task<ResultadoArchivo> NoticiasAsync(List<(int, Dictionary<string, string>)> filas, bool aplicar) =>
        ProcesarAsync("noticias.csv", filas, aplicar,
            c => new Noticia
            {
                Titulo = Regex.Replace(Campo(c, "titulo"), @"\s+", " "), Resumen = Campo(c, "resumen", false) is { Length: > 0 } r ? r : null,
                Contenido = Campo(c, "contenido"), ImagenUrl = Campo(c, "imagen_url", false) is { Length: > 0 } u ? u : null,
                Publicada = !c.ContainsKey("publicada") || Normalizador.SiNo(c["publicada"]),
                PublicadoEn = Campo(c, "fecha_publicacion", false) is { Length: > 0 } f
                    ? Normalizador.Fecha(f).ToDateTime(new TimeOnly(16, 0), DateTimeKind.Utc) : DateTime.UtcNow
            },
            n => n.Titulo.ToLowerInvariant(),
            async titulos => (await db.Noticias.Select(n => n.Titulo.ToLower()).ToListAsync()).Where(titulos.Contains).ToHashSet(),
            async n =>
            {
                if (!n.Publicada) n.PublicadoEn = null;
                db.Noticias.Add(n);
                await db.SaveChangesAsync();
            },
            () => db.Noticias.CountAsync());
}
