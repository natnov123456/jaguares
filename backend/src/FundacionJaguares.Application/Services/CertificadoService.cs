using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Infrastructure.Repositories;

namespace FundacionJaguares.Application.Services;

public interface ICertificadoService
{
    Task<CertificadoVerificacionDto> VerificarAsync(string codigoUnico);
    Task<List<CertificadoDto>> ListarPorParticipanteAsync(Guid participanteId);
}

public class CertificadoService : ICertificadoService
{
    private readonly CertificadoRepository _certs;
    public CertificadoService(CertificadoRepository certs) => _certs = certs;

    public async Task<CertificadoVerificacionDto> VerificarAsync(string codigoUnico)
    {
        var c = await _certs.GetByCodigoAsync(codigoUnico);
        if (c == null || !c.Valido)
            return new CertificadoVerificacionDto(false, null, null, null);
        return new CertificadoVerificacionDto(true, c.NombreParticipante, c.NombreCurso, c.EmitidoEn);
    }

    public async Task<List<CertificadoDto>> ListarPorParticipanteAsync(Guid participanteId) =>
        (await _certs.GetByParticipanteAsync(participanteId))
            .Select(c => new CertificadoDto(c.Id, c.CodigoUnico, c.NombreCurso, c.EmitidoEn, c.Valido))
            .ToList();
}