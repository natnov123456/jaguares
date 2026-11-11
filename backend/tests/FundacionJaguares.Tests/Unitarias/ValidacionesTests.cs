using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Application.Services;

namespace FundacionJaguares.Tests.Unitarias;

// Reglas de validación puras (sin base de datos)
public class ValidacionesTests
{
    [Theory]
    [InlineData("ana@correo.com", true)]
    [InlineData("a.b-c@sub.dominio.bo", true)]
    [InlineData("sin-arroba", false)]
    [InlineData("ana@localhost", false)]
    [InlineData("ana@.com", false)]
    [InlineData("ana@correo.", false)]
    [InlineData("Ana <ana@correo.com>", false)]
    public void EmailValido(string email, bool esperado) =>
        Assert.Equal(esperado, ContactoService.EmailValido(email));

    [Fact]
    public void EmailValido_rechaza_mas_de_150_caracteres() =>
        Assert.False(ContactoService.EmailValido(new string('a', 145) + "@x.com"));

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=5", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/shorts/dQw4w9WgXcQ", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?feature=x&v=dQw4w9WgXcQ", "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://vimeo.com/76979871", "https://player.vimeo.com/video/76979871")]
    public void NormalizarVideo_acepta_youtube_y_vimeo(string url, string embed)
    {
        var (resultado, miniatura) = GaleriaService.NormalizarVideo(url);
        Assert.Equal(embed, resultado);
        if (url.Contains("youtu")) Assert.StartsWith("https://i.ytimg.com/vi/dQw4w9WgXcQ/", miniatura);
        else Assert.Null(miniatura);
    }

    [Theory]
    [InlineData("https://evil.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com.evil.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void NormalizarVideo_rechaza_otros_sitios(string? url) =>
        Assert.Throws<ArgumentException>(() => GaleriaService.NormalizarVideo(url));

    [Fact]
    public void Codigo_QR_es_aleatorio_y_de_22_caracteres_base64url()
    {
        var codigos = Enumerable.Range(0, 200).Select(_ => AsistenciaService.NuevoCodigo()).ToList();
        Assert.All(codigos, c => Assert.Matches("^[A-Za-z0-9_-]{22}$", c));
        Assert.Equal(codigos.Count, codigos.Distinct().Count());
    }

    [Theory]
    [InlineData("JAGUARES-EV:abc123", "abc123")]
    [InlineData("jaguares-ev: abc123 ", "abc123")]
    [InlineData("  abc123  ", "abc123")]
    [InlineData(null, "")]
    public void NormalizarCodigo_quita_prefijo_y_espacios(string? entrada, string esperado) =>
        Assert.Equal(esperado, AsistenciaService.NormalizarCodigo(entrada));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(200.5)]
    [InlineData(1.234)]
    public void ValidarHoras_rechaza_valores_invalidos(decimal horas) =>
        Assert.Throws<ArgumentException>(() => VoluntariadoService.ValidarHoras(horas));

    [Theory]
    [InlineData(0.5)]
    [InlineData(3.25)]
    [InlineData(200)]
    public void ValidarHoras_acepta_valores_validos(decimal horas) => VoluntariadoService.ValidarHoras(horas);

    [Fact]
    public void ValidarPreguntas_sin_preguntas_usa_las_por_defecto()
    {
        var lista = EncuestaService.ValidarPreguntas(null);
        Assert.Equal(EncuestaService.PreguntasPorDefecto.Count, lista.Count);
        Assert.Contains(lista, p => p.Tipo == "ESCALA");
    }

    public static TheoryData<List<PreguntaEncuestaDto>> PreguntasInvalidas => new()
    {
        new() { new("¿Te gustó?", "ESCALA"), new("¿Volverías?", "ESCALA") },                                  // menos de 3
        new() { new("A", "ESCALA"), new("B", "ESCALA"), new("C", "ESCALA"), new("D", "ESCALA"), new("E", "ESCALA"), new("F", "ESCALA") }, // más de 5
        new() { new("¿Te gustó?", "ESCALA"), new(" ", "ESCALA"), new("¿Volverías?", "ESCALA") },               // vacía
        new() { new("¿Te gustó?", "ESCALA"), new("¿Qué mejorarías?", "TEXTO"), new("Otra", "SI_NO") },        // tipo inválido
        new() { new("Uno", "TEXTO"), new("Dos", "TEXTO"), new("Tres", "TEXTO") },                             // sin puntaje
        new() { new("¿Te gustó?", "ESCALA"), new("¿te gustó?", "ESCALA"), new("Comentario", "TEXTO") },       // repetidas
        new() { new(new string('x', 201), "ESCALA"), new("B", "ESCALA"), new("C", "TEXTO") },                 // muy larga
    };

    [Theory]
    [MemberData(nameof(PreguntasInvalidas))]
    public void ValidarPreguntas_rechaza_encuestas_mal_formadas(List<PreguntaEncuestaDto> preguntas) =>
        Assert.Throws<ArgumentException>(() => EncuestaService.ValidarPreguntas(preguntas));
}
