using System.Text.Json;
using FundacionJaguares.Application.Services;

namespace FundacionJaguares.Application.Reportes;

// Reportes del área financiera (Contador y Directora)
public class ReportesFinanzas(IAuditoriaService auditoria) : IProveedorReportes
{
    private static readonly string[] Finanzas = ["DIRECTORA", "CONTADOR"];

    public IEnumerable<DefinicionReporte> Reportes() =>
    [
        new("bitacora-donaciones", "Bitácora de auditoría de donaciones", Finanzas, BitacoraAsync),
    ];

    public static string Accion(string a) => a switch { "ALTA" => "Alta", "EDICION" => "Edición", "ANULACION" => "Anulación", _ => a };

    // Resumen legible del JSON guardado: "monto: 100 → 150; medio: EFECTIVO → QR"
    public static string Cambios(string? anterior, string nuevo)
    {
        using var n = JsonDocument.Parse(nuevo);
        if (anterior is null)
            return string.Join("; ", n.RootElement.EnumerateObject().Where(p => p.Name is "monto" or "medio" or "referencia" or "campana")
                .Select(p => $"{p.Name}: {Valor(p.Value)}"));
        using var a = JsonDocument.Parse(anterior);
        var cambios = n.RootElement.EnumerateObject()
            .Where(p => a.RootElement.TryGetProperty(p.Name, out var v) && Valor(v) != Valor(p.Value))
            .Select(p => $"{p.Name}: {Valor(a.RootElement.GetProperty(p.Name))} → {Valor(p.Value)}");
        return string.Join("; ", cambios);
    }

    private static string Valor(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Null => "—",
        JsonValueKind.String => v.GetString() ?? "—",
        _ => v.ToString()
    };

    private async Task<TablaReporte> BitacoraAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        // Mismos filtros que la pantalla: rango de fechas, usuario, acción y texto; hasta 5.000 registros por archivo
        var pagina = await auditoria.ConsultarAsync(new FiltroAuditoria(f.Desde, f.Hasta, f.UsuarioId, f.Accion, f.Q, 1, 100));
        var registros = new List<RegistroAuditoriaDto>(pagina.Registros);
        for (var p = 2; registros.Count < pagina.Total && p <= 50; p++)
            registros.AddRange((await auditoria.ConsultarAsync(new FiltroAuditoria(f.Desde, f.Hasta, f.UsuarioId, f.Accion, f.Q, p, 100))).Registros);

        var filtros = FiltroCombinado.Describir(f with { Accion = f.Accion is null ? null : Accion(f.Accion.ToUpperInvariant()) });
        if (f.UsuarioId is not null && registros.FirstOrDefault() is { } r) filtros.Add(new("Usuario", r.UsuarioNombre));
        return new TablaReporte("Bitácora de auditoría de donaciones",
            "Altas, ediciones y anulaciones de donaciones con su valor anterior y nuevo (registro de solo lectura)",
            [new("Fecha y hora", 1.3f), new("Comprobante", 0.9f), new("Acción", 0.9f), new("Usuario", 1.6f), new("Rol", 1.1f),
             new("Cambios", 3.6f), new("Motivo", 1.8f)],
            registros.Select(x => new[]
            {
                Fmt.FechaHora(x.CreadoEn), "N° " + x.NumeroComprobante.ToString("D6"), Accion(x.Accion), x.UsuarioNombre,
                x.Rol ?? "—", Cambios(x.ValorAnterior, x.ValorNuevo), x.Motivo ?? "—"
            }).ToList(),
            filtros,
            [new("Registros", Fmt.Entero(registros.Count))]);
    }
}
