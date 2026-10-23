namespace FundacionJaguares.Application.DTOs;

// AUTH
public record RegistroDto(string NombreCompleto, string Email, string Password, string? Telefono);
public record LoginDto(string Email, string Password);
public record TokenDto(string Token, string NombreCompleto, string Rol, DateTime Expira);

// CURSOS
public record CrearCursoDto(string Titulo, string? Descripcion, DateOnly FechaInicio, DateOnly FechaFin,
                            int CupoMaximo, int AsistenciaMinimaPct = 80);

// EVENTOS
public record CrearEventoDto(string Titulo, string? Descripcion, string? Lugar,
                             DateTime FechaInicio, DateTime? FechaFin);
public record AsistenciaDto(Guid ParticipanteId);

// CERTIFICADOS
public record VerificarCertificadoDto(string CodigoUnico);
public record CertificadoVerificacionDto(bool Valido, string? NombreParticipante, string? NombreCurso, DateTime? EmitidoEn);

// DONACIONES
public record CrearDonacionDto(decimal Monto, string Medio, string? Referencia, Guid? DonanteId);
public record DonanteDto(string Nombre, string? Email, string? Telefono, bool EsAnonimo);
public record ReporteDonacionesDto(int Anio, int Mes, decimal Total, int Cantidad, List<FundacionJaguares.Domain.Entities.Donacion> Detalle);

// VOLUNTARIADO
public record PostulacionVoluntarioDto(string NombreCompleto, string Email, string? Telefono);
public record RegistrarParticipacionDto(Guid? EventoId, string Descripcion);
public record ParticipacionDto(Guid Id, Guid? EventoId, string? EventoTitulo, string? Descripcion, DateTime Fecha);
public record VoluntarioConHistorialDto(Guid Id, string NombreCompleto, string Email, string? Telefono,
                                        string Estado, DateTime PostuladoEn, List<ParticipacionDto> Historial);

// CONTENIDO
public record CrearNoticiaDto(string Titulo, string? Resumen, string Contenido, string? ImagenUrl, bool Publicada);