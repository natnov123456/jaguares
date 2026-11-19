import { Fragment, useEffect, useState } from 'react';
import { auditoriaApi } from '../../services/api';
import BarraFiltros from '../../components/filtros/BarraFiltros';
import BotonExportar from '../../components/BotonExportar';
import useFiltros from '../../hooks/useFiltros';
import { aParametros, errorDeFiltros } from '../../utils/filtros';
import { bs, fechaHora, numeroComprobante } from '../../utils/formato';

// JAG-89 / FUJ-125: consulta de la bitácora de auditoría de donaciones (solo lectura) para Contador y Directora.
// Los filtros se aplican en el servidor (la bitácora puede ser grande) y la tabla está paginada.
const ACCIONES = [
  { valor: 'ALTA', etiqueta: 'Alta' }, { valor: 'EDICION', etiqueta: 'Edición' }, { valor: 'ANULACION', etiqueta: 'Anulación' },
];
const ETIQUETA = Object.fromEntries(ACCIONES.map(a => [a.valor, a.etiqueta]));
const BADGE = { ALTA: 'badge-ok', EDICION: 'badge-warn', ANULACION: 'badge-off' };
const CAMPOS = [['monto', 'Monto'], ['medio', 'Medio'], ['referencia', 'Referencia'], ['campana', 'Campaña'],
                ['estado', 'Estado'], ['donante', 'Donante'], ['fecha', 'Fecha']];
const POR_PAGINA = 25;

function valorCampo(clave, v) {
  if (v == null || v === '') return '—';
  if (clave === 'monto') return bs(v);
  if (clave === 'fecha') return fechaHora(v);
  return String(v);
}

// Antes y después lado a lado; resalta lo que cambió
function Detalle({ registro }) {
  const antes = registro.valorAnterior ? JSON.parse(registro.valorAnterior) : null;
  const despues = JSON.parse(registro.valorNuevo);
  return (
    <div className="table-wrap" style={{ background: '#fffbeb', padding: 8, borderRadius: 8 }}>
      <table className="table" style={{ margin: 0 }}>
        <thead><tr><th>Campo</th><th>Valor anterior</th><th>Valor nuevo</th></tr></thead>
        <tbody>
          {CAMPOS.map(([clave, etiqueta]) => {
            const a = antes ? valorCampo(clave, antes[clave]) : '—';
            const n = valorCampo(clave, despues[clave]);
            const cambio = antes && a !== n;
            return (
              <tr key={clave} style={cambio ? { fontWeight: 600, background: '#fef3c7' } : undefined}>
                <td>{etiqueta}</td><td>{antes ? a : <span className="muted">(alta)</span>}</td><td>{n}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
      {registro.motivo && <p style={{ marginTop: 8 }}><strong>Motivo:</strong> {registro.motivo}</p>}
    </div>
  );
}

export default function Bitacora() {
  const { valores, cambiar, limpiar } = useFiltros(['q', 'desde', 'hasta', 'accion', 'usuarioId']);
  const [pagina, setPagina] = useState(1);
  const [datos, setDatos] = useState(null);
  const [usuarios, setUsuarios] = useState([]);
  const [abierto, setAbierto] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => { auditoriaApi.usuarios().then(r => setUsuarios(r.data)).catch(() => {}); }, []);

  const clave = JSON.stringify(valores);
  useEffect(() => { setPagina(1); }, [clave]);

  useEffect(() => {
    if (errorDeFiltros(valores)) return;
    let vigente = true;
    setError(null);
    auditoriaApi.donaciones({ ...aParametros(valores), pagina, tamanoPagina: POR_PAGINA })
      .then(r => { if (vigente) setDatos(r.data); })
      .catch(err => { if (vigente) setError(err.response?.data?.error || 'No se pudo cargar la bitácora'); });
    return () => { vigente = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clave, pagina]);

  const paginas = datos ? Math.max(1, Math.ceil(datos.total / POR_PAGINA)) : 1;

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Bitácora de auditoría</h1>
          <p>Cada alta, corrección o anulación de una donación queda registrada. Este registro no se puede modificar ni borrar.</p>
        </div>
        {datos && <div className="stat-box"><p>Registros encontrados</p><h2>{datos.total}</h2></div>}
      </div>

      <section className="panel">
        <BarraFiltros valores={valores} cambiar={cambiar} limpiar={limpiar} texto="N° de comprobante, usuario o motivo"
                      fechas="Fecha del cambio" total={datos?.total} visibles={datos?.total}>
          <label className="filtro">
            <span>Usuario</span>
            <select value={valores.usuarioId} onChange={e => cambiar('usuarioId', e.target.value)}>
              <option value="">Todos</option>
              {usuarios.map(u => <option key={u.usuarioId} value={u.usuarioId}>{u.nombre}</option>)}
            </select>
          </label>
          <label className="filtro">
            <span>Acción</span>
            <select value={valores.accion} onChange={e => cambiar('accion', e.target.value)}>
              <option value="">Todas</option>
              {ACCIONES.map(a => <option key={a.valor} value={a.valor}>{a.etiqueta}</option>)}
            </select>
          </label>
          <BotonExportar reporte="bitacora-donaciones" filtros={valores} />
        </BarraFiltros>

        {error && <div className="error-msg">{error}</div>}
        {!datos ? <p>Cargando...</p> : datos.registros.length === 0 ? (
          <p className="muted" style={{ textAlign: 'center', padding: 24 }}>No hay registros con esos filtros.</p>
        ) : (
          <>
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>Fecha y hora</th><th>Comprobante</th><th>Acción</th><th>Usuario</th><th>Motivo</th><th /></tr></thead>
                <tbody>
                  {datos.registros.map(r => (
                    <Fragment key={r.id}>
                      <tr>
                        <td>{fechaHora(r.creadoEn)}</td>
                        <td><code>{numeroComprobante(r.numeroComprobante)}</code></td>
                        <td><span className={`badge ${BADGE[r.accion]}`}>{ETIQUETA[r.accion]}</span></td>
                        <td>{r.usuarioNombre}{r.rol && <div className="muted" style={{ fontSize: '0.8rem' }}>{r.rol}</div>}</td>
                        <td className="muted">{r.motivo || '—'}</td>
                        <td>
                          <button type="button" className="btn-secondary" style={{ padding: '4px 10px' }} aria-expanded={abierto === r.id}
                                  onClick={() => setAbierto(abierto === r.id ? null : r.id)}>
                            {abierto === r.id ? 'Ocultar' : 'Antes / después'}
                          </button>
                        </td>
                      </tr>
                      {abierto === r.id && (
                        <tr><td colSpan={6}><Detalle registro={r} /></td></tr>
                      )}
                    </Fragment>
                  ))}
                </tbody>
              </table>
            </div>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', justifyContent: 'center', marginTop: 12 }}>
              <button type="button" className="btn-secondary" disabled={pagina <= 1} onClick={() => setPagina(p => p - 1)}>← Anterior</button>
              <span className="muted">Página {pagina} de {paginas}</span>
              <button type="button" className="btn-secondary" disabled={pagina >= paginas} onClick={() => setPagina(p => p + 1)}>Siguiente →</button>
            </div>
          </>
        )}
      </section>
    </div>
  );
}
