using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using FundacionJaguares.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Application.Services;

public interface ICursoService
{
    Task<List<Curso>> ListarPublicadosAsync();
    Task<Curso> CrearAsync(CrearCursoDto dto, Guid docenteId);
    Task<Inscripcion> InscribirAsync(Guid cursoId, Guid participanteId);
    Task CancelarInscripcionAsync(Guid cursoId, Guid participanteId);
    Task<List<MiInscripcionDto>> ListarInscripcionesAsync(Guid participanteId);
    Task<List<CursoGestionDto>> ListarGestionAsync(Guid? docenteId);
    Task<int> FinalizarYGenerarCertificadosAsync(Guid cursoId, Guid? docenteId);
    Task AsignarPlantillaAsync(Guid cursoId, Guid? plantillaId, Guid? docenteId);
}

public class CursoService : ICursoService
{
    private readonly CursoRepository _cursos;
    private readonly CertificadoRepository _certs;
    private readonly AppDbContext _db;
    private readonly IEmailAdapter _email;
    private readonly IPlantillaService _plantillas;

    public CursoService(CursoRepository cursos, CertificadoRepository certs, AppDbContext db, IEmailAdapter email,
                        IPlantillaService plantillas)
    { _cursos = cursos; _certs = certs; _db = db; _email = email; _plantillas = plantillas; }

    public Task<List<Curso>> ListarPublicadosAsync() => _cursos.GetPublicadosAsync();

    public async Task<Curso> CrearAsync(CrearCursoDto dto, Guid docenteId)
    {
        if (dto.FechaFin < dto.FechaInicio)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio");
        if (dto.CupoMaximo <= 0)
            throw new ArgumentException("El cupo máximo debe ser mayor a 0");
        await ValidarPlantillaAsync(dto.PlantillaId);

        var c = new Curso
        {
            Titulo = dto.Titulo, Descripcion = dto.Descripcion,
            DocenteId = docenteId,
            FechaInicio = dto.FechaInicio, FechaFin = dto.FechaFin,
            CupoMaximo = dto.CupoMaximo, CupoDisponible = dto.CupoMaximo,
            AsistenciaMinimaPct = dto.AsistenciaMinimaPct,
            PlantillaId = dto.PlantillaId,
            Estado = "PUBLICADO"
        };
        await _cursos.AddAsync(c);
        await _cursos.SaveChangesAsync();
        return c;
    }

    public async Task<Inscripcion> InscribirAsync(Guid cursoId, Guid participanteId)
    {
        if (await _cursos.ExisteInscripcionAsync(cursoId, participanteId))
            throw new InvalidOperationException("Ya estás inscrito en este curso");

        var curso = await _cursos.GetByIdAsync(cursoId)
                    ?? throw new KeyNotFoundException("Curso no encontrado");

        if (!await _cursos.ReservarCupoAsync(cursoId))
            throw new InvalidOperationException("No hay cupos disponibles");

        var ins = new Inscripcion { CursoId = cursoId, ParticipanteId = participanteId };
        _db.Inscripciones.Add(ins);
        await _db.SaveChangesAsync();

        var u = await _db.Usuarios.FindAsync(participanteId);
        if (u != null)
            await _email.EnviarAsync(u.Email, "Inscripción confirmada",
                $"Hola {u.NombreCompleto}, te inscribiste en '{curso.Titulo}'.");

        return ins;
    }

    public async Task CancelarInscripcionAsync(Guid cursoId, Guid participanteId)
    {
        var ins = await _db.Inscripciones.FirstOrDefaultAsync(i =>
            i.CursoId == cursoId && i.ParticipanteId == participanteId)
            ?? throw new KeyNotFoundException("Inscripción no encontrada");
        _db.Inscripciones.Remove(ins);
        await _db.SaveChangesAsync();
        await _cursos.LiberarCupoAsync(cursoId);
    }

    public Task<List<MiInscripcionDto>> ListarInscripcionesAsync(Guid participanteId) =>
        (from i in _db.Inscripciones
         join c in _db.Cursos on i.CursoId equals c.Id
         where i.ParticipanteId == participanteId
         orderby c.FechaInicio descending
         select new MiInscripcionDto(c.Id, c.Titulo, c.FechaInicio, c.FechaFin,
                                     c.Estado, i.Estado, i.FechaInscripcion)).ToListAsync();

    // docenteId == null: la directora ve todos los cursos
    public Task<List<CursoGestionDto>> ListarGestionAsync(Guid? docenteId) =>
        _db.Cursos
            .Where(c => docenteId == null || c.DocenteId == docenteId)
            .OrderByDescending(c => c.FechaInicio)
            .Select(c => new CursoGestionDto(c.Id, c.Titulo, c.FechaInicio, c.FechaFin,
                c.CupoMaximo, c.CupoDisponible, c.Estado,
                _db.Inscripciones.Count(i => i.CursoId == c.Id && i.Estado == "CONFIRMADA"), c.PlantillaId))
            .ToListAsync();

    // docenteId == null: la directora puede cambiar la plantilla de cualquier curso
    public async Task AsignarPlantillaAsync(Guid cursoId, Guid? plantillaId, Guid? docenteId)
    {
        var curso = await _cursos.GetByIdAsync(cursoId)
                    ?? throw new KeyNotFoundException("Curso no encontrado");
        if (docenteId != null && curso.DocenteId != docenteId)
            throw new UnauthorizedAccessException("Solo puedes modificar tus propios cursos");
        if (curso.Estado == "FINALIZADO")
            throw new InvalidOperationException("El curso ya fue finalizado: sus certificados ya se emitieron");
        await ValidarPlantillaAsync(plantillaId);

        curso.PlantillaId = plantillaId;
        await _db.SaveChangesAsync();
    }

    private async Task ValidarPlantillaAsync(Guid? plantillaId)
    {
        if (plantillaId != null && !await _db.PlantillasCertificado.AnyAsync(p => p.Id == plantillaId))
            throw new ArgumentException("La plantilla seleccionada no existe");
    }

    // docenteId == null: la directora puede finalizar cualquier curso
    public async Task<int> FinalizarYGenerarCertificadosAsync(Guid cursoId, Guid? docenteId)
    {
        var curso = await _cursos.GetByIdAsync(cursoId)
                    ?? throw new KeyNotFoundException("Curso no encontrado");
        if (docenteId != null && curso.DocenteId != docenteId)
            throw new UnauthorizedAccessException("Solo puedes finalizar tus propios cursos");
        if (curso.Estado == "FINALIZADO")
            throw new InvalidOperationException("El curso ya fue finalizado y sus certificados emitidos");

        var inscripciones = await _db.Inscripciones
            .Where(i => i.CursoId == cursoId && i.Estado == "CONFIRMADA")
            .ToListAsync();

        var diseno = await _plantillas.CargarDisenoAsync(curso.PlantillaId);
        var avisos = new List<(string Email, string Codigo)>();
        foreach (var ins in inscripciones)
        {
            var u = await _db.Usuarios.FindAsync(ins.ParticipanteId);
            if (u == null) continue;

            var codigo = $"FUJ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            var cert = new Certificado
            {
                CodigoUnico = codigo,
                CursoId = cursoId,
                ParticipanteId = u.Id,
                NombreParticipante = u.NombreCompleto,
                NombreCurso = curso.Titulo,
                PlantillaId = curso.PlantillaId
            };
            cert.UrlPdf = await _plantillas.GuardarPdfAsync(diseno,
                new DatosCertificado(u.NombreCompleto, curso.Titulo, cert.EmitidoEn, codigo), cursoId, cert.Id);
            _db.Certificados.Add(cert);
            avisos.Add((u.Email, codigo));
        }

        curso.Estado = "FINALIZADO";
        await _db.SaveChangesAsync();

        // Se avisa después de guardar: si algo falla al emitir, nadie recibe un código que no existe
        foreach (var (email, codigo) in avisos)
            await _email.EnviarAsync(email, "¡Felicidades! Tu certificado está listo",
                $"Código de verificación: {codigo}");
        return avisos.Count;
    }
}