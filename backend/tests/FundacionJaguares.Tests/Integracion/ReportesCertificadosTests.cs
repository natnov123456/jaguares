using System.Net;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Tests.Infraestructura;

namespace FundacionJaguares.Tests.Integracion;

// Evaluación: los certificados emitidos se exportan con filtros combinados (solo la Dirección)
[Collection(ColeccionApi.Nombre)]
public class ReportesCertificadosTests(ApiFixture api)
{
    [Fact]
    public async Task Certificados_por_texto_estado_y_fecha()
    {
        var (_, id, _) = await api.NuevoParticipanteAsync("Cert Exportable");
        var marca = "EXP" + Guid.NewGuid().ToString("N")[..6];
        await api.EnBdAsync(db =>
        {
            db.Certificados.AddRange(
                new Certificado { CodigoUnico = marca + "A", ParticipanteId = id, NombreParticipante = "Cert Exportable", NombreCurso = "Taller de Lectura",
                                  EmitidoEn = new DateTime(2025, 6, 10, 15, 0, 0, DateTimeKind.Utc) },
                new Certificado { CodigoUnico = marca + "B", ParticipanteId = id, NombreParticipante = "Cert Exportable", NombreCurso = "Taller de Pintura",
                                  EmitidoEn = new DateTime(2025, 7, 10, 15, 0, 0, DateTimeKind.Utc), Valido = false });
            return Task.CompletedTask;
        });
        var dir = await api.DirectoraAsync();
        var t = await (await dir.GetAsync($"/api/Reportes/certificados?formato=json&q={marca}&estado=VALIDO&desde=2025-06-01&hasta=2025-06-30")).JsonAsync();
        var fila = Assert.Single(t.GetProperty("filas").EnumerateArray());
        Assert.Equal(marca + "A", fila[0].GetString());
        Assert.Equal("Diseño estándar", fila[3].GetString());
        await (await dir.GetAsync($"/api/Reportes/certificados?formato=xlsx&q={marca}&estado=ANULADO")).EsperarAsync(HttpStatusCode.OK);
        await (await dir.GetAsync($"/api/Reportes/certificados?formato=pdf&q={marca}&desde=2026-01-01")).EsperarAsync(HttpStatusCode.UnprocessableEntity);
        await (await (await api.DocenteAsync()).GetAsync("/api/Reportes/certificados?formato=pdf")).EsperarAsync(HttpStatusCode.Forbidden);
    }
}
