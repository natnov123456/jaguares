using FundacionJaguares.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Usuarios => Set<User>();
    public DbSet<Curso> Cursos => Set<Curso>();
    public DbSet<Inscripcion> Inscripciones => Set<Inscripcion>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<AsistenciaEvento> AsistenciasEvento => Set<AsistenciaEvento>();
    public DbSet<Certificado> Certificados => Set<Certificado>();
    public DbSet<Donante> Donantes => Set<Donante>();
    public DbSet<Donacion> Donaciones => Set<Donacion>();
    public DbSet<Voluntario> Voluntarios => Set<Voluntario>();
    public DbSet<ParticipacionVoluntario> ParticipacionesVoluntario => Set<ParticipacionVoluntario>();
    public DbSet<Noticia> Noticias => Set<Noticia>();
    public DbSet<GaleriaItem> Galeria => Set<GaleriaItem>();
    public DbSet<NotificacionLog> NotificacionesLog => Set<NotificacionLog>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<User>(e =>
        {
            e.ToTable("usuarios");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Rol).HasConversion<string>();
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

        mb.Entity<Certificado>(e =>
        {
            e.ToTable("certificados");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CodigoUnico).IsUnique();
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
            e.Property(x => x.Medio).HasConversion<string>();
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
        });

        mb.Entity<NotificacionLog>(e =>
        {
            e.ToTable("notificaciones_log");
            e.HasKey(x => x.Id);
        });
    }
}