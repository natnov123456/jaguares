using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// JAG-82 / FUJ-122: cuentas del personal, roles, contraseña asignada por la Directora (sin correos) y desactivación
[Collection(ColeccionApi.Nombre)]
public class UsuariosTests(ApiFixture api)
{
    private static string NuevoEmail() => $"staff{Guid.NewGuid().ToString("N")[..10]}@jaguares.org";

    private async Task<JsonElement> CrearAsync(string email, string rol, string nombre = "Persona del Personal", string? password = "Clave2026x") =>
        await (await (await api.DirectoraAsync()).PostJsonAsync("/api/Usuarios", new { nombreCompleto = nombre, email, rol, password })).JsonAsync();

    private async Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        await api.Anonimo().PostAsJsonAsync("/api/Auth/login", new { email, password });

    private HttpClient ConToken(string token)
    {
        var c = api.Anonimo();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private async Task<HttpClient> EntrarAsync(string email, string password = "Clave2026x")
    {
        var login = await (await LoginAsync(email, password)).JsonAsync();
        return ConToken(login.Str("token"));
    }

    [Fact]
    public async Task CP16_contador_no_entra_a_cursos_pero_si_a_donaciones()
    {
        var email = NuevoEmail();
        var u = await CrearAsync(email, "CONTADOR", "Carla Contadora");
        Assert.Equal("CONTADOR", u.Str("rol"));
        Assert.Empty(api.Correos.Para(email)); // no se envían correos

        var contador = await EntrarAsync(email);
        await (await contador.GetAsync("/api/Cursos/gestion")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await contador.GetAsync("/api/Usuarios")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await contador.GetAsync($"/api/Donaciones/reporte/{DateTime.UtcNow.Year}/{DateTime.UtcNow.Month}")).EsperarAsync(HttpStatusCode.OK);
        await (await contador.GetAsync("/api/Campanas/admin")).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Coordinador_de_voluntariado_gestiona_voluntarios_y_nada_mas()
    {
        var email = NuevoEmail();
        await CrearAsync(email, "COORDINADOR_VOLUNTARIADO");
        var coord = await EntrarAsync(email);
        await (await coord.GetAsync("/api/Voluntarios")).EsperarAsync(HttpStatusCode.OK);
        await (await coord.GetAsync("/api/Voluntarios/asignaciones")).EsperarAsync(HttpStatusCode.OK);
        await (await coord.GetAsync($"/api/Donaciones/reporte/{DateTime.UtcNow.Year}/{DateTime.UtcNow.Month}")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await coord.GetAsync("/api/Eventos/gestion")).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Directora_asigna_y_restablece_la_contrasena()
    {
        var email = NuevoEmail();
        var dir = await api.DirectoraAsync();

        // Contraseña inicial débil: se rechaza
        await (await dir.PostJsonAsync("/api/Usuarios", new { nombreCompleto = "Docente Nueva", email, rol = "DOCENTE", password = "corta1" }))
            .EsperarAsync(HttpStatusCode.BadRequest);

        // Sin contraseña (como en la importación) la cuenta existe pero no puede entrar hasta que se le asigne una
        var u = await CrearAsync(email, "DOCENTE", password: null);
        await (await LoginAsync(email, "Clave2026x")).EsperarAsync(HttpStatusCode.Unauthorized);

        var url = $"/api/Usuarios/{u.Id()}/contrasena";
        await (await dir.PutJsonAsync(url, new { password = "solamenteletras" })).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PutJsonAsync(url, new { password = "Clave2026x" })).EsperarAsync(HttpStatusCode.NoContent);
        await (await LoginAsync(email, "Clave2026x")).EsperarAsync(HttpStatusCode.OK);

        // Restablecer: la clave anterior deja de servir
        await (await dir.PutJsonAsync(url, new { password = "OtraClave99" })).EsperarAsync(HttpStatusCode.NoContent);
        await (await LoginAsync(email, "Clave2026x")).EsperarAsync(HttpStatusCode.Unauthorized);
        await (await LoginAsync(email, "OtraClave99")).EsperarAsync(HttpStatusCode.OK);

        await (await dir.PutJsonAsync($"/api/Usuarios/{Guid.NewGuid()}/contrasena", new { password = "Clave2026x" })).EsperarAsync(HttpStatusCode.NotFound);
        await (await (await api.DocenteAsync()).PutJsonAsync(url, new { password = "Clave2026x" })).EsperarAsync(HttpStatusCode.Forbidden);
        Assert.Empty(api.Correos.Para(email));
    }

    [Fact]
    public async Task Desactivar_o_cambiar_rol_invalida_la_sesion_abierta()
    {
        var email = NuevoEmail();
        var u = await CrearAsync(email, "ENCARGADO_EVENTOS");
        var encargado = await EntrarAsync(email);
        await (await encargado.GetAsync("/api/Eventos/gestion")).EsperarAsync(HttpStatusCode.OK);

        var dir = await api.DirectoraAsync();
        await (await dir.PutJsonAsync($"/api/Usuarios/{u.Id()}/rol", new { rol = "COORDINADOR_VOLUNTARIADO" })).EsperarAsync(HttpStatusCode.OK);
        await (await encargado.GetAsync("/api/Eventos/gestion")).EsperarAsync(HttpStatusCode.Unauthorized); // su token tenía el rol anterior

        var coord = ConToken((await (await LoginAsync(email, "Clave2026x")).JsonAsync()).Str("token"));
        await (await coord.GetAsync("/api/Voluntarios")).EsperarAsync(HttpStatusCode.OK);

        var desactivado = await (await dir.PutJsonAsync($"/api/Usuarios/{u.Id()}/activo", new { activo = false })).JsonAsync();
        Assert.False(desactivado.GetProperty("activo").GetBoolean());
        await (await coord.GetAsync("/api/Voluntarios")).EsperarAsync(HttpStatusCode.Unauthorized);
        await (await LoginAsync(email, "Clave2026x")).EsperarAsync(HttpStatusCode.Unauthorized);
        await (await dir.PutJsonAsync($"/api/Usuarios/{u.Id()}/contrasena", new { password = "Clave2026x" })).EsperarAsync(HttpStatusCode.BadRequest);

        await (await dir.PutJsonAsync($"/api/Usuarios/{u.Id()}/activo", new { activo = true })).EsperarAsync(HttpStatusCode.OK);
        await (await LoginAsync(email, "Clave2026x")).EsperarAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Validaciones_de_alta_y_protecciones_de_la_directora()
    {
        var dir = await api.DirectoraAsync();
        var email = NuevoEmail();
        await CrearAsync(email, "DOCENTE");
        await (await dir.PostJsonAsync("/api/Usuarios", new { nombreCompleto = "Repetido", email = email.ToUpperInvariant(), rol = "DOCENTE" }))
            .EsperarAsync(HttpStatusCode.Conflict);
        await (await dir.PostJsonAsync("/api/Usuarios", new { nombreCompleto = "Sin correo", email = "no-es-correo", rol = "DOCENTE" }))
            .EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostJsonAsync("/api/Usuarios", new { nombreCompleto = "Visitante", email = NuevoEmail(), rol = "VISITANTE" }))
            .EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PostJsonAsync("/api/Usuarios", new { nombreCompleto = "X", email = NuevoEmail(), rol = "DOCENTE" }))
            .EsperarAsync(HttpStatusCode.BadRequest);

        // La directora no se quita a sí misma el acceso
        var yo = await api.IdDeAsync(Usuarios.Directora);
        await (await dir.PutJsonAsync($"/api/Usuarios/{yo}/activo", new { activo = false })).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PutJsonAsync($"/api/Usuarios/{yo}/rol", new { rol = "DOCENTE" })).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PutJsonAsync($"/api/Usuarios/{Guid.NewGuid()}/activo", new { activo = false })).EsperarAsync(HttpStatusCode.NotFound);

        // Filtros combinados del listado
        var docentes = await (await dir.GetAsync("/api/Usuarios?rol=DOCENTE&activo=true")).JsonAsync();
        Assert.All(docentes.EnumerateArray(), u => { Assert.Equal("DOCENTE", u.Str("rol")); Assert.True(u.GetProperty("activo").GetBoolean()); });
        var porTexto = await (await dir.GetAsync($"/api/Usuarios?q={email[..10]}&rol=DOCENTE")).JsonAsync();
        Assert.Single(porTexto.EnumerateArray());
        var roles = await (await dir.GetAsync("/api/Usuarios/roles")).JsonAsync();
        Assert.Contains(roles.EnumerateArray(), r => r.Str("codigo") == "CONTADOR");

        await (await (await api.DocenteAsync()).GetAsync("/api/Usuarios")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await api.Anonimo().GetAsync("/api/Usuarios")).EsperarAsync(HttpStatusCode.Unauthorized);
    }
}
