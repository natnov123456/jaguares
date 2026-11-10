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

export const MESES = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto',
                      'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

export function bs(monto) {
  return `Bs. ${Number(monto ?? 0).toLocaleString('es-BO', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export const numeroComprobante = (n) => String(n).padStart(6, '0');

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
  ASIGNADA: 'badge-warn',
  COMPLETADA: 'badge-ok',
};

// FUJ-118: horas de voluntariado ("6.5" -> "6,5 h")
export function horas(valor) {
  return `${Number(valor ?? 0).toLocaleString('es-BO', { maximumFractionDigits: 2 })} h`;
}

// Fecha de una actividad: los cursos (todoElDia) llegan sin hora y se muestran como rango de días
export function fechaActividad(a) {
  if (a.todoElDia) return `${fecha(a.inicio.slice(0, 10))} – ${fecha((a.fin ?? a.inicio).slice(0, 10))}`;
  return fechaHora(a.inicio);
}
