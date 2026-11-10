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
    // FUJ-111: lista de espera
    Task<ListaEsperaDto> UnirseListaEsperaAsync(Guid cursoId, Guid participanteId);
    Task SalirListaEsperaAsync(Guid cursoId, Guid participanteId);
    Task<List<ListaEsperaDto>> MiListaEsperaAsync(Guid participanteId);
    Task<List<EnEsperaGestionDto>> ListarEsperaGestionAsync(Guid cursoId, Guid? docenteId);
    Task<List<CursoGestionDto>> ListarGestionAsync(Guid? docenteId);
    // FUJ-120: al finalizar se envía la encuesta de satisfacción (preguntas == null: las de por defecto)
    Task<FinalizarCursoResultadoDto> FinalizarYGenerarCertificadosAsync(Guid cursoId, Guid? docenteId,
        Guid usuarioId, List<PreguntaEncuestaDto>? preguntas = null);
    Task AsignarPlantillaAsync(Guid cursoId, Guid? plantillaId, Guid? docenteId);
}

public class CursoService : ICursoService
{
    private readonly CursoRepository _cursos;
    private readonly CertificadoRepository _certs;
    private readonly AppDbContext _db;
    private readonly IEmailAdapter _email;
    private readonly IPlantillaService _plantillas;
    private readonly ListaEsperaRepository _espera;
    private readonly IEncuestaService _encuestas;

    private static readonly string[] EstadosAbiertos = ["PUBLICADO", "EN_CURSO"];

    public CursoService(CursoRepository cursos, CertificadoRepository certs, AppDbContext db, IEmailAdapter email,
                        IPlantillaService plantillas, ListaEsperaRepository espera, IEncuestaService encuestas)
    {
        _cursos = cursos; _certs = certs; _db = db; _email = email; _plantillas = plantillas; _espera = espera;
        _encuestas = encuestas;
    }

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
        if (!EstadosAbiertos.Contains(curso.Estado))
            throw new InvalidOperationException("El curso no está abierto a inscripciones");

        if (!await _cursos.ReservarCupoAsync(cursoId))
            throw new InvalidOperationException("No hay cupos disponibles. Puedes unirte a la lista de espera.");

        var ins = new Inscripcion { CursoId = cursoId, ParticipanteId = participanteId };
        _db.Inscripciones.Add(ins);

        // Si estaba en la lista de espera y consiguió cupo por su cuenta, deja la cola
        var enEspera = await _espera.GetActivaAsync(cursoId, participanteId);
        if (enEspera is not null) { enEspera.Estado = "PROMOVIDO"; enEspera.PromovidoEn = DateTime.UtcNow; }
        await _db.SaveChangesAsync();

        var u = await _db.Usuarios.FindAsync(participanteId);
        if (u != null)
            await _email.EnviarAsync(u.Email, "Inscripción confirmada",
                $"Hola {u.NombreCompleto}, te inscribiste en '{curso.Titulo}'.");

        return ins;
    }

    // Al cancelar, el cupo pasa directamente al primero de la lista de espera (FUJ-111).
    // Si la lista está vacía, el cupo vuelve a quedar disponible.
    public async Task CancelarInscripcionAsync(Guid cursoId, Guid participanteId)
    {
        var curso = await _cursos.GetByIdAsync(cursoId)
                    ?? throw new KeyNotFoundException("Curso no encontrado");
        if (!EstadosAbiertos.Contains(curso.Estado))
            throw new InvalidOperationException("El curso ya finalizó: la inscripción no se puede cancelar");

        User? promovido;
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            await _cursos.BloquearAsync(cursoId);
            var ins = await _db.Inscripciones.FirstOrDefaultAsync(i =>
                i.CursoId == cursoId && i.ParticipanteId == participanteId && i.Estado == "CONFIRMADA")
                ?? throw new KeyNotFoundException("Inscripción no encontrada");
            _db.Inscripciones.Remove(ins);
            await _db.SaveChangesAsync();

            promovido = await PromoverPrimeroAsync(cursoId);
            if (promovido is null) await _cursos.LiberarCupoAsync(cursoId);
            await tx.CommitAsync();
        }

        // Se notifica después del commit: nadie recibe el aviso de un cupo que no se confirmó
        if (promovido is not null)
            await _email.EnviarAsync(promovido.Email, $"¡Se liberó un cupo! Ya estás inscrito/a en {curso.Titulo}",
                $"Hola {promovido.NombreCompleto}, se liberó un cupo en '{curso.Titulo}' " +
                $"({curso.FechaInicio:dd/MM/yyyy} al {curso.FechaFin:dd/MM/yyyy}) y, como eras el primero/a de la lista de espera, " +
                "quedaste inscrito/a automáticamente. Si ya no puedes asistir, cancela tu inscripción desde \"Mi panel\" para liberar el cupo.");
    }

    // Debe llamarse con el curso bloqueado. Salta a quien ya no puede recibir el cupo
    // (cuenta desactivada o ya inscrito por otra vía) y sigue con el siguiente de la cola.
    private async Task<User?> PromoverPrimeroAsync(Guid cursoId)
    {
        foreach (var lugar in await _espera.ColaAsync(cursoId))
        {
            var u = await _db.Usuarios.FindAsync(lugar.ParticipanteId);
            if (u is null || !u.Activo || await _cursos.ExisteInscripcionAsync(cursoId, lugar.ParticipanteId))
            {
                lugar.Estado = "RETIRADO";
                continue;
            }

            lugar.Estado = "PROMOVIDO";
            lugar.PromovidoEn = DateTime.UtcNow;
            _db.Inscripciones.Add(new Inscripcion { CursoId = cursoId, ParticipanteId = u.Id });
            await _db.SaveChangesAsync();
            return u;
        }
        await _db.SaveChangesAsync();
        return null;
    }

    // ---------- Lista de espera (FUJ-111) ----------

    public async Task<ListaEsperaDto> UnirseListaEsperaAsync(Guid cursoId, Guid participanteId)
    {
        var curso = await _cursos.GetByIdAsync(cursoId)
                    ?? throw new KeyNotFoundException("Curso no encontrado");
        if (!EstadosAbiertos.Contains(curso.Estado))
            throw new InvalidOperationException("El curso no está abierto a inscripciones");
        if (curso.FechaFin < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new InvalidOperationException("El curso ya terminó");

        ListaEspera lugar;
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            // Con el curso bloqueado, ninguna cancelación puede liberar un cupo mientras se decide
            await _cursos.BloquearAsync(cursoId);
            if (await _cursos.ExisteInscripcionAsync(cursoId, participanteId))
                throw new InvalidOperationException("Ya estás inscrito en este curso");
            if (await _espera.GetActivaAsync(cursoId, participanteId) is not null)
                throw new InvalidOperationException("Ya estás en la lista de espera de este curso");

            var cupo = await _db.Cursos.Where(c => c.Id == cursoId).Select(c => c.CupoDisponible).FirstAsync();
            if (cupo > 0)
                throw new InvalidOperationException("Este curso todavía tiene cupos: inscríbete directamente");

            lugar = new ListaEspera { CursoId = cursoId, ParticipanteId = participanteId };
            _db.ListaEspera.Add(lugar);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        var dto = ADto(curso, lugar, await _espera.ColaAsync(cursoId));

        var u = await _db.Usuarios.FindAsync(participanteId);
        if (u != null)
            await _email.EnviarAsync(u.Email, $"Estás en la lista de espera: {curso.Titulo}",
                $"Hola {u.NombreCompleto}, te anotaste en la lista de espera de '{curso.Titulo}'. " +
                $"Tu posición actual es la {dto.Posicion}. Si alguien cancela, el cupo se asigna automáticamente " +
                "por orden de llegada y te avisaremos por este medio.");
        return dto;
    }

    public async Task SalirListaEsperaAsync(Guid cursoId, Guid participanteId)
    {
        var lugar = await _espera.GetActivaAsync(cursoId, participanteId)
                    ?? throw new KeyNotFoundException("No estás en la lista de espera de este curso");
        lugar.Estado = "RETIRADO";
        await _db.SaveChangesAsync();
    }

    public async Task<List<ListaEsperaDto>> MiListaEsperaAsync(Guid participanteId)
    {
        var ids = await _espera.CursosDeAsync(participanteId);
        if (ids.Count == 0) return [];

        var cursos = await _db.Cursos.Where(c => ids.Contains(c.Id) && EstadosAbiertos.Contains(c.Estado)).ToListAsync();
        var colas = (await _espera.ColasAsync(ids)).GroupBy(l => l.CursoId).ToDictionary(g => g.Key, g => g.ToList());

        return cursos
            .Select(c => ADto(c, colas[c.Id].First(l => l.ParticipanteId == participanteId), colas[c.Id]))
            .OrderBy(d => d.FechaInicio)
            .ToList();
    }

    // docenteId == null: la directora ve la lista de cualquier curso
    public async Task<List<EnEsperaGestionDto>> ListarEsperaGestionAsync(Guid cursoId, Guid? docenteId)
    {
        var curso = await _cursos.GetByIdAsync(cursoId)
                    ?? throw new KeyNotFoundException("Curso no encontrado");
        if (docenteId != null && curso.DocenteId != docenteId)
            throw new UnauthorizedAccessException("Solo puedes ver tus propios cursos");

        var cola = await _espera.ColaAsync(cursoId);
        var ids = cola.Select(l => l.ParticipanteId).ToList();
        var usuarios = await _db.Usuarios.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        return cola.Select((l, i) => usuarios.TryGetValue(l.ParticipanteId, out var u)
                ? new EnEsperaGestionDto(i + 1, u.NombreCompleto, u.Email, l.CreadoEn)
                : new EnEsperaGestionDto(i + 1, "(usuario eliminado)", "", l.CreadoEn))
            .ToList();
    }

    private static ListaEsperaDto ADto(Curso c, ListaEspera lugar, List<ListaEspera> cola) =>
        new(c.Id, c.Titulo, c.FechaInicio, c.FechaFin,
            cola.FindIndex(l => l.Id == lugar.Id) + 1, cola.Count, lugar.CreadoEn);

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
                _db.Inscripciones.Count(i => i.CursoId == c.Id && i.Estado == "CONFIRMADA"), c.PlantillaId,
                _db.ListaEspera.Count(l => l.CursoId == c.Id && l.Estado == "EN_ESPERA")))
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
    public async Task<FinalizarCursoResultadoDto> FinalizarYGenerarCertificadosAsync(Guid cursoId, Guid? docenteId,
        Guid usuarioId, List<PreguntaEncuestaDto>? preguntas = null)
    {
        // Se validan antes de cerrar: un curso no puede quedar finalizado con una encuesta inválida
        var preguntasValidas = EncuestaService.ValidarPreguntas(preguntas);
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
        var asistentes = new List<Guid>();
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
            asistentes.Add(u.Id);
        }

        EncuestaPreparada encuesta;
        // Certificados, cierre de la lista de espera y encuesta en una sola transacción
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            curso.Estado = "FINALIZADO";
            await _db.SaveChangesAsync();
            // FUJ-111: con el curso cerrado, la lista de espera ya no puede recibir cupos
            await _espera.RetirarTodosAsync(cursoId);
            // FUJ-120: los asistentes (quienes reciben certificado) reciben el enlace a la encuesta
            encuesta = await _encuestas.PrepararAsync("CURSO", cursoId, curso.Titulo, asistentes, preguntasValidas, usuarioId);
            await tx.CommitAsync();
        }

        // Se avisa después de guardar: si algo falla al emitir, nadie recibe un código que no existe
        foreach (var (email, codigo) in avisos)
            await _email.EnviarAsync(email, "¡Felicidades! Tu certificado está listo",
                $"Código de verificación: {codigo}");

        await _encuestas.NotificarAsync(encuesta);
        return new FinalizarCursoResultadoDto(avisos.Count, encuesta.Invitados.Count, encuesta.EncuestaId);
    }
}