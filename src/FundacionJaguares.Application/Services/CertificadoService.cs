using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Infrastructure.Repositories;

namespace FundacionJaguares.Application.Services;

public interface ICertificadoService
{
    Task<CertificadoVerificacionDto> VerificarAsync(string codigoUnico);
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
}