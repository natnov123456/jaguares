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
    Task<int> FinalizarYGenerarCertificadosAsync(Guid cursoId);
}

public class CursoService : ICursoService
{
    private readonly CursoRepository _cursos;
    private readonly CertificadoRepository _certs;
    private readonly AppDbContext _db;
    private readonly IEmailAdapter _email;

    public CursoService(CursoRepository cursos, CertificadoRepository certs, AppDbContext db, IEmailAdapter email)
    { _cursos = cursos; _certs = certs; _db = db; _email = email; }

    public Task<List<Curso>> ListarPublicadosAsync() => _cursos.GetPublicadosAsync();

    public async Task<Curso> CrearAsync(CrearCursoDto dto, Guid docenteId)
    {
        var c = new Curso
        {
            Titulo = dto.Titulo, Descripcion = dto.Descripcion,
            DocenteId = docenteId,
            FechaInicio = dto.FechaInicio, FechaFin = dto.FechaFin,
            CupoMaximo = dto.CupoMaximo, CupoDisponible = dto.CupoMaximo,
            AsistenciaMinimaPct = dto.AsistenciaMinimaPct,
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

    public async Task<int> FinalizarYGenerarCertificadosAsync(Guid cursoId)
    {
        var curso = await _cursos.GetByIdAsync(cursoId)
                    ?? throw new KeyNotFoundException("Curso no encontrado");

        var inscripciones = await _db.Inscripciones
            .Where(i => i.CursoId == cursoId && i.Estado == "CONFIRMADA")
            .ToListAsync();

        int generados = 0;
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
                NombreCurso = curso.Titulo
            };
            _db.Certificados.Add(cert);
            generados++;

            await _email.EnviarAsync(u.Email, "¡Felicidades! Tu certificado está listo",
                $"Código de verificación: {codigo}");
        }

        curso.Estado = "FINALIZADO";
        await _db.SaveChangesAsync();
        return generados;
    }
}