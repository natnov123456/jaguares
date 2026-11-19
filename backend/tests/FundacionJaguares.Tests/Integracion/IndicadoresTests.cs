using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FundacionJaguares.Tests.Integracion;

// JAG-84 / FUJ-123: CP-17 — los 6 indicadores de un mes coinciden con la base de datos y cargan en ≤ 2 s
[Collection(ColeccionApi.Nombre)]
public class IndicadoresTests(ApiFixture api)
{
    private static DateTime Utc(int y, int m, int d, int h = 15) => new(y, m, d, h, 0, 0, DateTimeKind.Utc);

    // Febrero de 2024: ningún otro test usa ese mes
    private async Task SembrarFebrero2024Async()
    {
        var (_, ana, _) = await api.NuevoParticipanteAsync("Ana Indicadores");
        var (_, beto, _) = await api.NuevoParticipanteAsync("Beto Indicadores");
        var docente = await api.DocenteIdAsync();
        var curso = new Curso { Titulo = "Curso indicadores 2024", DocenteId = docente, FechaInicio = new(2024, 2, 5), FechaFin = new(2024, 2, 25),
                                CupoMaximo = 10, CupoDisponible = 8, Estado = "FINALIZADO" };
        var ev = new Evento { Titulo = "Evento indicadores 2024", FechaInicio = Utc(2024, 2, 10), FechaFin = Utc(2024, 2, 10, 18), Estado = "FINALIZADO" };
        var vol = new Voluntario { NombreCompleto = "Vol indicadores", Email = $"vi{Guid.NewGuid():N}@correo.com", PostuladoEn = Utc(2024, 1, 10) };
        await api.EnBdAsync(db => { db.Cursos.Add(curso); db.Eventos.Add(ev); db.Voluntarios.Add(vol); return Task.CompletedTask; });

        var encuesta = new Encuesta { CursoId = curso.Id, CreadaEn = Utc(2024, 2, 26) };
        await api.EnBdAsync(db =>
        {
            db.Inscripciones.AddRange(
                new Inscripcion { CursoId = curso.Id, ParticipanteId = ana, FechaInscripcion = Utc(2024, 2, 3) },
                new Inscripcion { CursoId = curso.Id, ParticipanteId = beto, FechaInscripcion = Utc(2024, 2, 3) });
            db.InscripcionesEvento.AddRange(
                new InscripcionEvento { EventoId = ev.Id, ParticipanteId = ana, Estado = "ASISTIO", CodigoQr = Guid.NewGuid().ToString("N"), InscritoEn = Utc(2024, 2, 4), CheckinEn = Utc(2024, 2, 10) },
                new InscripcionEvento { EventoId = ev.Id, ParticipanteId = beto, Estado = "CONFIRMADA", CodigoQr = Guid.NewGuid().ToString("N"), InscritoEn = Utc(2024, 2, 4) });
            db.AsistenciasCurso.AddRange( // 3 presentes de 4 registros
                new AsistenciaCurso { CursoId = curso.Id, ParticipanteId = ana, Fecha = new(2024, 2, 6), Presente = true },
                new AsistenciaCurso { CursoId = curso.Id, ParticipanteId = ana, Fecha = new(2024, 2, 7), Presente = true },
                new AsistenciaCurso { CursoId = curso.Id, ParticipanteId = beto, Fecha = new(2024, 2, 6), Presente = true },
                new AsistenciaCurso { CursoId = curso.Id, ParticipanteId = beto, Fecha = new(2024, 2, 7), Presente = false });
            db.Certificados.Add(new Certificado { CodigoUnico = "IND-" + Guid.NewGuid().ToString("N")[..8], CursoId = curso.Id, ParticipanteId = ana,
                                                  NombreParticipante = "Ana", NombreCurso = curso.Titulo, EmitidoEn = Utc(2024, 2, 26) });
            db.Donaciones.AddRange(
                new Donacion { Monto = 100, Medio = MedioPago.EFECTIVO, Fecha = Utc(2024, 2, 15) },
                new Donacion { Monto = 50, Medio = MedioPago.QR, Fecha = Utc(2024, 2, 15) },
                new Donacion { Monto = 30, Medio = MedioPago.EFECTIVO, Fecha = Utc(2024, 2, 16), Estado = "ANULADA" },
                new Donacion { Monto = 100, Medio = MedioPago.EFECTIVO, Fecha = Utc(2024, 1, 20) });
            db.ParticipacionesVoluntario.Add(new ParticipacionVoluntario { VoluntarioId = vol.Id, EventoId = ev.Id, Horas = 3, Fecha = Utc(2024, 2, 10) });
            db.Encuestas.Add(encuesta);
            return Task.CompletedTask;
        });

        var escala = new EncuestaPregunta { EncuestaId = encuesta.Id, Orden = 1, Texto = "¿Qué tan satisfecho/a?", Tipo = "ESCALA" };
        var invAna = new EncuestaInvitacion { EncuestaId = encuesta.Id, ParticipanteId = ana, Token = Guid.NewGuid().ToString("N"), RespondidaEn = Utc(2024, 2, 27) };
        var invBeto = new EncuestaInvitacion { EncuestaId = encuesta.Id, ParticipanteId = beto, Token = Guid.NewGuid().ToString("N"), RespondidaEn = Utc(2024, 2, 28) };
        await api.EnBdAsync(db => { db.EncuestaPreguntas.Add(escala); db.EncuestaInvitaciones.AddRange(invAna, invBeto); return Task.CompletedTask; });
        await api.EnBdAsync(db =>
        {
            db.EncuestaRespuestas.AddRange(
                new EncuestaRespuesta { InvitacionId = invAna.Id, PreguntaId = escala.Id, Valor = 4 },
                new EncuestaRespuesta { InvitacionId = invBeto.Id, PreguntaId = escala.Id, Valor = 5 });
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task CP17_indicadores_de_un_mes_coinciden_con_la_base_y_cargan_rapido()
    {
        await SembrarFebrero2024Async();
        var dir = await api.DirectoraAsync();

        var reloj = Stopwatch.StartNew();
        var i = await (await dir.GetAsync("/api/Indicadores?mes=2024-02")).JsonAsync();
        reloj.Stop();
        Assert.True(reloj.ElapsedMilliseconds <= 2000, $"Tardó {reloj.ElapsedMilliseconds} ms (RNF-01: ≤ 2 s)");

        Assert.Equal(4, i.GetProperty("inscritosMes").GetInt32());
        Assert.Equal(2, i.GetProperty("inscritosCursos").GetInt32());
        Assert.Equal(66.7, i.GetProperty("asistenciaPromedio").GetDouble()); // (3 + 1) / (4 + 2)
        Assert.Equal(1, i.GetProperty("certificadosEmitidos").GetInt32());
        Assert.Equal(150, i.GetProperty("donacionesMes").GetDecimal());       // la anulada no suma
        Assert.Equal(100, i.GetProperty("donacionesMesAnterior").GetDecimal());
        Assert.Equal(50.0, i.GetProperty("variacionDonacionesPct").GetDouble());
        Assert.Equal(1, i.GetProperty("voluntariosActivos").GetInt32());
        Assert.Equal(1, i.GetProperty("voluntariosConActividad").GetInt32());
        Assert.Equal(4.5, i.GetProperty("satisfaccionPromedio").GetDouble());
        Assert.Equal(2, i.GetProperty("respuestasEncuestas").GetInt32());
        var serie = i.GetProperty("donacionesUltimosMeses").EnumerateArray().ToList();
        Assert.Equal(["2023-09", "2023-10", "2023-11", "2023-12", "2024-01", "2024-02"], serie.Select(s => s.Str("mes")));
        Assert.Contains(i.GetProperty("inscritosPorCurso").EnumerateArray(),
            c => c.Str("curso") == "Curso indicadores 2024" && c.GetProperty("inscritos").GetInt32() == 2);

        // Contra consultas SQL directas
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cn = db.Database.GetDbConnection();
        await cn.OpenAsync();
        async Task<decimal> Sql(string sql)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = sql;
            return Convert.ToDecimal(await cmd.ExecuteScalarAsync() ?? 0m);
        }
        const string Mes = "'2024-02-01 04:00+00' AND '2024-03-01 04:00+00'";
        Assert.Equal(await Sql($"SELECT COALESCE(SUM(monto),0) FROM donaciones WHERE estado='CONFIRMADA' AND fecha >= {Mes.Replace(" AND ", " AND fecha < ")}"),
                     i.GetProperty("donacionesMes").GetDecimal());
        Assert.Equal(await Sql($"SELECT COUNT(*) FROM certificados WHERE emitido_en >= {Mes.Replace(" AND ", " AND emitido_en < ")}"),
                     i.GetProperty("certificadosEmitidos").GetInt32());
        Assert.Equal(await Sql($"SELECT COUNT(*) FROM inscripciones WHERE fecha_inscripcion >= {Mes.Replace(" AND ", " AND fecha_inscripcion < ")}")
                   + await Sql($"SELECT COUNT(*) FROM inscripciones_evento WHERE estado <> 'CANCELADA' AND inscrito_en >= {Mes.Replace(" AND ", " AND inscrito_en < ")}"),
                     i.GetProperty("inscritosMes").GetInt32());

        // La segunda consulta sale de la caché (mismo cálculo, sin volver a la base)
        var cacheada = await (await dir.GetAsync("/api/Indicadores?mes=2024-02")).JsonAsync();
        Assert.True(cacheada.GetProperty("desdeCache").GetBoolean());
        Assert.Equal(i.GetProperty("calculadoEn").GetDateTime(), cacheada.GetProperty("calculadoEn").GetDateTime());

        // Otro mes se calcula por separado; el panel también se exporta
        var enero = await (await dir.GetAsync("/api/Indicadores?mes=2024-01")).JsonAsync();
        Assert.Equal(100, enero.GetProperty("donacionesMes").GetDecimal());
        Assert.Equal(JsonValueKind.Null, enero.GetProperty("variacionDonacionesPct").ValueKind);
        var pdf = await dir.GetAsync("/api/Reportes/indicadores?formato=pdf&desde=2024-02-01");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        var tabla = await (await dir.GetAsync("/api/Reportes/indicadores?formato=json&desde=2024-02-10")).JsonAsync();
        Assert.Contains(tabla.GetProperty("filas").EnumerateArray(), f => f[0].GetString() == "Donaciones del mes" && f[1].GetString() == "Bs. 150,00");
    }

    [Fact]
    public async Task Solo_la_directora_y_mes_valido()
    {
        var (contador, _, _) = await api.PersonalAsync("CONTADOR");
        await (await contador.GetAsync("/api/Indicadores")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await (await api.DocenteAsync()).GetAsync("/api/Indicadores")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await api.Anonimo().GetAsync("/api/Indicadores")).EsperarAsync(HttpStatusCode.Unauthorized);
        var dir = await api.DirectoraAsync();
        await (await dir.GetAsync("/api/Indicadores?mes=2024-13")).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.GetAsync("/api/Indicadores?mes=febrero")).EsperarAsync(HttpStatusCode.BadRequest);
        var actual = await (await dir.GetAsync("/api/Indicadores")).JsonAsync();
        Assert.Equal(DateTime.UtcNow.AddHours(-4).ToString("yyyy-MM"), actual.Str("mes"));
        await (await contador.GetAsync("/api/Reportes/indicadores?formato=pdf")).EsperarAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public void Parsear_mes_y_corte_en_hora_de_Bolivia()
    {
        Assert.Equal((2024, 2), IndicadorService.Parsear("2024-02", DateTime.UtcNow));
        Assert.Equal((2026, 10), IndicadorService.Parsear(null, new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc))); // aún 31/10 en Bolivia
        Assert.Throws<ArgumentException>(() => IndicadorService.Parsear("2024/02", DateTime.UtcNow));
        Assert.Equal(new DateTime(2024, 2, 1, 4, 0, 0, DateTimeKind.Utc), IndicadorService.InicioUtc(2024, 2));
        Assert.Equal("indicadores:2024-02", IndicadorService.Clave(2024, 2));
    }
}
