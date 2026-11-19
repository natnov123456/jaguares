// Corrección de la evaluación (búsquedas filtradas): módulo único de filtros combinados.
// Mismas reglas que el backend (FiltroCombinado.cs), así lo que se ve en pantalla es lo que se exporta:
//   - todos los filtros se combinan con Y; un filtro vacío no restringe
//   - texto: sin mayúsculas ni tildes, busca en cualquiera de los campos indicados
//   - estado: igualdad exacta (sin mayúsculas)
//   - desde/hasta: rango inclusivo con la fecha en hora de Bolivia; sin fecha queda fuera si hay rango

export const FILTROS_VACIOS = { q: '', estado: '', desde: '', hasta: '' };

// Opciones de estado comunes para el selector de la barra de filtros
export const ESTADOS_ACTIVIDAD = [
  { valor: 'PUBLICADO', etiqueta: 'Publicado' }, { valor: 'EN_CURSO', etiqueta: 'En curso' },
  { valor: 'FINALIZADO', etiqueta: 'Finalizado' }, { valor: 'CANCELADO', etiqueta: 'Cancelado' },
];
export const ESTADOS_VOLUNTARIO = [{ valor: 'ACTIVO', etiqueta: 'Activo' }, { valor: 'INACTIVO', etiqueta: 'Inactivo' }];
export const ESTADOS_ASIGNACION = [
  { valor: 'ASIGNADA', etiqueta: 'Asignada' }, { valor: 'COMPLETADA', etiqueta: 'Completada' },
];

export function normalizar(texto) {
  return String(texto ?? '').trim().normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();
}

export function coincideTexto(q, campos) {
  const buscado = normalizar(q);
  return !buscado || campos.some((c) => normalizar(c).includes(buscado));
}

export function coincideEstado(estado, valor) {
  return !estado || String(estado).toLowerCase() === String(valor ?? '').toLowerCase();
}

// "2026-11-10" (fecha sin hora) se toma tal cual; un instante ISO se pasa a la fecha de Bolivia (UTC-4)
export function fechaBolivia(valor) {
  if (!valor) return null;
  if (/^\d{4}-\d{2}-\d{2}$/.test(valor)) return valor;
  const d = new Date(new Date(valor).getTime() - 4 * 3600 * 1000);
  return Number.isNaN(d.getTime()) ? null : d.toISOString().slice(0, 10);
}

export function enRango(valor, desde, hasta) {
  if (!desde && !hasta) return true;
  const f = fechaBolivia(valor);
  if (!f) return false;
  return (!desde || f >= desde) && (!hasta || f <= hasta);
}

// opciones: { texto: (x) => [campos], estado: (x) => valor, fecha: (x) => fecha }
export function aplicarFiltros(items, valores, opciones = {}) {
  const { q, estado, desde, hasta } = { ...FILTROS_VACIOS, ...valores };
  return (items ?? []).filter((x) =>
    (!opciones.texto || coincideTexto(q, opciones.texto(x))) &&
    (!opciones.estado || coincideEstado(estado, opciones.estado(x))) &&
    (!opciones.fecha || enRango(opciones.fecha(x), desde, hasta)));
}

export function errorDeFiltros(valores) {
  if (valores?.desde && valores?.hasta && valores.desde > valores.hasta) return 'La fecha "desde" no puede ser posterior a "hasta".';
  if ((valores?.q ?? '').length > 100) return 'La búsqueda no puede superar 100 caracteres.';
  return null;
}

// Solo los filtros con valor, listos para la URL o la API
export function aParametros(valores) {
  return Object.fromEntries(Object.entries(valores ?? {}).filter(([, v]) => v !== '' && v != null));
}

export function hayFiltros(valores) {
  return Object.keys(aParametros(valores)).length > 0;
}
