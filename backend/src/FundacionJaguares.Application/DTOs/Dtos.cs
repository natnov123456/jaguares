namespace FundacionJaguares.Application.DTOs;

// AUTH
public record RegistroDto(string NombreCompleto, string Email, string Password, string? Telefono);
public record LoginDto(string Email, string Password);
public record TokenDto(string Token, string NombreCompleto, string Rol, DateTime Expira);

// CURSOS
public record CrearCursoDto(string Titulo, string? Descripcion, DateOnly FechaInicio, DateOnly FechaFin,
                            int CupoMaximo, int AsistenciaMinimaPct = 80, Guid? PlantillaId = null);
public record CursoGestionDto(Guid Id, string Titulo, DateOnly FechaInicio, DateOnly FechaFin,
                              int CupoMaximo, int CupoDisponible, string Estado, int Inscritos, Guid? PlantillaId,
                              int EnEspera = 0);
public record AsignarPlantillaDto(Guid? PlantillaId);
public record MiInscripcionDto(Guid CursoId, string Titulo, DateOnly FechaInicio, DateOnly FechaFin,
                               string EstadoCurso, string EstadoInscripcion, DateTime FechaInscripcion);

// LISTA DE ESPERA (FUJ-111): Posicion empieza en 1; TotalEnEspera = personas en la cola del curso
public record ListaEsperaDto(Guid CursoId, string Titulo, DateOnly FechaInicio, DateOnly FechaFin,
                             int Posicion, int TotalEnEspera, DateTime AnotadoEn);
public record EnEsperaGestionDto(int Posicion, string Nombre, string Email, DateTime AnotadoEn);

// PANEL DEL PARTICIPANTE (FUJ-112)
// Tipo = "CURSO" | "EVENTO". EstadoInscripcion: CONFIRMADA | ASISTIO | NO_ASISTIO.
// InscripcionEventoId solo en eventos (para pedir el QR); TodoElDia = true en cursos (fechas sin hora).
public record ActividadPanelDto(Guid ActividadId, string Tipo, string Titulo, string? Lugar,
                                DateTime Inicio, DateTime? Fin, bool TodoElDia,
                                string EstadoActividad, string EstadoInscripcion, bool PuedeCancelar,
                                Guid? InscripcionEventoId, string? CodigoCertificado);
public record ResumenPanelDto(int CursosActivos, int EventosProximos, int ActividadesCompletadas,
                              int Certificados, int EnListaEspera);
public record PanelParticipanteDto(ResumenPanelDto Resumen, List<ActividadPanelDto> Proximas,
                                   List<ActividadPanelDto> Pasadas, List<CertificadoDto> Certificados,
                                   List<ListaEsperaDto> ListaEspera);

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
public record CertificadoDto(Guid Id, string CodigoUnico, string NombreCurso, DateTime EmitidoEn, bool Valido, string? UrlPdf);

// PLANTILLAS DE CERTIFICADO (FUJ-115)
public record GuardarPlantillaDto(string? Nombre, string? TipoActividad, string? Titulo, string? Texto,
                                  string? FirmaUnoNombre, string? FirmaUnoCargo,
                                  string? FirmaDosNombre, string? FirmaDosCargo);
public record PlantillaCertificadoDto(Guid Id, string Nombre, string TipoActividad, string Titulo, string Texto,
                                      string? LogoUrl,
                                      string? FirmaUnoUrl, string? FirmaUnoNombre, string? FirmaUnoCargo,
                                      string? FirmaDosUrl, string? FirmaDosNombre, string? FirmaDosCargo,
                                      DateTime ActualizadoEn);

// DONACIONES
public record CrearDonacionDto(decimal Monto, string Medio, string? Referencia, Guid? DonanteId,
                               string? DonanteNombre = null, string? DonanteEmail = null, Guid? CampanaId = null);

// CAMPAÑAS (FUJ-117)
public record GuardarCampanaDto(string? Nombre, string? Descripcion, decimal Meta, DateOnly FechaLimite, bool Activa = true);
public record CampanaDto(Guid Id, string Nombre, string? Descripcion, decimal Meta, DateOnly FechaLimite, bool Activa,
                         bool Vigente, decimal Recaudado, decimal Porcentaje, int CantidadDonaciones, DateTime CreadoEn);

public record MiDonacionDto(Guid Id, long NumeroComprobante, DateTime Fecha, decimal Monto, string Moneda,
                            string Medio, string? Referencia, string Estado);
public record ComprobanteDto(string NombreArchivo, byte[] Pdf);
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
public record RegistrarParticipacionDto(Guid? EventoId, string Descripcion, decimal? Horas = null);
public record ParticipacionDto(Guid Id, Guid? EventoId, string? EventoTitulo, string? Descripcion, DateTime Fecha,
                               decimal? Horas = null, Guid? CursoId = null, string? CursoTitulo = null);
public record VoluntarioConHistorialDto(Guid Id, string NombreCompleto, string Email, string? Telefono,
                                        string Estado, DateTime PostuladoEn, List<ParticipacionDto> Historial,
                                        decimal TotalHoras = 0);

// ASIGNACIÓN DE VOLUNTARIOS Y HORAS (FUJ-118). Tipo = "EVENTO" | "CURSO"
public record ActividadAsignableDto(string Tipo, Guid Id, string Titulo, string? Lugar, DateTime Inicio, DateTime? Fin,
                                    bool TodoElDia, int Asignados);
public record AsignarVoluntariosDto(List<Guid>? VoluntarioIds, Guid? EventoId, Guid? CursoId, string? Tarea);
public record RegistrarHorasDto(decimal Horas);
// Finalizada = la actividad ya terminó: solo entonces se pueden registrar horas
public record AsignacionDto(Guid Id, Guid VoluntarioId, string Voluntario, string? Email,
                            string Tipo, Guid ActividadId, string Actividad, string? Lugar,
                            DateTime Inicio, DateTime? Fin, bool TodoElDia, bool Finalizada,
                            string? Tarea, string Estado, decimal? Horas, DateTime AsignadoEn);
public record AsignacionOmitidaDto(Guid VoluntarioId, string Motivo);
public record ResultadoAsignacionDto(List<AsignacionDto> Asignadas, List<AsignacionOmitidaDto> Omitidas);

// ENCUESTAS DE SATISFACCIÓN (FUJ-120). Tipo de pregunta = "ESCALA" (1 a 5) | "TEXTO"
public record PreguntaEncuestaDto(string Texto, string Tipo);
// Preguntas == null: se usan las preguntas por defecto
public record CerrarActividadDto(List<PreguntaEncuestaDto>? Preguntas);
public record CierreActividadDto(int Asistentes, int EncuestasEnviadas, Guid? EncuestaId);
public record FinalizarCursoResultadoDto(int certificados_generados, int encuestas_enviadas, Guid? encuesta_id);

public record PreguntaPublicaDto(Guid Id, int Orden, string Texto, string Tipo);
public record EncuestaPublicaDto(string Actividad, string TipoActividad, DateTime Fecha, string Participante,
                                 bool YaRespondida, List<PreguntaPublicaDto> Preguntas);
public record RespuestaDto(Guid PreguntaId, int? Valor, string? Texto);
public record ResponderEncuestaDto(List<RespuestaDto>? Respuestas);
public record EncuestaPendienteDto(string Token, string Actividad, string TipoActividad, DateTime EnviadaEn);

public record ResumenEncuestaDto(Guid Id, string TipoActividad, Guid ActividadId, string Actividad, DateTime FechaActividad,
                                 DateTime CreadaEn, int Invitados, int Respuestas, decimal? PromedioSatisfaccion);
// Distribucion[i] = cantidad de respuestas con puntaje i+1
public record ResultadoPreguntaDto(Guid Id, int Orden, string Texto, string Tipo, int Respuestas,
                                   decimal? Promedio, int[] Distribucion);
public record ComentarioEncuestaDto(string Pregunta, string Texto, DateTime RespondidoEn);
public record ResultadoEncuestaDto(ResumenEncuestaDto Resumen, List<ResultadoPreguntaDto> Preguntas,
                                   List<ComentarioEncuestaDto> Comentarios);

// CONTENIDO
public record CrearNoticiaDto(string Titulo, string? Resumen, string Contenido, string? ImagenUrl, bool Publicada);

// QUIÉNES SOMOS Y CONTACTO (FUJ-127)
public record MiembroEquipoDto(string Nombre, string? Cargo, string? Descripcion);
public record PaginaInstitucionalDto(string? Mision, string? Vision, string? Historia, List<MiembroEquipoDto> Equipo,
                                     string? Direccion, string? Telefono, string? EmailContacto, string? Horario,
                                     DateTime? ActualizadoEn);
// SitioWeb es un campo trampa (honeypot): oculto para personas, los bots lo llenan
public record ConsultaContactoDto(string? Nombre, string? Email, string? Asunto, string? Mensaje, string? SitioWeb);

// GALERÍA (FUJ-126)
public record GuardarAlbumDto(string Titulo, string? Descripcion, bool Publicado);
public record AgregarVideoDto(string Url, string? Titulo);
public record GaleriaItemDto(Guid Id, string Tipo, string? Titulo, string? ImagenUrl, string? MiniaturaUrl,
                             string? VideoUrl, int Orden);
public record AlbumResumenDto(Guid Id, string Titulo, string? Descripcion, bool Publicado, string? PortadaUrl,
                              int CantidadImagenes, int CantidadVideos, DateTime CreadoEn);
public record AlbumDetalleDto(Guid Id, string Titulo, string? Descripcion, bool Publicado, DateTime CreadoEn,
                              List<GaleriaItemDto> Items);
public record ErrorSubidaDto(string Archivo, string Error);
public record ResultadoSubidaDto(List<GaleriaItemDto> Subidas, List<ErrorSubidaDto> Errores);
