using FundacionJaguares.Application.Services;
using FundacionJaguares.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Reportes;

// JAG-83 / FUJ-122: listado de cuentas exportable (solo la Dirección)
public class ReportesAdministracion(IUsuarioService usuarios, AppDbContext db) : IProveedorReportes
{
    public IEnumerable<DefinicionReporte> Reportes() =>
    [
        new("usuarios", "Usuarios y roles", ["DIRECTORA"], UsuariosAsync),
        // Evaluación: pantalla de certificados (plantillas) con exportación de los certificados emitidos
        new("certificados", "Certificados emitidos", ["DIRECTORA"], CertificadosAsync),
    ];

    private async Task<TablaReporte> CertificadosAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        var lista = await (from c in db.Certificados
                           join p in db.PlantillasCertificado on c.PlantillaId equals (Guid?)p.Id into ps
                           from p in ps.DefaultIfEmpty()
                           orderby c.EmitidoEn descending
                           select new { c.CodigoUnico, c.NombreParticipante, c.NombreCurso, c.EmitidoEn, c.Valido, Plantilla = p == null ? null : p.Nombre })
                          .Take(10000).ToListAsync();
        // estado = VALIDO | ANULADO ; fechas = emisión
        var filtrados = FiltroCombinado.Aplicar(lista, f,
            texto: c => [c.CodigoUnico, c.NombreParticipante, c.NombreCurso], estado: c => c.Valido ? "VALIDO" : "ANULADO",
            fecha: c => c.EmitidoEn).ToList();
        return new TablaReporte("Certificados emitidos", "Certificados verificables con su código único",
            [new("Código", 1.2f), new("Participante", 2f), new("Actividad", 2.4f), new("Plantilla", 1.4f), new("Emitido el", 1f), new("Estado", 0.8f)],
            filtrados.Select(c => new[]
            {
                c.CodigoUnico, c.NombreParticipante, c.NombreCurso, c.Plantilla ?? "Diseño estándar", Fmt.Fecha(c.EmitidoEn),
                c.Valido ? "Válido" : "Anulado"
            }).ToList(),
            FiltroCombinado.Describir(f, null, e => e == "VALIDO" ? "Válidos" : "Anulados"),
            [new("Certificados", Fmt.Entero(filtrados.Count))]);
    }

    private async Task<TablaReporte> UsuariosAsync(SolicitanteReporte s, FiltrosReporte f)
    {
        var roles = (await usuarios.RolesAsync()).ToDictionary(r => r.Codigo, r => r.Nombre);
        var lista = await usuarios.ListarAsync(null, null, null);
        // estado = ACTIVO | INACTIVO ; rol = código del rol ; desde/hasta = fecha de creación de la cuenta
        var filtrados = FiltroCombinado.Aplicar(lista, f,
                texto: u => [u.NombreCompleto, u.Email, u.Telefono], estado: u => u.Activo ? "ACTIVO" : "INACTIVO", fecha: u => u.CreadoEn)
            .Where(u => string.IsNullOrWhiteSpace(f.Rol) || string.Equals(u.Rol, f.Rol, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return new TablaReporte("Usuarios y roles", "Cuentas de la plataforma con su rol y estado",
            [new("Nombre", 2f), new("Correo", 2.4f), new("Teléfono", 1f), new("Rol", 1.6f), new("Estado", 0.9f), new("Creada el", 1f)],
            filtrados.Select(u => new[]
            {
                u.NombreCompleto, u.Email, u.Telefono ?? "—", roles.GetValueOrDefault(u.Rol, u.Rol), u.Activo ? "Activa" : "Desactivada",
                Fmt.Fecha(u.CreadoEn)
            }).ToList(),
            FiltroCombinado.Describir(f with { Rol = f.Rol is null ? null : roles.GetValueOrDefault(f.Rol, f.Rol) }, null,
                e => e == "ACTIVO" ? "Activas" : "Desactivadas"),
            [new("Cuentas", Fmt.Entero(filtrados.Count)), new("Activas", Fmt.Entero(filtrados.Count(u => u.Activo)))]);
    }
}
