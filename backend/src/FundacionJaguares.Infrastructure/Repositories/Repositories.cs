using FundacionJaguares.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FundacionJaguares.Infrastructure.Repositories;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<List<T>> GetAllAsync();
    Task AddAsync(T e);
    void Update(T e);
    void Remove(T e);
    Task<int> SaveChangesAsync();
}

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Db;
    protected readonly DbSet<T> Set;
    public Repository(AppDbContext db) { Db = db; Set = db.Set<T>(); }
    public Task<T?> GetByIdAsync(Guid id) => Set.FindAsync(id).AsTask();
    public Task<List<T>> GetAllAsync() => Set.ToListAsync();
    public Task AddAsync(T e) => Set.AddAsync(e).AsTask();
    public void Update(T e) => Set.Update(e);
    public void Remove(T e) => Set.Remove(e);
    public Task<int> SaveChangesAsync() => Db.SaveChangesAsync();
}

public class CampanaRepository : Repository<Campana>
{
    public CampanaRepository(AppDbContext db) : base(db) { }

    public Task<List<Campana>> ListarAsync(bool soloVigentes, DateOnly hoy) =>
        Set.Where(c => !soloVigentes || (c.Activa && c.FechaLimite >= hoy))
           .OrderBy(c => c.FechaLimite).ThenBy(c => c.Nombre).ToListAsync();

    // Solo cuentan las donaciones confirmadas: una anulada deja de sumar a la barra
    public async Task<Dictionary<Guid, (decimal Total, int Cantidad)>> TotalesAsync(IReadOnlyCollection<Guid> ids)
    {
        var filas = await Db.Donaciones
            .Where(d => d.CampanaId != null && ids.Contains(d.CampanaId.Value) && d.Estado == "CONFIRMADA")
            .GroupBy(d => d.CampanaId!.Value)
            .Select(g => new { Id = g.Key, Total = g.Sum(d => d.Monto), Cantidad = g.Count() })
            .ToListAsync();
        return filas.ToDictionary(f => f.Id, f => (f.Total, f.Cantidad));
    }
}

public class CursoRepository : Repository<Curso>
{
    public CursoRepository(AppDbContext db) : base(db) { }
    public Task<List<Curso>> GetPublicadosAsync() =>
        Set.Where(c => c.Estado == "PUBLICADO" || c.Estado == "EN_CURSO").ToListAsync();
    public Task<bool> ExisteInscripcionAsync(Guid cursoId, Guid participanteId) =>
        Db.Inscripciones.AnyAsync(i => i.CursoId == cursoId && i.ParticipanteId == participanteId);

    public async Task<bool> ReservarCupoAsync(Guid cursoId)
    {
        var filas = await Db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE cursos SET cupo_disponible = cupo_disponible - 1 WHERE id = {cursoId} AND cupo_disponible > 0");
        return filas > 0;
    }

    public async Task LiberarCupoAsync(Guid cursoId)
    {
        await Db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE cursos SET cupo_disponible = cupo_disponible + 1 WHERE id = {cursoId} AND cupo_disponible < cupo_maximo");
    }

    // FUJ-111: bloquea la fila del curso hasta el fin de la transacción actual. Serializa las
    // cancelaciones y las altas en la lista de espera del mismo curso, para que un cupo liberado
    // no se pierda ni se entregue dos veces.
    public Task BloquearAsync(Guid cursoId) =>
        Db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM cursos WHERE id = {cursoId} FOR UPDATE");
}

// FUJ-111: lista de espera de cursos llenos
public class ListaEsperaRepository : Repository<ListaEspera>
{
    public ListaEsperaRepository(AppDbContext db) : base(db) { }

    private IQueryable<ListaEspera> EnEspera(Guid cursoId) =>
        Set.Where(l => l.CursoId == cursoId && l.Estado == "EN_ESPERA");

    public Task<ListaEspera?> GetActivaAsync(Guid cursoId, Guid participanteId) =>
        EnEspera(cursoId).FirstOrDefaultAsync(l => l.ParticipanteId == participanteId);

    // Cola en orden de llegada (el Id desempata registros creados en el mismo instante)
    public Task<List<ListaEspera>> ColaAsync(Guid cursoId) =>
        EnEspera(cursoId).OrderBy(l => l.CreadoEn).ThenBy(l => l.Id).ToListAsync();

    public Task<List<ListaEspera>> ColasAsync(IReadOnlyCollection<Guid> cursoIds) =>
        Set.Where(l => cursoIds.Contains(l.CursoId) && l.Estado == "EN_ESPERA")
           .OrderBy(l => l.CreadoEn).ThenBy(l => l.Id).ToListAsync();

    public Task<List<Guid>> CursosDeAsync(Guid participanteId) =>
        Set.Where(l => l.ParticipanteId == participanteId && l.Estado == "EN_ESPERA")
           .Select(l => l.CursoId).ToListAsync();

    public Task<int> RetirarTodosAsync(Guid cursoId) =>
        EnEspera(cursoId).ExecuteUpdateAsync(s => s.SetProperty(l => l.Estado, "RETIRADO"));
}

public class EventoRepository : Repository<Evento>
{
    public EventoRepository(AppDbContext db) : base(db) { }
    public Task<List<Evento>> GetPublicadosAsync() =>
        Set.Where(e => e.Estado == "PUBLICADO" || e.Estado == "EN_CURSO").ToListAsync();
}

public class CertificadoRepository : Repository<Certificado>
{
    public CertificadoRepository(AppDbContext db) : base(db) { }
    public Task<Certificado?> GetByCodigoAsync(string codigo) =>
        Set.FirstOrDefaultAsync(c => c.CodigoUnico == codigo);
    public Task<List<Certificado>> GetByParticipanteAsync(Guid participanteId) =>
        Set.Where(c => c.ParticipanteId == participanteId).OrderByDescending(c => c.EmitidoEn).ToListAsync();
}

public class DonacionRepository : Repository<Donacion>
{
    public DonacionRepository(AppDbContext db) : base(db) { }
    public Task<List<Donacion>> GetByPeriodoAsync(int anio, int mes) =>
        Set.Where(d => d.Fecha.Year == anio && d.Fecha.Month == mes).ToListAsync();
    public async Task<decimal> TotalPeriodoAsync(int anio, int mes) =>
        await Set.Where(d => d.Estado == "CONFIRMADA" && d.Fecha.Year == anio && d.Fecha.Month == mes)
                 .SumAsync(d => (decimal?)d.Monto) ?? 0m;
    public Task<List<Donacion>> GetByRegistradorAsync(Guid usuarioId) =>
        Set.Where(d => d.RegistradoPor == usuarioId).OrderByDescending(d => d.Fecha).Take(50).ToListAsync();
}

public class VoluntarioRepository : Repository<Voluntario>
{
    public VoluntarioRepository(AppDbContext db) : base(db) { }
    public Task<Voluntario?> GetByEmailAsync(string email) =>
        Set.FirstOrDefaultAsync(v => v.Email == email);
}

public class ContenidoRepository : Repository<Noticia>
{
    public ContenidoRepository(AppDbContext db) : base(db) { }
    public Task<List<Noticia>> GetPublicadasAsync() =>
        Set.Where(n => n.Publicada).OrderByDescending(n => n.PublicadoEn).ToListAsync();
}