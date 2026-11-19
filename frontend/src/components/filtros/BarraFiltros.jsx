import { errorDeFiltros, hayFiltros } from '../../utils/filtros';

// Barra de filtros combinados reutilizable (corrección de la evaluación). Todas las pantallas con listas la usan.
//   valores/cambiar/limpiar: de useFiltros()
//   texto: placeholder del buscador (o false para ocultarlo)
//   estados: [{ valor, etiqueta }] para el selector de estado (opcional)
//   fechas: etiqueta del rango de fechas (opcional), p. ej. "Inscritos entre"
//   total / visibles: para mostrar "N de M resultados"
//   children: filtros propios de la pantalla y acciones (p. ej. <BotonExportar/>)
export default function BarraFiltros({ valores, cambiar, limpiar, texto = 'Buscar…', estados, etiquetaEstado = 'Estado',
                                       fechas, total, visibles, children }) {
  const error = errorDeFiltros(valores);
  return (
    <div className="barra-filtros" role="search">
      <div className="barra-filtros-campos">
        {texto !== false && (
          <label className="filtro">
            <span>Buscar</span>
            <input type="search" value={valores.q ?? ''} placeholder={texto} maxLength={100}
                   onChange={(e) => cambiar('q', e.target.value)} />
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
          <>
            <label className="filtro">
              <span>{fechas} — desde</span>
              <input type="date" value={valores.desde ?? ''} max={valores.hasta || undefined}
                     onChange={(e) => cambiar('desde', e.target.value)} />
            </label>
            <label className="filtro">
              <span>hasta</span>
              <input type="date" value={valores.hasta ?? ''} min={valores.desde || undefined}
                     onChange={(e) => cambiar('hasta', e.target.value)} />
            </label>
          </>
        )}
        {children}
      </div>
      <div className="barra-filtros-pie">
        {total != null && (
          <span className="muted" aria-live="polite">
            {hayFiltros(valores) ? `${visibles} de ${total} resultado(s)` : `${total} resultado(s)`}
          </span>
        )}
        {hayFiltros(valores) && (
          <button type="button" className="btn-link" onClick={limpiar}>Limpiar filtros</button>
        )}
      </div>
      {error && <div className="error-msg" role="alert" style={{ marginTop: 8 }}>{error}</div>}
    </div>
  );
}
