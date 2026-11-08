namespace FundacionJaguares.Application.DTOs;

// AUTH
public record RegistroDto(string NombreCompleto, string Email, string Password, string? Telefono);
public record LoginDto(string Email, string Password);
public record TokenDto(string Token, string NombreCompleto, string Rol, DateTime Expira);

// CURSOS
public record CrearCursoDto(string Titulo, string? Descripcion, DateOnly FechaInicio, DateOnly FechaFin,
                            int CupoMaximo, int AsistenciaMinimaPct = 80);
public record CursoGestionDto(Guid Id, string Titulo, DateOnly FechaInicio, DateOnly FechaFin,
                              int CupoMaximo, int CupoDisponible, string Estado, int Inscritos);
public record MiInscripcionDto(Guid CursoId, string Titulo, DateOnly FechaInicio, DateOnly FechaFin,
                               string EstadoCurso, string EstadoInscripcion, DateTime FechaInscripcion);

// EVENTOS
public record CrearEventoDto(string Titulo, string? Descripcion, string? Lugar,
                             DateTime FechaInicio, DateTime? FechaFin);
public record AsistenciaDto(Guid ParticipanteId);

// CHECK-IN CON QR (FUJ-113)
public record MiInscripcionEventoDto(Guid Id, Guid EventoId, string Titulo, string? Lugar,
                                    DateTime FechaInicio, DateTime? FechaFin, string Estado,
                                    string CodigoQr, DateTime InscritoEn, DateTime? CheckinEn);
public record CheckinDto(string Codigo);
// Resultado: REGISTRADO | YA_REGISTRADO | INVALIDO | OTRO_EVENTO | FUERA_DE_FECHA
public record CheckinResultadoDto(string Resultado, string Mensaje, string? Participante, DateTime? CheckinEn);
public record InscritoEventoDto(Guid InscripcionId, string Nombre, string Email, string Estado,
                                DateTime InscritoEn, DateTime? CheckinEn);

// CALENDARIO (FUJ-114): Tipo = "CURSO" | "EVENTO"; TodoElDia = true en cursos (fechas sin hora)
public record ActividadDto(Guid Id, string Tipo, string Titulo, string? Descripcion, string? Lugar,
                           DateTime Inicio, DateTime? Fin, bool TodoElDia,
                           int? CupoMaximo, int? CupoDisponible, string Estado);

// CERTIFICADOS
public record VerificarCertificadoDto(string CodigoUnico);
public record CertificadoVerificacionDto(bool Valido, string? NombreParticipante, string? NombreCurso, DateTime? EmitidoEn);
public record CertificadoDto(Guid Id, string CodigoUnico, string NombreCurso, DateTime EmitidoEn, bool Valido);

// DONACIONES
public record CrearDonacionDto(decimal Monto, string Medio, string? Referencia, Guid? DonanteId);
public record DonanteDto(string Nombre, string? Email, string? Telefono, bool EsAnonimo);
public record ReporteDonacionesDto(int Anio, int Mes, decimal Total, int Cantidad, List<FundacionJaguares.Domain.Entities.Donacion> Detalle);

// TRANSPARENCIA (FUJ-121): solo montos agregados, ningún dato de donantes
public record DestinoDto(string Categoria, decimal Monto, string? Descripcion);
public record PublicarTransparenciaDto(List<DestinoDto> Destinos, string? Nota);
public record DestinoResumenDto(string Categoria, decimal Monto, string? Descripcion, decimal Porcentaje);
public record ResumenTransparenciaDto(int Anio, int Mes, decimal TotalRecaudado, int CantidadDonaciones,
                                      decimal TotalAsignado, List<DestinoResumenDto> Destinos,
                                      string? Nota, DateTime? PublicadoEn, bool Publicado);
public record MesPublicadoDto(int Anio, int Mes, decimal TotalRecaudado);

// VOLUNTARIADO
public record PostulacionVoluntarioDto(string NombreCompleto, string Email, string? Telefono);
public record RegistrarParticipacionDto(Guid? EventoId, string Descripcion);
public record ParticipacionDto(Guid Id, Guid? EventoId, string? EventoTitulo, string? Descripcion, DateTime Fecha);
public record VoluntarioConHistorialDto(Guid Id, string NombreCompleto, string Email, string? Telefono,
                                        string Estado, DateTime PostuladoEn, List<ParticipacionDto> Historial);

// CONTENIDO
public record CrearNoticiaDto(string Titulo, string? Resumen, string Contenido, string? ImagenUrl, bool Publicada);