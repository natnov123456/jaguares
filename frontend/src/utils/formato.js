// Las fechas DateOnly llegan como "2026-10-01"; new Date() las tomaría como UTC
// y en Bolivia (UTC-4) se mostrarían un día antes.
export function fecha(valor) {
  if (!valor) return '-';
  const d = /^\d{4}-\d{2}-\d{2}$/.test(valor) ? new Date(`${valor}T00:00`) : new Date(valor);
  return d.toLocaleDateString('es-BO');
}

export function fechaHora(valor) {
  if (!valor) return '-';
  return new Date(valor).toLocaleString('es-BO', { dateStyle: 'short', timeStyle: 'short' });
}

export function bs(monto) {
  return `Bs. ${Number(monto ?? 0).toLocaleString('es-BO', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export const ESTADO_BADGE = {
  PUBLICADO: 'badge-ok',
  EN_CURSO: 'badge-warn',
  FINALIZADO: '',
  CANCELADO: 'badge-off',
  BORRADOR: '',
  ACTIVO: 'badge-ok',
  INACTIVO: 'badge-off',
  PENDIENTE: 'badge-warn',
  CONFIRMADA: 'badge-ok',
  CANCELADA: 'badge-off',
  ASISTIO: 'badge-ok',
  NO_ASISTIO: 'badge-off',
};
