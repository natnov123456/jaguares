import { useState } from 'react';
import { reportesApi } from '../services/api';
import { aParametros, errorDeFiltros } from '../utils/filtros';

// JAG-87 / FUJ-124: botón "Exportar" (PDF institucional o Excel) con los mismos filtros que la pantalla.
// El backend verifica los permisos, registra la exportación y avisa si no hay datos.
//   reporte: clave del reporte (p. ej. "inscritos-curso")   filtros: { q, estado, desde, hasta, actividadId, ... }
async function mensajeDeError(err) {
  const data = err.response?.data;
  // Con responseType "blob" el error JSON llega como Blob
  if (data instanceof Blob) {
    try { return JSON.parse(await data.text()).error; } catch { /* sin cuerpo */ }
  }
  if (err.response?.status === 403) return 'No tienes permiso para exportar este reporte.';
  return data?.error || 'No se pudo generar el archivo.';
}

export default function BotonExportar({ reporte, filtros, etiqueta = 'Exportar', deshabilitado = false }) {
  const [generando, setGenerando] = useState(null); // 'pdf' | 'xlsx'
  const [aviso, setAviso] = useState(null);

  const exportar = async (formato) => {
    setAviso(null);
    const error = errorDeFiltros(filtros);
    if (error) { setAviso(error); return; }
    setGenerando(formato);
    try {
      const r = await reportesApi.exportar(reporte, formato, aParametros(filtros));
      const nombre = /filename\*?=(?:UTF-8'')?"?([^";]+)/.exec(r.headers['content-disposition'] ?? '')?.[1]
        ?? `${reporte}.${formato}`;
      const url = URL.createObjectURL(r.data);
      const a = document.createElement('a');
      a.href = url;
      a.download = decodeURIComponent(nombre);
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setAviso(await mensajeDeError(err));
    } finally {
      setGenerando(null);
    }
  };

  return (
    <div className="exportar" aria-busy={!!generando}>
      <span className="exportar-etiqueta">{etiqueta}:</span>
      <button type="button" className="btn-secondary" disabled={deshabilitado || !!generando} onClick={() => exportar('pdf')}>
        {generando === 'pdf' ? 'Generando PDF…' : '⬇ PDF'}
      </button>
      <button type="button" className="btn-secondary" disabled={deshabilitado || !!generando} onClick={() => exportar('xlsx')}>
        {generando === 'xlsx' ? 'Generando Excel…' : '⬇ Excel'}
      </button>
      {aviso && <span className="exportar-aviso" role="alert">{aviso}</span>}
    </div>
  );
}
