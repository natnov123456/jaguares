import { useEffect, useState } from 'react';
import { reportesApi } from '../../services/api';
import BarraFiltros from '../../components/filtros/BarraFiltros';
import BotonExportar from '../../components/BotonExportar';
import useFiltros from '../../hooks/useFiltros';
import { aParametros, errorDeFiltros } from '../../utils/filtros';

// Corrección de la evaluación (logging): registro de cada PDF/Excel generado — usuario, fecha, filtros usados —
// y de los errores al generarlo. Solo la Dirección lo consulta.
const ESTADOS = [{ valor: 'EXITO', etiqueta: 'Generados' }, { valor: 'ERROR', etiqueta: 'Con error' }];

export default function Exportaciones() {
  const { valores, cambiar, limpiar } = useFiltros(['q', 'estado', 'desde', 'hasta', 'accion']);
  const [tabla, setTabla] = useState(null);
  const [error, setError] = useState(null);
  const clave = JSON.stringify(valores);

  useEffect(() => {
    if (errorDeFiltros(valores)) return;
    let vigente = true;
    reportesApi.tabla('exportaciones', aParametros(valores))
      .then(r => { if (vigente) { setTabla(r.data); setError(null); } })
      .catch(err => { if (vigente) setError(err.response?.data?.error || 'No se pudo cargar el registro'); });
    return () => { vigente = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clave]);

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Registro de exportaciones</h1>
          <p>Cada reporte PDF o Excel que se genera en la plataforma: quién, cuándo, con qué filtros y si hubo errores.</p>
        </div>
      </div>
      <section className="panel">
        <BarraFiltros valores={valores} cambiar={cambiar} limpiar={limpiar} texto="Reporte, usuario o rol" estados={ESTADOS}
                      etiquetaEstado="Resultado" fechas="Fecha" total={tabla?.filas.length} visibles={tabla?.filas.length}>
          <label className="filtro">
            <span>Formato</span>
            <select value={valores.accion} onChange={e => cambiar('accion', e.target.value)}>
              <option value="">Todos</option><option value="PDF">PDF</option><option value="XLSX">Excel</option>
            </select>
          </label>
          <BotonExportar reporte="exportaciones" filtros={valores} />
        </BarraFiltros>
        {error && <div className="error-msg">{error}</div>}
        {!tabla ? <p>Cargando...</p> : tabla.filas.length === 0 ? (
          <p className="muted" style={{ textAlign: 'center', padding: 24 }}>No hay exportaciones con esos filtros.</p>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead><tr>{tabla.columnas.map(c => <th key={c.titulo}>{c.titulo}</th>)}</tr></thead>
              <tbody>
                {tabla.filas.map((f, i) => (
                  <tr key={i}>
                    {f.map((v, j) => (
                      <td key={j} style={j === 5 ? { fontFamily: 'monospace', fontSize: '0.8rem', wordBreak: 'break-all' } : undefined}
                          className={j === 7 && v.startsWith('Error') ? 'error-text' : undefined}>{v}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
