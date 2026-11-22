using System.Text.Json;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Reportes;

// Reportes del área financiera (Contador y Directora)
public class ReportesFinanzas(IAuditoriaService auditoria, IDonacionService donaciones, AppDbContext db) : IProveedorReportes
{
    private static readonly string[] Finanzas = ["DIRECTORA", "CONTADOR"];

    public IEnumerable<DefinicionReporte> Reportes() =>
    [
        new("bitacora-donaciones", "Bitácora de auditoría de donaciones", Finanzas, BitacoraAsync),
        // El encargado de operaciones exporta solo las donaciones que él registró
        new("donaciones", "Donaciones", ["DIRECTORA", "CONTADOR", "ENCARGADO_EVENTOS"], DonacionesAsync),
        new("campanas", "Campañas de donación", Finanzas, CampanasAsync),
    ];

    private async Task<TablaReporte> DonacionesAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        var soloPropias = s.Rol == "ENCARGADO_EVENTOS";
        var q = from d in db.Donaciones
                join dn in db.Donantes on d.DonanteId equals (Guid?)dn.Id into dns
                from dn in dns.DefaultIfEmpty()
                join c in db.Campanas on d.CampanaId equals (Guid?)c.Id into cs
                from c in cs.DefaultIfEmpty()
                select new
                {
                    d.NumeroComprobante, d.Fecha, d.Monto, d.Medio, d.Referencia, d.Estado, d.RegistradoPor,
                    Donante = dn == null || dn.EsAnonimo ? null : dn.Nombre, Campana = c == null ? null : c.Nombre
                };
        if (soloPropias) q = q.Where(x => x.RegistradoPor == s.UsuarioId);
        var lista = await q.OrderByDescending(x => x.Fecha).Take(10000).ToListAsync();
        // accion = medio de pago (EFECTIVO, TRANSFERENCIA, QR, PASARELA)
        var filtrados = FiltroCombinado.Aplicar(lista, f,
                texto: x => [x.NumeroComprobante.ToString("D6"), x.Referencia, x.Donante, x.Campana], estado: x => x.Estado, fecha: x => x.Fecha)
            .Where(x => string.IsNullOrWhiteSpace(f.Accion) || string.Equals(x.Medio.ToString(), f.Accion, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var confirmadas = filtrados.Where(x => x.Estado == "CONFIRMADA").ToList();
        return new TablaReporte("Donaciones", soloPropias ? "Donaciones que registraste" : "Detalle de donaciones recibidas",
            [new("N°", 0.8f), new("Fecha", 1.3f), new("Donante", 1.8f), new("Medio", 1f), new("Referencia", 1.3f),
             new("Campaña", 1.6f), new("Estado", 1f), new("Monto", 1.1f, true)],
            filtrados.Select(x => new[]
            {
                x.NumeroComprobante.ToString("D6"), Fmt.FechaHora(x.Fecha), x.Donante ?? "Anónimo", x.Medio.ToString(),
                x.Referencia ?? "—", x.Campana ?? "General", Fmt.Estado(x.Estado), Fmt.Bs(x.Monto)
            }).ToList(),
            FiltroCombinado.Describir(f with { Accion = f.Accion is null ? null : "Medio " + f.Accion }, null, Fmt.Estado),
            [new("Donaciones confirmadas", Fmt.Entero(confirmadas.Count)), new("Total confirmado", Fmt.Bs(confirmadas.Sum(x => x.Monto))),
             new("Anuladas (no suman)", Fmt.Entero(filtrados.Count(x => x.Estado == "ANULADA")))]);
    }

    private async Task<TablaReporte> CampanasAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        var lista = await donaciones.ListarCampanasAsync(soloVigentes: false);
        // estado = VIGENTE | CERRADA ; fechas = fecha límite
        var filtrados = FiltroCombinado.Aplicar(lista, f,
            texto: c => [c.Nombre, c.Descripcion], estado: c => c.Vigente ? "VIGENTE" : "CERRADA",
            fecha: c => c.FechaLimite.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc)).ToList();
        return new TablaReporte("Campañas de donación", "Meta, recaudación y avance de cada campaña",
            [new("Campaña", 2.4f), new("Fecha límite", 1f), new("Estado", 0.9f), new("Donaciones", 0.9f, true),
             new("Meta", 1.1f, true), new("Recaudado", 1.1f, true), new("Avance", 0.8f, true)],
            filtrados.Select(c => new[]
            {
                c.Nombre, Fmt.Fecha(c.FechaLimite), c.Vigente ? "Vigente" : "Cerrada", Fmt.Entero(c.CantidadDonaciones),
                Fmt.Bs(c.Meta), Fmt.Bs(c.Recaudado), Fmt.Pct((double)c.Porcentaje)
            }).ToList(),
            FiltroCombinado.Describir(f, null, e => e == "VIGENTE" ? "Vigentes" : "Cerradas"),
            [new("Total recaudado", Fmt.Bs(filtrados.Sum(c => c.Recaudado)))]);
    }

    public static string Accion(string a) => a switch { "ALTA" => "Alta", "EDICION" => "Edición", "ANULACION" => "Anulación", "VERIFICACION" => "Verificación del pago", "RECHAZO" => "Rechazo del pago", _ => a };

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
