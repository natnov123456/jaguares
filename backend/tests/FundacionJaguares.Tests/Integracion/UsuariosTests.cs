using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// JAG-82 / FUJ-122: cuentas del personal, roles, enlace para definir la contraseña y desactivación
[Collection(ColeccionApi.Nombre)]
public class UsuariosTests(ApiFixture api)
{
    private static string NuevoEmail() => $"staff{Guid.NewGuid().ToString("N")[..10]}@jaguares.org";

    private async Task<JsonElement> CrearAsync(string email, string rol, string nombre = "Persona del Personal") =>
        await (await (await api.DirectoraAsync()).PostJsonAsync("/api/Usuarios", new { nombreCompleto = nombre, email, rol })).JsonAsync();

    // El token llega solo en el correo
    private string TokenDelCorreo(string email) =>
        Regex.Match(api.Correos.Para(email).Last().Texto, @"token=([A-Za-z0-9_-]+)").Groups[1].Value;

    private async Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        await api.Anonimo().PostAsJsonAsync("/api/Auth/login", new { email, password });

    private HttpClient ConToken(string token)
    {
        var c = api.Anonimo();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private async Task<HttpClient> ActivarYEntrarAsync(string email, string password = "Clave2026x")
    {
        await (await api.Anonimo().PostJsonAsync("/api/Auth/definir-contrasena", new { token = TokenDelCorreo(email), password }))
            .EsperarAsync(HttpStatusCode.NoContent);
        var login = await (await LoginAsync(email, password)).JsonAsync();
        return ConToken(login.Str("token"));
    }

    [Fact]
    public async Task CP16_contador_no_entra_a_cursos_pero_si_a_donaciones()
    {
        var email = NuevoEmail();
        var u = await CrearAsync(email, "CONTADOR", "Carla Contadora");
        Assert.Equal("CONTADOR", u.Str("rol"));
        var correo = Assert.Single(api.Correos.Para(email));
        Assert.Contains("definir-contrasena?token=", correo.Texto);
        Assert.Contains("Contador/a", correo.Texto);

        // Antes de definir la contraseña no puede entrar con ninguna clave
        await (await LoginAsync(email, "Clave2026x")).EsperarAsync(HttpStatusCode.Unauthorized);

        var contador = await ActivarYEntrarAsync(email);
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
        var coord = await ActivarYEntrarAsync(email);
        await (await coord.GetAsync("/api/Voluntarios")).EsperarAsync(HttpStatusCode.OK);
        await (await coord.GetAsync("/api/Voluntarios/asignaciones")).EsperarAsync(HttpStatusCode.OK);
        await (await coord.GetAsync($"/api/Donaciones/reporte/{DateTime.UtcNow.Year}/{DateTime.UtcNow.Month}")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await coord.GetAsync("/api/Eventos/gestion")).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Enlace_es_de_un_solo_uso_vence_y_valida_la_contrasena()
    {
        var email = NuevoEmail();
        await CrearAsync(email, "DOCENTE");
        var token = TokenDelCorreo(email);
        var url = "/api/Auth/definir-contrasena";

        await (await api.Anonimo().PostJsonAsync(url, new { token, password = "corta1" })).EsperarAsync(HttpStatusCode.BadRequest);
        await (await api.Anonimo().PostJsonAsync(url, new { token, password = "solamenteletras" })).EsperarAsync(HttpStatusCode.BadRequest);
        await (await api.Anonimo().PostJsonAsync(url, new { token = "inventado", password = "Clave2026x" })).EsperarAsync(HttpStatusCode.BadRequest);
        await (await api.Anonimo().PostJsonAsync(url, new { token, password = "Clave2026x" })).EsperarAsync(HttpStatusCode.NoContent);
        await (await api.Anonimo().PostJsonAsync(url, new { token, password = "OtraClave99" })).EsperarAsync(HttpStatusCode.BadRequest); // ya usado
        await (await LoginAsync(email, "Clave2026x")).EsperarAsync(HttpStatusCode.OK);

        // Reenviar invalida el enlace anterior; un enlace vencido no sirve
        var id = await api.IdDeAsync(email);
        var dir = await api.DirectoraAsync();
        await (await dir.PostAsync($"/api/Usuarios/{id}/reenviar-enlace", null)).EsperarAsync(HttpStatusCode.NoContent);
        var primero = TokenDelCorreo(email);
        await (await dir.PostAsync($"/api/Usuarios/{id}/reenviar-enlace", null)).EsperarAsync(HttpStatusCode.NoContent);
        var segundo = TokenDelCorreo(email);
        await (await api.Anonimo().PostJsonAsync(url, new { token = primero, password = "Clave2027x" })).EsperarAsync(HttpStatusCode.BadRequest);
        await api.EnBdAsync(async db =>
        {
            foreach (var t in db.TokensContrasena.Where(t => t.UsuarioId == id)) t.ExpiraEn = DateTime.UtcNow.AddMinutes(-1);
            await Task.CompletedTask;
        });
        await (await api.Anonimo().PostJsonAsync(url, new { token = segundo, password = "Clave2027x" })).EsperarAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Desactivar_o_cambiar_rol_invalida_la_sesion_abierta()
    {
        var email = NuevoEmail();
        var u = await CrearAsync(email, "ENCARGADO_EVENTOS");
        var encargado = await ActivarYEntrarAsync(email);
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
        await (await dir.PostAsync($"/api/Usuarios/{u.Id()}/reenviar-enlace", null)).EsperarAsync(HttpStatusCode.BadRequest);

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
