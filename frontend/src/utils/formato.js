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

// FUJ-118: horas de voluntariado en horas y minutos (2.75 -> "2 h 45 min", 3 -> "3 h", 0.5 -> "30 min")
export function horas(valor) {
  const minutos = Math.round(Number(valor ?? 0) * 60);
  const h = Math.floor(minutos / 60), m = minutos % 60;
  if (h === 0 && m === 0) return '0 h';
  if (m === 0) return `${h} h`;
  return h === 0 ? `${m} min` : `${h} h ${m} min`;
}

// Horario de una colaboración: "15:30 a 18:15" (y "× 4 días" si se repitió). Vacío si no hay horario.
export function horario(inicio, fin, dias) {
  if (!inicio || !fin) return '';
  return `${inicio.slice(0, 5)} a ${fin.slice(0, 5)}${dias > 1 ? ` × ${dias} días` : ''}`;
}

// Horas que resultan de un horario "HH:mm" a "HH:mm" (por días). null si el horario no es válido.
export function horasDeHorario(inicio, fin, dias = 1) {
  const minutos = (t) => { const [h, m] = String(t ?? '').split(':').map(Number); return Number.isInteger(h) && Number.isInteger(m) ? h * 60 + m : NaN; };
  const total = (minutos(fin) - minutos(inicio)) * (Number(dias) || 1);
  return total > 0 ? Math.round((total / 60) * 100) / 100 : null;
}

// Fecha de una actividad: los cursos (todoElDia) llegan sin hora y se muestran como rango de días
export function fechaActividad(a) {
  if (a.todoElDia) return `${fecha(a.inicio.slice(0, 10))} – ${fecha((a.fin ?? a.inicio).slice(0, 10))}`;
  return fechaHora(a.inicio);
}
