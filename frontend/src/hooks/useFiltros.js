import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { FILTROS_VACIOS } from '../utils/filtros';

// Estado de los filtros guardado en la URL (?q=&estado=&desde=&hasta=): al recargar o compartir el enlace
// se conservan, y el botón Exportar usa exactamente los mismos valores.
// claves: filtros que usa la pantalla (por defecto los cuatro comunes)
export default function useFiltros(claves = Object.keys(FILTROS_VACIOS), iniciales = {}) {
  const [params, setParams] = useSearchParams();
  const clavesTexto = claves.join(',');

  const valores = useMemo(() => Object.fromEntries(clavesTexto.split(',').map((k) => [k, params.get(k) ?? iniciales[k] ?? ''])),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [params, clavesTexto]);

  const cambiar = useCallback((clave, valor) => {
    setParams((p) => {
      const n = new URLSearchParams(p);
      if (valor === '' || valor == null) n.delete(clave); else n.set(clave, valor);
      return n;
    }, { replace: true });
  }, [setParams]);

  const limpiar = useCallback(() => {
    setParams((p) => {
      const n = new URLSearchParams(p);
      clavesTexto.split(',').forEach((k) => n.delete(k));
      return n;
    }, { replace: true });
  }, [setParams, clavesTexto]);

  return { valores, cambiar, limpiar };
}
