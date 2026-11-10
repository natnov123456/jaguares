using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Infrastructure;
using FundacionJaguares.Infrastructure.Adapters;
using FundacionJaguares.Infrastructure.Repositories;

namespace FundacionJaguares.Application.Services;

public interface ICertificadoService
{
    Task<CertificadoVerificacionDto> VerificarAsync(string codigoUnico);
    Task<List<CertificadoDto>> ListarPorParticipanteAsync(Guid participanteId);
    Task<ComprobanteDto> DescargarPdfAsync(Guid certificadoId, Guid participanteId);
}

public class CertificadoService : ICertificadoService
{
    private readonly CertificadoRepository _certs;
    private readonly AppDbContext _db;
    private readonly IPlantillaService _plantillas;
    private readonly IAlmacenamientoArchivos _archivos;

    public CertificadoService(CertificadoRepository certs, AppDbContext db, IPlantillaService plantillas,
                              IAlmacenamientoArchivos archivos)
    { _certs = certs; _db = db; _plantillas = plantillas; _archivos = archivos; }

    public async Task<CertificadoVerificacionDto> VerificarAsync(string codigoUnico)
    {
        var c = await _certs.GetByCodigoAsync(codigoUnico);
        if (c == null || !c.Valido)
            return new CertificadoVerificacionDto(false, null, null, null);
        return new CertificadoVerificacionDto(true, c.NombreParticipante, c.NombreCurso, c.EmitidoEn);
    }

    public async Task<List<CertificadoDto>> ListarPorParticipanteAsync(Guid participanteId) =>
        (await _certs.GetByParticipanteAsync(participanteId))
            .Select(c => new CertificadoDto(c.Id, c.CodigoUnico, c.NombreCurso, c.EmitidoEn, c.Valido, c.UrlPdf))
            .ToList();

    // FUJ-112: descarga desde "Mi panel". Solo el dueño puede bajarlo y solo si sigue siendo válido.
    // Los certificados emitidos antes de FUJ-115 no tienen PDF guardado (o el archivo se perdió):
    // en ese caso se genera con la plantilla del certificado y se guarda para las próximas descargas.
    public async Task<ComprobanteDto> DescargarPdfAsync(Guid certificadoId, Guid participanteId)
    {
        var c = await _certs.GetByIdAsync(certificadoId);
        if (c is null || c.ParticipanteId != participanteId)
            throw new KeyNotFoundException("Certificado no encontrado");
        if (!c.Valido)
            throw new InvalidOperationException("Este certificado fue anulado y no se puede descargar");

        var nombre = $"certificado-{c.CodigoUnico}.pdf";
        var pdf = await _archivos.LeerAsync(c.UrlPdf);
        if (pdf is not null) return new ComprobanteDto(nombre, pdf);

        var diseno = await _plantillas.CargarDisenoAsync(c.PlantillaId);
        pdf = GeneradorCertificadoPdf.Generar(diseno,
            new DatosCertificado(c.NombreParticipante, c.NombreCurso, c.EmitidoEn, c.CodigoUnico));
        try
        {
            using var datos = new MemoryStream(pdf);
            var carpeta = $"certificados/{(c.CursoId ?? Guid.Empty):N}";
            c.UrlPdf = await _archivos.GuardarAsync(carpeta, $"{c.Id:N}-{Guid.NewGuid():N}.pdf", datos);
            await _db.SaveChangesAsync();
        }
        catch (IOException) { /* no se pudo guardar: igual se entrega el PDF generado */ }
        return new ComprobanteDto(nombre, pdf);
    }
}
