using FundacionJaguares.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Usuarios => Set<User>();
    public DbSet<Curso> Cursos => Set<Curso>();
    public DbSet<Inscripcion> Inscripciones => Set<Inscripcion>();
    public DbSet<ListaEspera> ListaEspera => Set<ListaEspera>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<AsistenciaEvento> AsistenciasEvento => Set<AsistenciaEvento>();
    public DbSet<InscripcionEvento> InscripcionesEvento => Set<InscripcionEvento>();
    public DbSet<Certificado> Certificados => Set<Certificado>();
    public DbSet<PlantillaCertificado> PlantillasCertificado => Set<PlantillaCertificado>();
    public DbSet<Donante> Donantes => Set<Donante>();
    public DbSet<Donacion> Donaciones => Set<Donacion>();
    public DbSet<Campana> Campanas => Set<Campana>();
    public DbSet<ReporteTransparencia> ReportesTransparencia => Set<ReporteTransparencia>();
    public DbSet<DestinoTransparencia> DestinosTransparencia => Set<DestinoTransparencia>();
    public DbSet<Voluntario> Voluntarios => Set<Voluntario>();
    public DbSet<ParticipacionVoluntario> ParticipacionesVoluntario => Set<ParticipacionVoluntario>();
    public DbSet<AsignacionVoluntario> AsignacionesVoluntario => Set<AsignacionVoluntario>();
    public DbSet<Noticia> Noticias => Set<Noticia>();
    public DbSet<GaleriaItem> Galeria => Set<GaleriaItem>();
    public DbSet<Album> Albumes => Set<Album>();
    public DbSet<NotificacionLog> NotificacionesLog => Set<NotificacionLog>();
    public DbSet<Encuesta> Encuestas => Set<Encuesta>();
    public DbSet<EncuestaPregunta> EncuestaPreguntas => Set<EncuestaPregunta>();
    public DbSet<EncuestaInvitacion> EncuestaInvitaciones => Set<EncuestaInvitacion>();
    public DbSet<EncuestaRespuesta> EncuestaRespuestas => Set<EncuestaRespuesta>();
    public DbSet<PaginaInstitucional> PaginaInstitucional => Set<PaginaInstitucional>();
    public DbSet<ConsultaContacto> ConsultasContacto => Set<ConsultaContacto>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<User>(e =>
        {
            e.ToTable("usuarios");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
        });

        mb.Entity<Curso>(e =>
        {
            e.ToTable("cursos");
            e.HasKey(x => x.Id);
        });

        mb.Entity<Inscripcion>(e =>
        {
            e.ToTable("inscripciones");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CursoId, x.ParticipanteId }).IsUnique();
        });

        mb.Entity<ListaEspera>(e =>
        {
            e.ToTable("lista_espera");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CursoId, x.ParticipanteId }).IsUnique().HasFilter("estado = 'EN_ESPERA'");
            e.HasIndex(x => new { x.CursoId, x.CreadoEn });
        });

        mb.Entity<Evento>(e =>
        {
            e.ToTable("eventos");
            e.HasKey(x => x.Id);
        });

        mb.Entity<AsistenciaEvento>(e =>
        {
            e.ToTable("asistencias_evento");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EventoId, x.ParticipanteId }).IsUnique();
        });

        mb.Entity<InscripcionEvento>(e =>
        {
            e.ToTable("inscripciones_evento");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CodigoQr).IsUnique();
            e.HasIndex(x => new { x.EventoId, x.ParticipanteId }).IsUnique();
        });

        mb.Entity<Certificado>(e =>
        {
            e.ToTable("certificados");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CodigoUnico).IsUnique();
        });

        mb.Entity<PlantillaCertificado>(e =>
        {
            e.ToTable("plantillas_certificado");
            e.HasKey(x => x.Id);
        });

        mb.Entity<Donante>(e =>
        {
            e.ToTable("donantes");
            e.HasKey(x => x.Id);
        });

        mb.Entity<Donacion>(e =>
        {
            e.ToTable("donaciones");
            e.HasKey(x => x.Id);
            e.Property(x => x.Monto).HasPrecision(12, 2);
            e.Property(x => x.NumeroComprobante).ValueGeneratedOnAdd();
            // Relación explícita: el donante nuevo debe insertarse antes que su donación
            e.HasOne<Donante>().WithMany().HasForeignKey(x => x.DonanteId).OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<Campana>(e =>
        {
            e.ToTable("campanas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Meta).HasPrecision(12, 2);
        });

        mb.Entity<ReporteTransparencia>(e =>
        {
            e.ToTable("reportes_transparencia");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Anio, x.Mes }).IsUnique();
            e.Property(x => x.TotalRecaudado).HasPrecision(12, 2);
            e.HasMany(x => x.Destinos).WithOne().HasForeignKey(d => d.ReporteId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<DestinoTransparencia>(e =>
        {
            e.ToTable("transparencia_destinos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Monto).HasPrecision(12, 2);
        });

        mb.Entity<Voluntario>(e =>
        {
            e.ToTable("voluntarios");
            e.HasKey(x => x.Id);
        });

        mb.Entity<ParticipacionVoluntario>(e =>
        {
            e.ToTable("participaciones_voluntario");
            e.HasKey(x => x.Id);
            e.Property(x => x.Horas).HasPrecision(5, 2);
            e.HasIndex(x => x.AsignacionId).IsUnique();
        });

        mb.Entity<AsignacionVoluntario>(e =>
        {
            e.ToTable("asignaciones_voluntario");
            e.HasKey(x => x.Id);
            e.Property(x => x.Horas).HasPrecision(5, 2);
        });

        mb.Entity<Noticia>(e =>
        {
            e.ToTable("noticias");
            e.HasKey(x => x.Id);
        });

        mb.Entity<GaleriaItem>(e =>
        {
            e.ToTable("galeria");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AlbumId, x.Orden });
        });

        mb.Entity<Album>(e =>
        {
            e.ToTable("albumes");
            e.HasKey(x => x.Id);
            e.HasMany<GaleriaItem>().WithOne().HasForeignKey(i => i.AlbumId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<PaginaInstitucional>(e =>
        {
            e.ToTable("pagina_institucional");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.EquipoJson).HasColumnName("equipo").HasColumnType("jsonb");
        });

        mb.Entity<ConsultaContacto>(e =>
        {
            e.ToTable("consultas_contacto");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Ip, x.CreadoEn });
        });

        mb.Entity<Encuesta>(e =>
        {
            e.ToTable("encuestas");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CursoId).IsUnique().HasFilter("curso_id IS NOT NULL");
            e.HasIndex(x => x.EventoId).IsUnique().HasFilter("evento_id IS NOT NULL");
        });

        mb.Entity<EncuestaPregunta>(e =>
        {
            e.ToTable("encuesta_preguntas");
            e.HasKey(x => x.Id);
            // Relaciones explícitas: EF inserta la encuesta antes que sus preguntas e invitaciones
            e.HasOne<Encuesta>().WithMany().HasForeignKey(x => x.EncuestaId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.EncuestaId, x.Orden }).IsUnique();
        });

        mb.Entity<EncuestaInvitacion>(e =>
        {
            e.ToTable("encuesta_invitaciones");
            e.HasKey(x => x.Id);
            e.HasOne<Encuesta>().WithMany().HasForeignKey(x => x.EncuestaId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => new { x.EncuestaId, x.ParticipanteId }).IsUnique();
        });

        mb.Entity<EncuestaRespuesta>(e =>
        {
            e.ToTable("encuesta_respuestas");
            e.HasKey(x => x.Id);
            e.HasOne<EncuestaInvitacion>().WithMany().HasForeignKey(x => x.InvitacionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<EncuestaPregunta>().WithMany().HasForeignKey(x => x.PreguntaId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.InvitacionId, x.PreguntaId }).IsUnique();
        });

        mb.Entity<NotificacionLog>(e =>
        {
            e.ToTable("notificaciones_log");
            e.HasKey(x => x.Id);
        });
    }
}