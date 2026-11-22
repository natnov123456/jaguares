using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FundacionJaguares.Tests.Integracion;

// JAG-88 / FUJ-125: bitácora de auditoría de donaciones (alta, edición, anulación), solo lectura, filtros y permisos
[Collection(ColeccionApi.Nombre)]
public class AuditoriaTests(ApiFixture api)
{
    private static List<JsonElement> Registros(JsonElement pagina) => pagina.GetProperty("registros").EnumerateArray().ToList();

    private async Task<JsonElement> RegistrarAsync(HttpClient c, decimal monto, string referencia) =>
        await (await c.PostJsonAsync("/api/Donaciones", new { monto, medio = "EFECTIVO", referencia, donanteId = (Guid?)null,
                                                              donanteNombre = "Donante Auditado" })).JsonAsync();

    [Fact]
    public async Task CP19_editar_el_monto_de_100_a_150_queda_en_la_bitacora_con_usuario_y_hora()
    {
        var (contador, contadorId, _) = await api.PersonalAsync("CONTADOR", "Carla Contadora");
        var antes = DateTime.UtcNow.AddSeconds(-5);
        var d = await RegistrarAsync(contador, 100, "REC-CP19");

        var editada = await (await contador.PutJsonAsync($"/api/Donaciones/{d.Id()}", new { monto = 150, medio = "EFECTIVO", referencia = "REC-CP19",
            campanaId = (Guid?)null, motivo = "Error de tipeo en el recibo" })).JsonAsync();
        Assert.Equal(150, editada.GetProperty("monto").GetDecimal());

        var numero = d.GetProperty("numeroComprobante").GetInt64();
        var pagina = await (await contador.GetAsync($"/api/Auditoria/donaciones?q={numero}")).JsonAsync();
        var regs = Registros(pagina);
        Assert.Equal(2, regs.Count); // exactamente uno por operación
        var edicion = regs.Single(r => r.Str("accion") == "EDICION");
        Assert.Equal("Carla Contadora", edicion.Str("usuarioNombre"));
        Assert.Equal(contadorId, edicion.Id("usuarioId"));
        Assert.Equal("CONTADOR", edicion.Str("rol"));
        Assert.Equal("Error de tipeo en el recibo", edicion.Str("motivo"));
        Assert.True(edicion.GetProperty("creadoEn").GetDateTime().ToUniversalTime() >= antes);
        Assert.Equal(100, JsonDocument.Parse(edicion.Str("valorAnterior")).RootElement.GetProperty("monto").GetDecimal());
        Assert.Equal(150, JsonDocument.Parse(edicion.Str("valorNuevo")).RootElement.GetProperty("monto").GetDecimal());
        var alta = regs.Single(r => r.Str("accion") == "ALTA");
        Assert.Equal(JsonValueKind.Null, alta.GetProperty("valorAnterior").ValueKind);

        // Sin cambios no se registra nada
        await (await contador.PutJsonAsync($"/api/Donaciones/{d.Id()}", new { monto = 150, medio = "EFECTIVO", referencia = "REC-CP19", campanaId = (Guid?)null }))
            .EsperarAsync(HttpStatusCode.BadRequest);
        Assert.Equal(2, Registros(await (await contador.GetAsync($"/api/Auditoria/donaciones?q={numero}")).JsonAsync()).Count);
    }

    [Fact]
    public async Task Anular_exige_motivo_deja_de_sumar_y_queda_registrado()
    {
        var dir = await api.DirectoraAsync();
        var hoy = DateTime.UtcNow;
        var d = await RegistrarAsync(dir, 777, "REC-ANULAR");
        decimal Total(JsonElement r) => r.GetProperty("total").GetDecimal();
        var antes = Total(await (await dir.GetAsync($"/api/Donaciones/reporte/{hoy.Year}/{hoy.Month}")).JsonAsync());

        await (await dir.PostJsonAsync($"/api/Donaciones/{d.Id()}/anular", new { motivo = "" })).EsperarAsync(HttpStatusCode.BadRequest);
        var anulada = await (await dir.PostJsonAsync($"/api/Donaciones/{d.Id()}/anular", new { motivo = "Billete falso detectado" })).JsonAsync();
        Assert.Equal("ANULADA", anulada.Str("estado"));
        var despues = Total(await (await dir.GetAsync($"/api/Donaciones/reporte/{hoy.Year}/{hoy.Month}")).JsonAsync());
        Assert.Equal(antes - 777, despues);

        await (await dir.PostJsonAsync($"/api/Donaciones/{d.Id()}/anular", new { motivo = "Otra vez anulada" })).EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.PutJsonAsync($"/api/Donaciones/{d.Id()}", new { monto = 1, medio = "EFECTIVO", referencia = (string?)null, campanaId = (Guid?)null }))
            .EsperarAsync(HttpStatusCode.BadRequest);
        await (await dir.GetAsync($"/api/Donaciones/{d.Id()}/comprobante")).EsperarAsync(HttpStatusCode.BadRequest);

        var regs = Registros(await (await dir.GetAsync($"/api/Auditoria/donaciones?q={d.GetProperty("numeroComprobante").GetInt64()}&accion=ANULACION")).JsonAsync());
        var r = Assert.Single(regs);
        Assert.Equal("Billete falso detectado", r.Str("motivo"));
        Assert.Equal("ANULADA", JsonDocument.Parse(r.Str("valorNuevo")).RootElement.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Donacion_en_linea_sin_cuenta_tambien_se_audita()
    {
        var d = await (await api.Anonimo().DonarEnLineaAsync(50, "QR", "Visitante Generoso", "visitante@correo.com")).JsonAsync();
        var regs = Registros(await (await (await api.DirectoraAsync()).GetAsync($"/api/Auditoria/donaciones?q={d.GetProperty("numeroComprobante").GetInt64()}")).JsonAsync());
        var alta = Assert.Single(regs);
        Assert.Equal("ALTA", alta.Str("accion"));
        Assert.Equal("Donación en línea (sin cuenta)", alta.Str("usuarioNombre"));
        Assert.Equal(JsonValueKind.Null, alta.GetProperty("usuarioId").ValueKind);
    }

    [Fact]
    public async Task La_bitacora_es_de_solo_lectura_incluso_en_la_base_de_datos()
    {
        await RegistrarAsync(await api.DirectoraAsync(), 10, "REC-SOLO-LECTURA");
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ex1 = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE auditoria_donaciones SET motivo = 'x'"));
        Assert.Contains("solo lectura", ex1.MessageText);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM auditoria_donaciones"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("TRUNCATE auditoria_donaciones"));

        // Y la API no ofrece forma de modificarla
        var dir = await api.DirectoraAsync();
        var id = Registros(await (await dir.GetAsync("/api/Auditoria/donaciones?tamanoPagina=1")).JsonAsync())[0].Id();
        Assert.Contains((await dir.DeleteAsync($"/api/Auditoria/donaciones/{id}")).StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Contains((await dir.PutJsonAsync($"/api/Auditoria/donaciones/{id}", new { })).StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    [Fact]
    public async Task Filtros_por_fecha_usuario_y_accion_paginacion_permisos_y_exportacion()
    {
        var (contador, contadorId, _) = await api.PersonalAsync("CONTADOR", "Filtro Contador");
        var d1 = await RegistrarAsync(contador, 20, "REC-F1");
        await RegistrarAsync(contador, 30, "REC-F2");
        await (await contador.PostJsonAsync($"/api/Donaciones/{d1.Id()}/anular", new { motivo = "Registro duplicado" })).EsperarAsync(HttpStatusCode.OK);

        var deContador = Registros(await (await contador.GetAsync($"/api/Auditoria/donaciones?usuarioId={contadorId}")).JsonAsync());
        Assert.Equal(3, deContador.Count);
        var altas = Registros(await (await contador.GetAsync($"/api/Auditoria/donaciones?usuarioId={contadorId}&accion=alta")).JsonAsync());
        Assert.Equal(2, altas.Count);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4)).ToString("yyyy-MM-dd");
        Assert.Equal(3, Registros(await (await contador.GetAsync($"/api/Auditoria/donaciones?usuarioId={contadorId}&desde={hoy}&hasta={hoy}")).JsonAsync()).Count);
        Assert.Empty(Registros(await (await contador.GetAsync($"/api/Auditoria/donaciones?usuarioId={contadorId}&desde=2020-01-01&hasta=2020-01-31")).JsonAsync()));
        Assert.Single(Registros(await (await contador.GetAsync($"/api/Auditoria/donaciones?usuarioId={contadorId}&q=duplicado")).JsonAsync()));

        var pag = await (await contador.GetAsync($"/api/Auditoria/donaciones?usuarioId={contadorId}&tamanoPagina=2&pagina=2")).JsonAsync();
        Assert.Equal(3, pag.GetProperty("total").GetInt32());
        Assert.Single(Registros(pag));

        await (await contador.GetAsync("/api/Auditoria/donaciones?accion=BORRAR")).EsperarAsync(HttpStatusCode.BadRequest);
        var usuarios = await (await contador.GetAsync("/api/Auditoria/donaciones/usuarios")).JsonAsync();
        Assert.Contains(usuarios.EnumerateArray(), u => u.Id("usuarioId") == contadorId);

        var encargado = await api.EncargadoAsync();
        await (await encargado.GetAsync("/api/Auditoria/donaciones")).EsperarAsync(HttpStatusCode.Forbidden);
        await (await encargado.PutJsonAsync($"/api/Donaciones/{d1.Id()}", new { monto = 5, medio = "EFECTIVO", referencia = (string?)null, campanaId = (Guid?)null }))
            .EsperarAsync(HttpStatusCode.Forbidden);
        await (await encargado.GetAsync("/api/Reportes/bitacora-donaciones?formato=pdf")).EsperarAsync(HttpStatusCode.Forbidden);

        var xlsx = await contador.GetAsync($"/api/Reportes/bitacora-donaciones?formato=xlsx&usuarioId={contadorId}&accion=ANULACION");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        using var libro = new XLWorkbook(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync()));
        var fila = Assert.Single(libro.Worksheet(1).RowsUsed(), r => r.Cell(3).GetString() == "Anulación");
        Assert.Equal("Filtro Contador", fila.Cell(4).GetString());
        Assert.Contains("estado: CONFIRMADA → ANULADA", fila.Cell(6).GetString());
    }
}
