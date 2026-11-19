using FundacionJaguares.Application.Services;
using FundacionJaguares.Domain.Entities;

namespace FundacionJaguares.Tests.Unitarias;

// JAG-82 / FUJ-122: reglas de contraseñas, roles y tokens (sin base de datos)
public class UsuariosUnitTests
{
    [Theory]
    [InlineData("Clave2026")]
    [InlineData("abcdefg1")]
    public void ValidarContrasena_acepta_8_o_mas_con_letras_y_numeros(string password) =>
        UsuarioService.ValidarContrasena(password);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc12")]
    [InlineData("solamenteletras")]
    [InlineData("1234567890")]
    public void ValidarContrasena_rechaza_debiles(string? password) =>
        Assert.Throws<ArgumentException>(() => UsuarioService.ValidarContrasena(password));

    [Fact]
    public void Solo_se_asignan_roles_de_cuenta()
    {
        foreach (var rol in UsuarioService.RolesAsignables) UsuarioService.ValidarRolAsignable(rol);
        Assert.Throws<ArgumentException>(() => UsuarioService.ValidarRolAsignable(Rol.VISITANTE));
        Assert.Contains(Rol.CONTADOR, UsuarioService.RolesAsignables);
        Assert.Contains(Rol.COORDINADOR_VOLUNTARIADO, UsuarioService.RolesAsignables);
    }

    [Fact]
    public void Token_es_aleatorio_y_solo_se_guarda_su_hash()
    {
        var a = UsuarioService.NuevoToken();
        var b = UsuarioService.NuevoToken();
        Assert.NotEqual(a, b);
        Assert.Equal(43, a.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", a);
        Assert.Equal(64, UsuarioService.Hash(a).Length);
        Assert.Equal(UsuarioService.Hash(a), UsuarioService.Hash(a));
        Assert.NotEqual(a, UsuarioService.Hash(a));
    }

    [Fact]
    public void Enlace_vence_en_24_horas() => Assert.Equal(TimeSpan.FromHours(24), UsuarioService.VigenciaEnlace);
}
