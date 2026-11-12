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
    Task<Curso> CrearAsync(CrearCursoDto dto);
    Task<List<DocenteDto>> ListarDocentesAsync();
    Task AsignarDocenteAsync(Guid cursoId, Guid? docenteId);
    // docenteId == null en los métodos de gestión: la directora puede operar sobre cualquier curso
    Task<CursoDetalleDto> DetalleAsync(Guid cursoId, Guid? docenteId);
    Task<CursoDetalleDto> EditarAsync(Guid cursoId, EditarCursoDto dto, Guid? docenteId);
    Task<AsistenciaDiaDto> AsistenciaDiaAsync(Guid cursoId, DateOnly fecha, Guid? docenteId);
    Task<AsistenciaDiaDto> RegistrarAsistenciaAsync(Guid cursoId, RegistrarAsistenciaCursoDto dto, Guid? docenteId, Guid registradoPor);
    Task<MiAsistenciaCursoDto> MiAsistenciaAsync(Guid cursoId, Guid participanteId);
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

    // Solo la directora crea cursos; el docente a cargo es obligatorio
    public async Task<Curso> CrearAsync(CrearCursoDto dto)
    {
        ValidarDatos(dto.Titulo, dto.FechaInicio, dto.FechaFin, dto.CupoMaximo, dto.AsistenciaMinimaPct);
        if (dto.DocenteId is null)
            throw new ArgumentException("Selecciona el docente a cargo del curso");
        await ValidarDocenteAsync(dto.DocenteId.Value);
        await ValidarPlantillaAsync(dto.PlantillaId);

        var c = new Curso
        {
            Titulo = dto.Titulo.Trim(), Descripcion = dto.Descripcion,
            DocenteId = dto.DocenteId,
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
                _db.ListaEspera.Count(l => l.CursoId == c.Id && l.Estado == "EN_ESPERA"),
                c.DocenteId,
                _db.Usuarios.Where(u => u.Id == c.DocenteId).Select(u => u.NombreCompleto).FirstOrDefault()))
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

        // Si se tomó asistencia, solo se certifica a quien alcanza el mínimo del curso
        var (diasCurso, presentes) = await ConteoAsistenciaAsync(cursoId);
        if (diasCurso > 0)
            inscripciones = inscripciones
                .Where(i => Porcentaje(presentes.GetValueOrDefault(i.ParticipanteId), diasCurso) >= curso.AsistenciaMinimaPct)
                .ToList();

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

    // ---------- Docente a cargo ----------

    public Task<List<DocenteDto>> ListarDocentesAsync() =>
        _db.Usuarios.Where(u => u.Rol == Rol.DOCENTE && u.Activo)
            .OrderBy(u => u.NombreCompleto)
            .Select(u => new DocenteDto(u.Id, u.NombreCompleto, u.Email))
            .ToListAsync();

    public async Task AsignarDocenteAsync(Guid cursoId, Guid? docenteId)
    {
        var curso = await _cursos.GetByIdAsync(cursoId)
                    ?? throw new KeyNotFoundException("Curso no encontrado");
        if (docenteId is null) throw new ArgumentException("Selecciona el docente a cargo del curso");
        await ValidarDocenteAsync(docenteId.Value);
        if (curso.DocenteId == docenteId) return;

        curso.DocenteId = docenteId;
        await _db.SaveChangesAsync();

        var docente = await _db.Usuarios.FindAsync(docenteId.Value);
        if (docente != null)
            await _email.EnviarAsync(docente.Email, $"Te asignaron el curso: {curso.Titulo}",
                $"Hola {docente.NombreCompleto}, la dirección te asignó como docente de '{curso.Titulo}' " +
                $"({curso.FechaInicio:dd/MM/yyyy} al {curso.FechaFin:dd/MM/yyyy}). " +
                "Desde \"Mis cursos\" puedes ver a los inscritos, registrar la asistencia y editar los datos del curso.");
    }

    private async Task ValidarDocenteAsync(Guid docenteId)
    {
        if (!await _db.Usuarios.AnyAsync(u => u.Id == docenteId && u.Rol == Rol.DOCENTE && u.Activo))
            throw new ArgumentException("El docente seleccionado no existe o su cuenta está desactivada");
    }

    // ---------- Detalle y edición ----------

    private async Task<Curso> CursoGestionableAsync(Guid cursoId, Guid? docenteId)
    {
        var curso = await _cursos.GetByIdAsync(cursoId)
                    ?? throw new KeyNotFoundException("Curso no encontrado");
        if (docenteId != null && curso.DocenteId != docenteId)
            throw new UnauthorizedAccessException("Solo puedes gestionar los cursos que te asignaron");
        return curso;
    }

    public async Task<CursoDetalleDto> DetalleAsync(Guid cursoId, Guid? docenteId)
    {
        var c = await CursoGestionableAsync(cursoId, docenteId);
        var docente = c.DocenteId is null ? null
            : await _db.Usuarios.Where(u => u.Id == c.DocenteId).Select(u => u.NombreCompleto).FirstOrDefaultAsync();
        var inscritos = await _db.Inscripciones.CountAsync(i => i.CursoId == cursoId && i.Estado == "CONFIRMADA");
        var dias = await _db.AsistenciasCurso.Where(a => a.CursoId == cursoId)
            .Select(a => a.Fecha).Distinct().OrderBy(f => f).ToListAsync();
        return new CursoDetalleDto(c.Id, c.Titulo, c.Descripcion, c.FechaInicio, c.FechaFin, c.CupoMaximo,
            c.CupoDisponible, c.AsistenciaMinimaPct, c.Estado, c.DocenteId, docente, inscritos, dias);
    }

    public async Task<CursoDetalleDto> EditarAsync(Guid cursoId, EditarCursoDto dto, Guid? docenteId)
    {
        var curso = await CursoGestionableAsync(cursoId, docenteId);
        if (!EstadosAbiertos.Contains(curso.Estado))
            throw new InvalidOperationException("El curso ya está cerrado: no se puede modificar");
        ValidarDatos(dto.Titulo, curso.FechaInicio, curso.FechaFin, dto.CupoMaximo, dto.AsistenciaMinimaPct);

        var promovidos = new List<User>();
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            // Con el curso bloqueado, nadie se inscribe ni cancela mientras se recalcula el cupo
            await _cursos.BloquearAsync(cursoId);
            var inscritos = await _db.Inscripciones.CountAsync(i => i.CursoId == cursoId && i.Estado == "CONFIRMADA");
            if (dto.CupoMaximo < inscritos)
                throw new ArgumentException($"El cupo no puede ser menor a los {inscritos} inscritos actuales");

            curso.Titulo = dto.Titulo.Trim();
            curso.Descripcion = dto.Descripcion;
            curso.AsistenciaMinimaPct = dto.AsistenciaMinimaPct;
            curso.CupoMaximo = dto.CupoMaximo;
            await _db.SaveChangesAsync();

            // Si se amplió el cupo, los nuevos lugares pasan primero a la lista de espera (FUJ-111)
            var libres = dto.CupoMaximo - inscritos;
            while (libres > 0 && await PromoverPrimeroAsync(cursoId) is { } u) { promovidos.Add(u); libres--; }
            curso.CupoDisponible = libres;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        foreach (var u in promovidos)
            await _email.EnviarAsync(u.Email, $"¡Se liberó un cupo! Ya estás inscrito/a en {curso.Titulo}",
                $"Hola {u.NombreCompleto}, se amplió el cupo de '{curso.Titulo}' " +
                $"({curso.FechaInicio:dd/MM/yyyy} al {curso.FechaFin:dd/MM/yyyy}) y, como estabas en la lista de espera, " +
                "quedaste inscrito/a automáticamente.");

        return await DetalleAsync(cursoId, docenteId);
    }

    private static void ValidarDatos(string titulo, DateOnly inicio, DateOnly fin, int cupo, int asistenciaMinima)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new ArgumentException("El título es obligatorio");
        if (fin < inicio)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio");
        if (cupo <= 0)
            throw new ArgumentException("El cupo máximo debe ser mayor a 0");
        if (asistenciaMinima is < 0 or > 100)
            throw new ArgumentException("La asistencia mínima debe estar entre 0 y 100 %");
    }

    // ---------- Asistencia ----------

    private static double Porcentaje(int presentes, int dias) =>
        dias == 0 ? 0 : Math.Round(presentes * 100.0 / dias, 1);

    // Días en que se tomó asistencia en el curso y días presente de cada participante
    private async Task<(int Dias, Dictionary<Guid, int> Presentes)> ConteoAsistenciaAsync(Guid cursoId)
    {
        var dias = await _db.AsistenciasCurso.Where(a => a.CursoId == cursoId).Select(a => a.Fecha).Distinct().CountAsync();
        var presentes = await _db.AsistenciasCurso.Where(a => a.CursoId == cursoId && a.Presente)
            .GroupBy(a => a.ParticipanteId)
            .Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.N);
        return (dias, presentes);
    }

    public async Task<AsistenciaDiaDto> AsistenciaDiaAsync(Guid cursoId, DateOnly fecha, Guid? docenteId)
    {
        var curso = await CursoGestionableAsync(cursoId, docenteId);
        var inscritos = await (from i in _db.Inscripciones
                               join u in _db.Usuarios on i.ParticipanteId equals u.Id
                               where i.CursoId == cursoId && i.Estado == "CONFIRMADA"
                               orderby u.NombreCompleto
                               select new { u.Id, u.NombreCompleto, u.Email }).ToListAsync();
        var delDia = await _db.AsistenciasCurso.Where(a => a.CursoId == cursoId && a.Fecha == fecha)
            .ToDictionaryAsync(a => a.ParticipanteId, a => a.Presente);
        var (dias, presentes) = await ConteoAsistenciaAsync(cursoId);

        var lista = inscritos.Select(u =>
        {
            var p = presentes.GetValueOrDefault(u.Id);
            return new AsistenciaParticipanteDto(u.Id, u.NombreCompleto, u.Email,
                delDia.TryGetValue(u.Id, out var presente) ? presente : null,
                p, dias, dias == 0 ? null : Porcentaje(p, dias));
        }).ToList();
        return new AsistenciaDiaDto(fecha, curso.AsistenciaMinimaPct, lista);
    }

    // Guarda la asistencia de un día y avisa a cada participante con la fecha y su porcentaje.
    // Se puede corregir el mismo día: solo se avisa a quienes les cambió el registro.
    public async Task<AsistenciaDiaDto> RegistrarAsistenciaAsync(Guid cursoId, RegistrarAsistenciaCursoDto dto,
        Guid? docenteId, Guid registradoPor)
    {
        var curso = await CursoGestionableAsync(cursoId, docenteId);
        if (!EstadosAbiertos.Contains(curso.Estado))
            throw new InvalidOperationException("El curso ya está cerrado: no se puede registrar asistencia");
        if (dto.Fecha < curso.FechaInicio || dto.Fecha > curso.FechaFin)
            throw new ArgumentException($"La fecha debe estar entre el {curso.FechaInicio:dd/MM/yyyy} y el {curso.FechaFin:dd/MM/yyyy}");
        if (dto.Fecha > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("No se puede registrar asistencia de un día que todavía no llegó");
        if (dto.Registros is null || dto.Registros.Count == 0)
            throw new ArgumentException("No hay participantes para registrar");

        var inscritos = (await _db.Inscripciones
                .Where(i => i.CursoId == cursoId && i.Estado == "CONFIRMADA")
                .Select(i => i.ParticipanteId).ToListAsync())
            .ToHashSet();
        if (dto.Registros.Any(r => !inscritos.Contains(r.ParticipanteId)))
            throw new ArgumentException("Uno de los participantes no está inscrito en este curso");

        var existentes = await _db.AsistenciasCurso.Where(a => a.CursoId == cursoId && a.Fecha == dto.Fecha)
            .ToDictionaryAsync(a => a.ParticipanteId);
        var cambiaron = new HashSet<Guid>();
        foreach (var r in dto.Registros.DistinctBy(r => r.ParticipanteId))
        {
            if (existentes.TryGetValue(r.ParticipanteId, out var a))
            {
                if (a.Presente == r.Presente) continue;
                a.Presente = r.Presente;
                a.RegistradoPor = registradoPor;
                a.RegistradoEn = DateTime.UtcNow;
            }
            else
            {
                _db.AsistenciasCurso.Add(new AsistenciaCurso
                {
                    CursoId = cursoId, ParticipanteId = r.ParticipanteId, Fecha = dto.Fecha,
                    Presente = r.Presente, RegistradoPor = registradoPor
                });
            }
            cambiaron.Add(r.ParticipanteId);
        }
        if (curso.Estado == "PUBLICADO") curso.Estado = "EN_CURSO"; // la primera clase marca el inicio
        await _db.SaveChangesAsync();

        var resultado = await AsistenciaDiaAsync(cursoId, dto.Fecha, docenteId);

        // Aviso después de guardar: cada participante ve el día registrado y cómo va su porcentaje
        foreach (var p in resultado.Participantes.Where(p => cambiaron.Contains(p.ParticipanteId)))
            await _email.EnviarAsync(p.Email, $"Asistencia registrada: {curso.Titulo}",
                $"Hola {p.Nombre}, se registró tu asistencia del {dto.Fecha:dd/MM/yyyy} en '{curso.Titulo}': " +
                $"{(p.Presente == true ? "PRESENTE" : "AUSENTE")}. " +
                $"Tu asistencia en el curso es de {p.Porcentaje:0.#} % ({p.DiasPresente} de {p.DiasRegistrados} clases). " +
                $"Para recibir el certificado necesitas al menos {curso.AsistenciaMinimaPct} %. " +
                "Puedes ver el detalle en \"Mi panel\".");

        return resultado;
    }

    public async Task<MiAsistenciaCursoDto> MiAsistenciaAsync(Guid cursoId, Guid participanteId)
    {
        var curso = await _cursos.GetByIdAsync(cursoId)
                    ?? throw new KeyNotFoundException("Curso no encontrado");
        if (!await _db.Inscripciones.AnyAsync(i => i.CursoId == cursoId && i.ParticipanteId == participanteId && i.Estado != "CANCELADA"))
            throw new KeyNotFoundException("No estás inscrito/a en este curso");

        var diasCurso = await _db.AsistenciasCurso.Where(a => a.CursoId == cursoId)
            .Select(a => a.Fecha).Distinct().OrderBy(f => f).ToListAsync();
        var mios = await _db.AsistenciasCurso.Where(a => a.CursoId == cursoId && a.ParticipanteId == participanteId)
            .ToDictionaryAsync(a => a.Fecha, a => a.Presente);

        // Un día sin registro propio (p. ej. se inscribió después) cuenta como ausencia
        var dias = diasCurso.Select(f => new MiDiaAsistenciaDto(f, mios.GetValueOrDefault(f))).ToList();
        var presentes = dias.Count(d => d.Presente);
        return new MiAsistenciaCursoDto(curso.Id, curso.Titulo, curso.AsistenciaMinimaPct, presentes, dias.Count,
            dias.Count == 0 ? null : Porcentaje(presentes, dias.Count), dias);
    }
}