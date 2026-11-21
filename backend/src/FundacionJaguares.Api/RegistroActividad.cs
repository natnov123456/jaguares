using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Channels;
using FundacionJaguares.Application.Services;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace FundacionJaguares.Api;

// Registro de actividad: cada solicitud a la API queda en la tabla registro_actividad con quién la hizo,
// cuándo, desde qué IP y navegador, qué acción fue y su resultado. Incluye inicios de sesión (también los
// fallidos), cierres de sesión, consultas, altas, cambios y exportaciones.
// No se guardan contraseñas, tokens ni el contenido de los formularios.
// Las filas se encolan y un servicio en segundo plano las guarda en lotes: registrar no hace más lenta la respuesta.

public class ColaRegistroActividad
{
    private readonly Channel<RegistroActividad> _canal =
        Channel.CreateBounded<RegistroActividad>(new BoundedChannelOptions(20_000) { FullMode = BoundedChannelFullMode.DropOldest });
    public void Encolar(RegistroActividad r) => _canal.Writer.TryWrite(r);
    public ChannelReader<RegistroActividad> Lector => _canal.Reader;
}

public class EscritorRegistroActividad(ColaRegistroActividad cola, IServiceScopeFactory scopes, ILogger<EscritorRegistroActividad> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        var lote = new List<RegistroActividad>(200);
        try
        {
            while (await cola.Lector.WaitToReadAsync(detener))
            {
                while (lote.Count < 200 && cola.Lector.TryRead(out var r)) lote.Add(r);
                await GuardarAsync(lote);
            }
        }
        catch (OperationCanceledException) { }
        // Al apagar la API se guarda lo que quedó en la cola
        while (cola.Lector.TryRead(out var r)) lote.Add(r);
        await GuardarAsync(lote);
    }

    private async Task GuardarAsync(List<RegistroActividad> lote)
    {
        if (lote.Count == 0) return;
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Un usuario borrado no rompe el lote: la referencia se deja vacía y queda el correo
            var ids = lote.Where(r => r.UsuarioId != null).Select(r => r.UsuarioId!.Value).Distinct().ToList();
            var existentes = ids.Count == 0 ? [] : db.Usuarios.Where(u => ids.Contains(u.Id)).Select(u => u.Id).ToHashSet();
            foreach (var r in lote.Where(r => r.UsuarioId is { } id && !existentes.Contains(id))) r.UsuarioId = null;
            // Inicio de sesión y registro: la persona todavía no tiene sesión; se la identifica por el correo
            var correos = lote.Where(r => r.UsuarioId == null && r.Email != null).Select(r => r.Email!.ToLower()).Distinct().ToList();
            if (correos.Count > 0)
            {
                var porCorreo = db.Usuarios.Where(u => correos.Contains(u.Email.ToLower()))
                    .Select(u => new { Email = u.Email.ToLower(), u.Id, Rol = u.Rol.ToString() }).ToList().ToDictionary(u => u.Email);
                foreach (var r in lote.Where(r => r.UsuarioId == null && r.Email != null && r.Exito))
                    if (porCorreo.TryGetValue(r.Email!.ToLower(), out var u)) { r.UsuarioId = u.Id; r.Rol ??= u.Rol; }
            }
            db.RegistroActividad.AddRange(lote);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "[ACTIVIDAD] No se pudieron guardar {n} registros de actividad", lote.Count);
        }
        finally { lote.Clear(); }
    }
}

public static class RegistroActividadMiddleware
{
    // IP y navegador de quien hace la solicitud (para el registro de consentimientos)
    public static OrigenSolicitud Origen(this HttpContext ctx) =>
        new(ctx.Connection.RemoteIpAddress?.ToString(), ctx.Request.Headers.UserAgent.ToString());

    // El controlador puede indicar el correo con el que se intentó entrar (login, registro, recuperar contraseña)
    public const string ClaveEmail = "actividad.email";
    public const string ClaveUsuarioId = "actividad.usuarioId";
    public const string ClaveRol = "actividad.rol";

    // Segmentos que nunca se guardan tal cual (enlaces de encuesta, de contraseña, códigos QR)
    private static readonly string[] ValoresSecretos = ["token", "codigo"];
    private static readonly string[] FiltrosSecretos = ["token", "password", "contrasena", "codigo"];

    public static IApplicationBuilder UseRegistroActividad(this IApplicationBuilder app) => app.Use(async (ctx, siguiente) =>
    {
        var ruta = ctx.Request.Path.Value ?? "";
        if (!ruta.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) || HttpMethods.IsOptions(ctx.Request.Method))
        {
            await siguiente();
            return;
        }
        var reloj = Stopwatch.StartNew();
        var estado = 500;
        try
        {
            await siguiente();
            estado = ctx.Response.StatusCode;
        }
        finally
        {
            reloj.Stop();
            try { Encolar(ctx, estado, (int)reloj.ElapsedMilliseconds); }
            catch (Exception ex) { ctx.RequestServices.GetService<ILogger<ColaRegistroActividad>>()?.LogWarning(ex, "[ACTIVIDAD] No se pudo registrar"); }
        }
    });

    private static void Encolar(HttpContext ctx, int estado, int ms)
    {
        var endpoint = ctx.GetEndpoint();
        var accion = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
        var plantilla = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? ctx.Request.Path.Value ?? "";

        var usuario = ctx.User;
        Guid? usuarioId = Guid.TryParse(usuario.FindFirstValue(ClaimTypes.NameIdentifier) ?? usuario.FindFirstValue("sub"), out var id) ? id : null;
        var email = usuario.FindFirstValue(ClaimTypes.Email) ?? usuario.FindFirstValue("email");
        var rol = usuario.FindFirstValue(ClaimTypes.Role) ?? usuario.FindFirstValue("role");
        if (ctx.Items[ClaveEmail] is string e) email ??= e;
        if (ctx.Items[ClaveUsuarioId] is Guid u) usuarioId ??= u;
        if (ctx.Items[ClaveRol] is string r) rol ??= r;

        // Identificadores de la ruta (curso, evento, donación...) y filtros, sin valores secretos
        var partes = new List<string>();
        foreach (var (clave, valor) in ctx.Request.RouteValues)
        {
            if (clave is "controller" or "action" || valor is null) continue;
            partes.Add($"{clave}={(ValoresSecretos.Contains(clave, StringComparer.OrdinalIgnoreCase) ? "***" : valor)}");
        }
        foreach (var (clave, valor) in ctx.Request.Query)
            partes.Add($"{clave}={(FiltrosSecretos.Any(s => clave.Contains(s, StringComparison.OrdinalIgnoreCase)) ? "***" : valor.ToString())}");

        var cola = ctx.RequestServices.GetRequiredService<ColaRegistroActividad>();
        cola.Encolar(new RegistroActividad
        {
            Fecha = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Email = ConsentimientoService.Recortar(email, 150),
            Rol = ConsentimientoService.Recortar(rol, 40),
            Accion = ConsentimientoService.Recortar(DescripcionAcciones.Describir(accion?.ControllerName, accion?.ActionName, ctx.Request.Method, estado), 120)!,
            Metodo = ctx.Request.Method,
            Ruta = ConsentimientoService.Recortar(plantilla.StartsWith('/') ? plantilla : "/" + plantilla, 300)!,
            Recurso = partes.Count == 0 ? null : ConsentimientoService.Recortar(string.Join("; ", partes), 300),
            Estado = (short)estado,
            Exito = estado < 400,
            Ip = ConsentimientoService.Recortar(ctx.Connection.RemoteIpAddress?.ToString(), 45),
            Navegador = ConsentimientoService.Recortar(ctx.Request.Headers.UserAgent.ToString(), 300),
            DuracionMs = ms,
        });
    }
}

// Nombre legible de cada acción para el registro de actividad
public static class DescripcionAcciones
{
    private static readonly Dictionary<string, string> Nombres = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Auth.Login"] = "Inicio de sesión", ["Auth.Salir"] = "Cierre de sesión", ["Auth.Registro"] = "Registro de cuenta",
        ["Auth.DefinirContrasena"] = "Definir contraseña con enlace", ["Auth.OlvideContrasena"] = "Pedido de recuperación de contraseña",
        ["Auth.CambiarMiContrasena"] = "Cambio de su contraseña",
        ["Usuarios.Crear"] = "Creación de cuenta del personal", ["Usuarios.CambiarRol"] = "Cambio de rol de una cuenta",
        ["Usuarios.CambiarActivo"] = "Activación o baja de una cuenta", ["Usuarios.ReenviarEnlace"] = "Reenvío de enlace de contraseña",
        ["Usuarios.Listar"] = "Consulta de usuarios y roles",
        ["Cursos.Crear"] = "Creación de un curso", ["Cursos.Editar"] = "Edición de un curso", ["Cursos.Reprogramar"] = "Reprogramación de un curso",
        ["Cursos.CancelarCurso"] = "Cancelación de un curso", ["Cursos.AsignarDocente"] = "Asignación de docente",
        ["Cursos.Inscribir"] = "Inscripción a un curso", ["Cursos.Cancelar"] = "Cancelación de inscripción a un curso",
        ["Cursos.UnirseListaEspera"] = "Ingreso a lista de espera", ["Cursos.SalirListaEspera"] = "Salida de lista de espera",
        ["Cursos.RegistrarAsistencia"] = "Registro de asistencia a un curso", ["Cursos.Finalizar"] = "Finalización de curso y emisión de certificados",
        ["Cursos.AsignarPlantilla"] = "Cambio de plantilla de certificado del curso",
        ["Eventos.Crear"] = "Creación de un evento", ["Eventos.Reprogramar"] = "Reprogramación de un evento", ["Eventos.CancelarEvento"] = "Cancelación de un evento",
        ["Eventos.Inscribir"] = "Inscripción a un evento", ["Eventos.Cancelar"] = "Cancelación de inscripción a un evento",
        ["Eventos.Checkin"] = "Check-in con QR", ["Eventos.RegistrarAsistencia"] = "Registro manual de asistencia a un evento",
        ["Eventos.CerrarAsistencia"] = "Cierre de asistencia de un evento",
        ["Certificados.Pdf"] = "Descarga de certificado", ["Certificados.Verificar"] = "Verificación pública de certificado",
        ["Certificados.CrearPlantilla"] = "Creación de plantilla de certificado", ["Certificados.ActualizarPlantilla"] = "Edición de plantilla de certificado",
        ["Certificados.EliminarPlantilla"] = "Eliminación de plantilla de certificado",
        ["Donaciones.Registrar"] = "Registro de donación", ["Donaciones.EnLinea"] = "Donación en línea", ["Donaciones.Editar"] = "Corrección de donación",
        ["Donaciones.Anular"] = "Anulación de donación", ["Donaciones.Comprobante"] = "Descarga de comprobante de donación",
        ["Donaciones.Reporte"] = "Consulta del reporte mensual de donaciones",
        ["Campanas.Crear"] = "Creación de campaña", ["Campanas.Actualizar"] = "Edición de campaña",
        ["Transparencia.Publicar"] = "Publicación de transparencia", ["Transparencia.Despublicar"] = "Retiro de transparencia publicada",
        ["Auditoria.Donaciones"] = "Consulta de la bitácora de donaciones",
        ["Voluntarios.Postular"] = "Postulación de voluntario", ["Voluntarios.Asignar"] = "Asignación de voluntarios",
        ["Voluntarios.CancelarAsignacion"] = "Retiro de una asignación de voluntario", ["Voluntarios.RegistrarHoras"] = "Registro de horas de voluntariado",
        ["Voluntarios.RegistrarParticipacion"] = "Registro de actividad de voluntario",
        ["Encuestas.Responder"] = "Respuesta a encuesta de satisfacción",
        ["Contenido.CrearNoticia"] = "Publicación de noticia", ["Institucional.Actualizar"] = "Edición de Quiénes somos",
        ["Contacto.Enviar"] = "Consulta desde el formulario de contacto",
        ["Galeria.CrearAlbum"] = "Creación de álbum", ["Galeria.ActualizarAlbum"] = "Edición o publicación de álbum",
        ["Galeria.EliminarAlbum"] = "Eliminación de álbum", ["Galeria.SubirImagenes"] = "Subida de fotos a la galería",
        ["Galeria.AgregarVideo"] = "Video agregado a la galería", ["Galeria.EliminarItem"] = "Foto o video quitado de la galería",
        ["Reportes.Exportar"] = "Exportación de reporte", ["Indicadores.Obtener"] = "Consulta del panel de indicadores",
        ["RegistroActividad.Listar"] = "Consulta del registro de actividad",
        ["Cursos.MisInscripciones"] = "Consulta de sus inscripciones a cursos", ["Eventos.MisInscripciones"] = "Consulta de sus inscripciones a eventos",
        ["Participante.Panel"] = "Consulta de Mi panel", ["Certificados.Mios"] = "Consulta de sus certificados",
        ["Donaciones.Mias"] = "Consulta de sus donaciones", ["Voluntarios.MiHistorial"] = "Consulta de su historial de voluntariado",
        ["Eventos.Qr"] = "Descarga de su código QR", ["Cursos.Gestion"] = "Consulta de cursos (gestión)", ["Eventos.Gestion"] = "Consulta de eventos (gestión)",
        ["Voluntarios.Listar"] = "Consulta del registro de voluntarios", ["Voluntarios.Asignaciones"] = "Consulta de asignaciones de voluntarios",
        ["Eventos.Inscritos"] = "Consulta de inscritos de un evento", ["Cursos.Detalle"] = "Consulta de un curso", ["Cursos.Asistencia"] = "Consulta de asistencia de un curso",
    };

    public static string Describir(string? controlador, string? accion, string metodo, int estado)
    {
        if (controlador is not null && accion is not null && Nombres.TryGetValue($"{controlador}.{accion}", out var nombre))
            return estado is 401 or 403 && controlador == "Auth" && accion == "Login" ? "Inicio de sesión fallido" : nombre;
        var verbo = metodo.ToUpperInvariant() switch
        {
            "GET" => "Consulta", "POST" => "Alta", "PUT" or "PATCH" => "Modificación", "DELETE" => "Eliminación", _ => metodo,
        };
        return controlador is null ? $"{verbo} (ruta inexistente)" : $"{verbo} en {controlador}";
    }
}
