using FundacionJaguares.Application.DTOs;
using FundacionJaguares.Domain.Entities;
using FundacionJaguares.Infrastructure.Adapters;
using FundacionJaguares.Infrastructure.Repositories;

namespace FundacionJaguares.Application.Services;

public interface IDonacionService
{
    Task<Donacion> RegistrarAsync(CrearDonacionDto dto, Guid? registradoPor);
    Task<Donacion> RegistrarDonacionEnLineaAsync(CrearDonacionDto dto);
    Task<decimal> TotalPeriodoAsync(int anio, int mes);
    Task<List<Donacion>> ListarPeriodoAsync(int anio, int mes);
}

public class DonacionService : IDonacionService
{
    private readonly DonacionRepository _donaciones;
    private readonly IPaymentAdapter _payment;

    public DonacionService(DonacionRepository donaciones, IPaymentAdapter payment)
    { _donaciones = donaciones; _payment = payment; }

    public async Task<Donacion> RegistrarAsync(CrearDonacionDto dto, Guid? registradoPor)
    {
        if (!Enum.TryParse<MedioPago>(dto.Medio, true, out var medio))
            throw new ArgumentException("Medio de pago inválido");

        var d = new Donacion
        {
            Monto = dto.Monto,
            Medio = medio,
            Referencia = dto.Referencia,
            DonanteId = dto.DonanteId,
            RegistradoPor = registradoPor,
            Estado = "CONFIRMADA"
        };
        await _donaciones.AddAsync(d);
        await _donaciones.SaveChangesAsync();
        return d;
    }

    public async Task<Donacion> RegistrarDonacionEnLineaAsync(CrearDonacionDto dto)
    {
        var ref_ = dto.Referencia ?? Guid.NewGuid().ToString("N");
        var ok = await _payment.ProcesarPagoAsync(dto.Monto, ref_);
        if (!ok) throw new InvalidOperationException("Pago rechazado");

        var d = new Donacion
        {
            Monto = dto.Monto,
            Medio = MedioPago.PASARELA,
            Referencia = ref_,
            DonanteId = dto.DonanteId,
            Estado = "CONFIRMADA"
        };
        await _donaciones.AddAsync(d);
        await _donaciones.SaveChangesAsync();
        return d;
    }

    public Task<decimal> TotalPeriodoAsync(int anio, int mes) => _donaciones.TotalPeriodoAsync(anio, mes);
    public Task<List<Donacion>> ListarPeriodoAsync(int anio, int mes) => _donaciones.GetByPeriodoAsync(anio, mes);
}