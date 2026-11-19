import { useId } from 'react';
import { errorDeFiltros, hayFiltros } from '../../utils/filtros';

// Barra de filtros combinados reutilizable (corrección de la evaluación). Todas las pantallas con listas la usan.
//   valores/cambiar/limpiar: de useFiltros()
//   texto: placeholder del buscador (o false para ocultarlo)
//   estados: [{ valor, etiqueta }] para el selector de estado (opcional)
//   fechas: etiqueta del rango de fechas (opcional), p. ej. "Inscritos entre"
//   total / visibles: para mostrar "N de M resultados"
//   children: filtros propios de la pantalla (<label className="filtro">) y acciones (p. ej. <BotonExportar/>)
export default function BarraFiltros({ valores, cambiar, limpiar, texto = 'Buscar…', estados, etiquetaEstado = 'Estado',
                                       fechas, total, visibles, children }) {
  const id = useId();
  const error = errorDeFiltros(valores);
  const filtrando = hayFiltros(valores);

  return (
    <div className="barra-filtros" role="search">
      <div className="barra-filtros-campos">
        {texto !== false && (
          <label className="filtro filtro-buscar">
            <span>Buscar</span>
            <span className="filtro-buscar-caja">
              <svg viewBox="0 0 20 20" aria-hidden="true"><circle cx="9" cy="9" r="6" /><path d="m14 14 3.5 3.5" /></svg>
              <input type="search" value={valores.q ?? ''} placeholder={texto} maxLength={100}
                     onChange={(e) => cambiar('q', e.target.value)} />
            </span>
          </label>
        )}
        {estados && (
          <label className="filtro">
            <span>{etiquetaEstado}</span>
            <select value={valores.estado ?? ''} onChange={(e) => cambiar('estado', e.target.value)}>
              <option value="">Todos</option>
              {estados.map((e) => <option key={e.valor} value={e.valor}>{e.etiqueta}</option>)}
            </select>
          </label>
        )}
        {fechas && (
          <div className="filtro filtro-rango" role="group" aria-labelledby={`${id}-fechas`}>
            <span id={`${id}-fechas`}>{fechas}</span>
            <span className="filtro-rango-campos">
              <input type="date" aria-label={`${fechas}: desde`} title="Desde" value={valores.desde ?? ''}
                     max={valores.hasta || undefined} onChange={(e) => cambiar('desde', e.target.value)} />
              <span className="filtro-rango-guion" aria-hidden="true">–</span>
              <input type="date" aria-label={`${fechas}: hasta`} title="Hasta" value={valores.hasta ?? ''}
                     min={valores.desde || undefined} onChange={(e) => cambiar('hasta', e.target.value)} />
            </span>
          </div>
        )}
        {children}
      </div>

      {(total != null || filtrando) && (
        <div className="barra-filtros-pie">
          {total != null && (
            <span className="barra-filtros-total" aria-live="polite">
              {filtrando ? <><strong>{visibles}</strong> de {total} resultado(s)</> : <><strong>{total}</strong> resultado(s)</>}
            </span>
          )}
          {filtrando && (
            <button type="button" className="barra-filtros-limpiar" onClick={limpiar}>
              <span aria-hidden="true">×</span> Limpiar filtros
            </button>
          )}
        </div>
      )}
      {error && <div className="error-msg barra-filtros-error" role="alert">{error}</div>}
    </div>
  );
}
