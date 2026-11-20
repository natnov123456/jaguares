using System.Net;
using System.Net.Http.Json;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FundacionJaguares.Tests.Integracion;

// Términos y condiciones / Política de privacidad (dos casillas en cada formulario, con constancia de la
// aceptación) y registro de actividad (quién entró, cuándo y qué hizo; de solo lectura).
[Collection(ColeccionApi.Nombre)]
public class LegalYRegistroTests(ApiFixture api)
{
    private static string Correo(string prefijo) => $"{prefijo}{Guid.NewGuid().ToString("N")[..10]}@prueba.bo";

    private async Task<T> ConBdAsync<T>(Func<AppDbContext, Task<T>> consulta)
    {
        using var scope = api.Factory.Services.CreateScope();
        return await consulta(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    // El registro de actividad se guarda en segundo plano: se espera hasta 10 s a que aparezca
    private async Task<List<Domain.Entities.RegistroActividad>> EsperarRegistrosAsync(Func<Domain.Entities.RegistroActividad, bool> condicion, int minimo = 1)
    {
        for (var i = 0; i < 50; i++)
        {
            var filas = (await ConBdAsync(db => db.RegistroActividad.AsNoTracking().OrderByDescending(r => r.Id).Take(2000).ToListAsync()))
                .Where(condicion).ToList();
            if (filas.Count >= minimo) return filas;
            await Task.Delay(200);
        }
        throw new Xunit.Sdk.XunitException("No apareció el registro de actividad esperado");
    }

    [Fact]
    public async Task Cada_formulario_exige_las_dos_casillas_y_guarda_cuando_y_como_se_acepto()
    {
        var anon = api.Anonimo();
        anon.DefaultRequestHeaders.UserAgent.ParseAdd("NavegadorDePrueba/1.0");

        // Registro: sin las dos casillas no se crea la cuenta
        var email = Correo("legal");
        var registro = new { nombreCompleto = "Persona Legal", email, password = "Clave123!", telefono = (string?)null };
        var r1 = await (await anon.PostAsJsonAsync("/api/Auth/registro", registro)).JsonAsync(HttpStatusCode.BadRequest);
        Assert.Contains("Términos", r1.Str("error"));
        var r2 = await (await anon.PostAsJsonAsync("/api/Auth/registro", new { registro.nombreCompleto, email, registro.password, aceptaTerminos = true }))
            .JsonAsync(HttpStatusCode.BadRequest);
        Assert.Contains("privacidad", r2.Str("error"));
        Assert.False(await ConBdAsync(db => db.Usuarios.AnyAsync(u => u.Email == email)));
        await (await anon.PostAsJsonAsync("/api/Auth/registro",
            new { registro.nombreCompleto, email, registro.password, aceptaTerminos = true, aceptaPrivacidad = true })).EsperarAsync(HttpStatusCode.OK);

        var usuarioId = await ConBdAsync(db => db.Usuarios.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        var aceptados = await ConBdAsync(db => db.Consentimientos.Where(c => c.Email == email).ToListAsync());
        Assert.Equal(["PRIVACIDAD", "TERMINOS"], aceptados.Select(c => c.Documento).Order());
        Assert.All(aceptados, c =>
        {
            Assert.Equal(("REGISTRO", DocumentosLegales.Version, usuarioId), (c.Formulario, c.Version, c.UsuarioId!.Value));
            Assert.Equal("NavegadorDePrueba/1.0", c.Navegador);
            Assert.True(DateTime.UtcNow - c.AceptadoEn < TimeSpan.FromMinutes(1));
        });

        // Voluntariado, donación en línea y contacto: lo mismo
        var vol = Correo("vol");
        await (await anon.PostAsJsonAsync("/api/Voluntarios/postular", new { nombreCompleto = "Vol Legal", email = vol, telefono = (string?)null }))
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.False(await ConBdAsync(db => db.Voluntarios.AnyAsync(v => v.Email == vol)));
        await (await anon.PostAsJsonAsync("/api/Voluntarios/postular",
            new { nombreCompleto = "Vol Legal", email = vol, telefono = (string?)null, aceptaTerminos = true, aceptaPrivacidad = true })).EsperarAsync(HttpStatusCode.OK);

        var donante = Correo("don");
        var donacion = new { monto = 20, medio = "QR", referencia = (string?)null, donanteId = (Guid?)null, donanteNombre = "Donante Legal", donanteEmail = donante };
        await (await anon.PostAsJsonAsync("/api/Donaciones/en-linea", donacion)).EsperarAsync(HttpStatusCode.BadRequest);
        await (await anon.PostAsJsonAsync("/api/Donaciones/en-linea", new
        {
            donacion.monto, donacion.medio, donacion.referencia, donacion.donanteId, donacion.donanteNombre, donacion.donanteEmail,
            aceptaTerminos = true, aceptaPrivacidad = true
        })).EsperarAsync(HttpStatusCode.OK);

        var contacto = Correo("con");
        var sinCasillas = await (await anon.PostAsJsonAsync("/api/Contacto",
            new { nombre = "Ana", email = contacto, asunto = (string?)null, mensaje = "Quiero información.", sitioWeb = "" })).JsonAsync(HttpStatusCode.BadRequest);
        Assert.True(sinCasillas.GetProperty("errores").TryGetProperty("aceptaTerminos", out _));
        Assert.True(sinCasillas.GetProperty("errores").TryGetProperty("aceptaPrivacidad", out _));
        await (await anon.PostAsJsonAsync("/api/Contacto", new
        {
            nombre = "Ana", email = contacto, asunto = (string?)null, mensaje = "Quiero información.", sitioWeb = "", aceptaTerminos = true, aceptaPrivacidad = true
        })).EsperarAsync(HttpStatusCode.OK);

        foreach (var (correo, formulario) in new[] { (vol, "VOLUNTARIADO"), (donante, "DONACION"), (contacto, "CONTACTO") })
            Assert.Equal(2, await ConBdAsync(db => db.Consentimientos.CountAsync(c => c.Email == correo && c.Formulario == formulario)));

        // La constancia no se puede modificar ni borrar
        await Assert.ThrowsAnyAsync<Exception>(() => ConBdAsync(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE consentimientos SET version = 'otra' WHERE email = {0}", email)));
        await Assert.ThrowsAnyAsync<Exception>(() => ConBdAsync(db => db.Database.ExecuteSqlRawAsync(
            "DELETE FROM consentimientos WHERE email = {0}", email)));
    }

    [Fact]
    public async Task El_registro_de_actividad_guarda_quien_entro_cuando_y_que_hizo()
    {
        var (_, id, email) = await api.NuevoParticipanteAsync("Persona Registrada");
        var anon = api.Anonimo();

        // Intento fallido y luego correcto
        await (await anon.PostAsJsonAsync("/api/Auth/login", new { email, password = "equivocada1" })).EsperarAsync(HttpStatusCode.Unauthorized);
        var login = await (await anon.PostAsJsonAsync("/api/Auth/login", new { email, password = "Clave123!" })).JsonAsync();
        var cliente = api.Anonimo();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", login.Str("token"));
        await (await cliente.GetAsync("/api/Cursos/mis-inscripciones")).EsperarAsync(HttpStatusCode.OK);
        await (await cliente.GetAsync("/api/Usuarios")).EsperarAsync(HttpStatusCode.Forbidden); // acceso denegado también queda
        await (await cliente.PostAsync("/api/Auth/salir", null)).EsperarAsync(HttpStatusCode.NoContent);
        // Los enlaces con token nunca se guardan tal cual
        await (await anon.GetAsync("/api/Encuestas/responder/token-secreto-de-prueba")).EsperarAsync(HttpStatusCode.NotFound);

        var propios = await EsperarRegistrosAsync(r => r.Email == email, 6);
        var fallido = Assert.Single(propios, r => r.Accion == "Inicio de sesión fallido");
        Assert.Equal((401, false), (fallido.Estado, fallido.Exito));
        var correcto = propios.First(r => r.Accion == "Inicio de sesión"); // el más reciente
        Assert.Equal((id, "PARTICIPANTE", true), (correcto.UsuarioId!.Value, correcto.Rol, correcto.Exito));
        Assert.Contains(propios, r => r.Ruta == "/api/Cursos/mis-inscripciones" && r.UsuarioId == id && r.Accion == "Consulta de sus inscripciones a cursos");
        Assert.Contains(propios, r => r.Ruta == "/api/Usuarios" && r.Estado == 403 && !r.Exito);
        Assert.Contains(propios, r => r.Accion == "Cierre de sesión" && r.UsuarioId == id);
        Assert.All(propios, r => Assert.True(r.DuracionMs >= 0));

        var encuesta = (await EsperarRegistrosAsync(r => r.Ruta.StartsWith("/api/Encuestas/responder/"))).First();
        Assert.Equal("/api/Encuestas/responder/{token}", encuesta.Ruta);
        Assert.Equal("token=***", encuesta.Recurso);
        Assert.DoesNotContain(await ConBdAsync(db => db.RegistroActividad.Select(r => r.Recurso ?? "").ToListAsync()),
            r => r.Contains("token-secreto-de-prueba") || r.Contains("Clave123!"));

        // Solo la Dirección lo consulta, con filtros y paginación
        var dir = await api.DirectoraAsync();
        var pagina = await (await dir.GetAsync($"/api/RegistroActividad?q={Uri.EscapeDataString(email)}&tamano=3")).JsonAsync();
        Assert.True(pagina.GetProperty("total").GetInt32() >= 6);
        Assert.Equal(3, pagina.GetProperty("registros").GetArrayLength());
        Assert.Equal("Persona Registrada", pagina.GetProperty("registros")[0].Str("usuario"));
        var errores = await (await dir.GetAsync($"/api/RegistroActividad?q={Uri.EscapeDataString(email)}&resultado=ERROR")).JsonAsync();
        Assert.All(errores.GetProperty("registros").EnumerateArray(), r => Assert.False(r.GetProperty("exito").GetBoolean()));
        await (await dir.GetAsync("/api/RegistroActividad?desde=2026-05-10&hasta=2026-05-01")).EsperarAsync(HttpStatusCode.BadRequest);
        await (await (await api.DocenteAsync()).GetAsync("/api/RegistroActividad")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await api.Anonimo().GetAsync("/api/RegistroActividad")).EsperarAsync(HttpStatusCode.Unauthorized);

        // Exportable a PDF/Excel y de solo lectura
        await (await dir.GetAsync("/api/Reportes/registro-actividad?formato=xlsx")).EsperarAsync(HttpStatusCode.OK);
        await Assert.ThrowsAnyAsync<Exception>(() => ConBdAsync(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE registro_actividad SET accion = 'x' WHERE email = {0}", email)));
        await Assert.ThrowsAnyAsync<Exception>(() => ConBdAsync(db => db.Database.ExecuteSqlRawAsync("TRUNCATE registro_actividad")));
    }
}
