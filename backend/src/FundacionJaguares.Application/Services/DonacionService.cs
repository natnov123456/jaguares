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
    Task<List<Donacion>> ListarRegistradasPorAsync(Guid usuarioId);
}

public class DonacionService : IDonacionService
{
    private readonly DonacionRepository _donaciones;
    private readonly IPaymentAdapter _payment;

    public DonacionService(DonacionRepository donaciones, IPaymentAdapter payment)
    { _donaciones = donaciones; _payment = payment; }

    public async Task<Donacion> RegistrarAsync(CrearDonacionDto dto, Guid? registradoPor)
    {
        if (dto.Monto <= 0) throw new ArgumentException("El monto debe ser mayor a 0");
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
        if (dto.Monto <= 0) throw new ArgumentException("El monto debe ser mayor a 0");

        // En línea solo se acepta QR, pasarela o transferencia (el efectivo se registra en oficina)
        var medio = MedioPago.PASARELA;
        if (!string.IsNullOrWhiteSpace(dto.Medio)
            && (!Enum.TryParse(dto.Medio, true, out medio) || medio == MedioPago.EFECTIVO))
            throw new ArgumentException("Medio de pago inválido para donación en línea");

        var ref_ = dto.Referencia ?? $"DON-{Guid.NewGuid().ToString("N")[..10].ToUpper()}";
        var ok = await _payment.ProcesarPagoAsync(dto.Monto, ref_);
        if (!ok) throw new InvalidOperationException("Pago rechazado");

        var d = new Donacion
        {
            Monto = dto.Monto,
            Medio = medio,
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
    public Task<List<Donacion>> ListarRegistradasPorAsync(Guid usuarioId) => _donaciones.GetByRegistradorAsync(usuarioId);
}