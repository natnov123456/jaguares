using System.IdentityModel.Tokens.Jwt;
using System.Net;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

[Collection(ColeccionApi.Nombre)]
public class AuthTests(ApiFixture api)
{
    [Fact]
    public async Task Login_con_credenciales_validas_devuelve_jwt_con_rol()
    {
        var r = await api.Anonimo().PostJsonAsync("/api/Auth/login", new { email = Usuarios.Directora, password = "Admin123!" });
        var json = await r.JsonAsync();

        Assert.Equal("DIRECTORA", json.Str("rol"));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(json.Str("token"));
        // RNF-02: el token dura como máximo 60 minutos
        Assert.True(jwt.ValidTo <= DateTime.UtcNow.AddMinutes(61));
    }

    [Theory]
    [InlineData(Usuarios.Directora, "incorrecta")]
    [InlineData("nadie@jaguares.org", "Admin123!")]
    public async Task Login_invalido_devuelve_401(string email, string password)
    {
        var r = await api.Anonimo().PostJsonAsync("/api/Auth/login", new { email, password });
        await r.EsperarAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Registro_crea_participante_y_no_permite_email_repetido()
    {
        var email = $"r{Guid.NewGuid().ToString("N")[..12]}@prueba.bo";
        var datos = new { nombreCompleto = "Nueva Persona", email, password = "Clave123!", telefono = "70000000" };

        var r = await api.Anonimo().PostJsonAsync("/api/Auth/registro", datos);
        var json = await r.JsonAsync();
        Assert.Equal("PARTICIPANTE", json.Str("rol"));

        var repetido = await api.Anonimo().PostJsonAsync("/api/Auth/registro", datos);
        Assert.False(repetido.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Endpoint_protegido_sin_token_devuelve_401_y_con_otro_rol_403()
    {
        await (await api.Anonimo().GetAsync("/api/Cursos/gestion")).EsperarAsync(HttpStatusCode.Unauthorized);
        var juan = await api.JuanAsync();
        await (await juan.GetAsync("/api/Cursos/gestion")).EsperarAsync(HttpStatusCode.Forbidden);
    }
}
