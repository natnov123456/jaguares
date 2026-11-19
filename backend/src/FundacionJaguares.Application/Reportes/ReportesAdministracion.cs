using FundacionJaguares.Application.Services;

namespace FundacionJaguares.Application.Reportes;

// JAG-83 / FUJ-122: listado de cuentas exportable (solo la Dirección)
public class ReportesAdministracion(IUsuarioService usuarios) : IProveedorReportes
{
    public IEnumerable<DefinicionReporte> Reportes() =>
    [
        new("usuarios", "Usuarios y roles", ["DIRECTORA"], UsuariosAsync),
    ];

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
