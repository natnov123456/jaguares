import { useEffect, useState } from 'react';
import { registroActividadApi } from '../../services/api';
import BarraFiltros from '../../components/filtros/BarraFiltros';
import BotonExportar from '../../components/BotonExportar';
import useFiltros from '../../hooks/useFiltros';
import { errorDeFiltros } from '../../utils/filtros';
import { fechaHora } from '../../utils/formato';

// Registro de actividad: quién entró, a qué hora, desde dónde y qué hizo. Solo la Dirección lo consulta.
// Se guarda en la base de datos y no se puede modificar ni borrar.
const RESULTADOS = [
  { valor: 'EXITO', etiqueta: 'Correctos' },
  { valor: 'ERROR', etiqueta: 'Con error o denegados' },
  { valor: 'SOSPECHOSO', etiqueta: 'Sospechosos (denegados o bloqueados)' },
];
const TAMANO = 50;

const ETIQUETA_ROL = {
  DIRECTORA: 'Directora', CONTADOR: 'Contador', ENCARGADO_EVENTOS: 'Operaciones', COORDINADOR_VOLUNTARIADO: 'Coordinación',
  DOCENTE: 'Docente', PARTICIPANTE: 'Participante', VOLUNTARIO: 'Voluntario',
};

// "Mozilla/5.0 (Windows NT 10.0...) Chrome/131" -> "Chrome · Windows"
function navegadorCorto(ua) {
  if (!ua) return '—';
  const nav = /Edg\//.test(ua) ? 'Edge' : /OPR\//.test(ua) ? 'Opera' : /Chrome\//.test(ua) ? 'Chrome' : /Firefox\//.test(ua) ? 'Firefox'
    : /Safari\//.test(ua) ? 'Safari' : ua.split(/[ /]/)[0];
  const so = /Android/.test(ua) ? 'Android' : /iPhone|iPad/.test(ua) ? 'iOS' : /Windows/.test(ua) ? 'Windows' : /Mac OS/.test(ua) ? 'macOS'
    : /Linux/.test(ua) ? 'Linux' : '';
  return so ? `${nav} · ${so}` : nav;
}

const Dato = ({ titulo, valor, detalle }) => (
  <div className="kpi"><p className="kpi-titulo">{titulo}</p><p className="kpi-valor">{valor}</p><p className="kpi-detalle">{detalle}</p></div>
);
const Enlace = ({ texto, onClick }) => (
  <button type="button" className="btn-link" onClick={onClick}
          style={{ background: 'none', border: 'none', padding: 0, color: '#b45309', textDecoration: 'underline', cursor: 'pointer', font: 'inherit' }}>
    {texto}
  </button>
);

// Vigilancia de las últimas 24 horas: lo que conviene revisar. Al tocar una IP o una cuenta se filtra la tabla.
function PanelVigilancia({ alElegir }) {
  const [resumen, setResumen] = useState(null);
  useEffect(() => {
    let vigente = true;
    registroActividadApi.seguridad(24).then(r => { if (vigente) setResumen(r.data); }).catch(() => {});
    return () => { vigente = false; };
  }, []);
  if (!resumen) return null;
  const alerta = resumen.ips.length > 0 || resumen.cuentas.length > 0 || resumen.bloqueos > 0;
  return (
    <section className="panel" style={{ marginBottom: 16, borderLeft: `4px solid ${alerta ? '#dc2626' : '#16a34a'}` }}>
      <h2 style={{ marginTop: 0, fontSize: '1.1rem' }}>
        Vigilancia de seguridad · últimas 24 horas {alerta ? '— revisar' : '— sin señales de riesgo'}
      </h2>
      <div className="kpis">
        <Dato titulo="Inicios de sesión fallidos" valor={resumen.loginsFallidos} detalle="Contraseña o correo incorrectos" />
        <Dato titulo="Bloqueos por intentos" valor={resumen.bloqueos} detalle="Cuenta o IP frenada por seguridad" />
        <Dato titulo="Accesos denegados" valor={resumen.accesosDenegados} detalle="Intentos de entrar a pantallas sin permiso" />
      </div>
      {resumen.ips.length > 0 && (
        <>
          <h3 style={{ fontSize: '0.95rem', marginBottom: 4 }}>IPs con muchos fallos</h3>
          <ul style={{ margin: 0 }}>
            {resumen.ips.map(x => (
              <li key={x.ip}>
                <Enlace texto={x.ip} onClick={() => alElegir(x.ip)} /> — {x.fallos} fallos contra {x.cuentasDistintas} cuenta(s) · último {fechaHora(x.ultimo)}
              </li>
            ))}
          </ul>
        </>
      )}
      {resumen.cuentas.length > 0 && (
        <>
          <h3 style={{ fontSize: '0.95rem', marginBottom: 4 }}>Cuentas con varios intentos fallidos</h3>
          <ul style={{ margin: 0 }}>
            {resumen.cuentas.map(x => (
              <li key={x.email}>
                <Enlace texto={x.email} onClick={() => alElegir(x.email)} /> — {x.fallos} intentos fallidos · último {fechaHora(x.ultimo)}
              </li>
            ))}
          </ul>
        </>
      )}
      <p className="muted" style={{ fontSize: '0.8rem', marginBottom: 0 }}>
        Tras 5 intentos fallidos seguidos la cuenta se bloquea 15 minutos (y la IP tras 20) y se avisa por correo a la Dirección.
      </p>
    </section>
  );
}

export default function RegistroActividad() {
  const { valores, cambiar, limpiar } = useFiltros(['q', 'estado', 'desde', 'hasta']);
  const [pagina, setPagina] = useState(1);
  const [datos, setDatos] = useState(null);
  const [error, setError] = useState(null);
  const clave = JSON.stringify(valores);

  // Al cambiar un filtro se vuelve a la primera página
  useEffect(() => { setPagina(1); }, [clave]);

  useEffect(() => {
    if (errorDeFiltros(valores)) return;
    let vigente = true;
    registroActividadApi.listar({
      q: valores.q || undefined, resultado: valores.estado || undefined,
      desde: valores.desde || undefined, hasta: valores.hasta || undefined, pagina, tamano: TAMANO,
    })
      .then(r => { if (vigente) { setDatos(r.data); setError(null); } })
      .catch(err => { if (vigente) setError(err.response?.data?.error || 'No se pudo cargar el registro de actividad'); });
    return () => { vigente = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clave, pagina]);

  const paginas = datos ? Math.max(1, Math.ceil(datos.total / TAMANO)) : 1;

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Registro de actividad</h1>
          <p>Quién entró, a qué hora, desde dónde y qué hizo en la plataforma, incluidos los intentos fallidos. No se puede modificar ni borrar.</p>
        </div>
      </div>
      <PanelVigilancia alElegir={(texto) => { cambiar('q', texto); cambiar('estado', 'SOSPECHOSO'); }} />
      <section className="panel">
        <BarraFiltros valores={valores} cambiar={cambiar} limpiar={limpiar} texto="Persona, correo, acción o IP" estados={RESULTADOS}
                      etiquetaEstado="Resultado" fechas="Fecha" total={datos?.total} visibles={datos?.total}>
          <BotonExportar reporte="registro-actividad" filtros={valores} />
        </BarraFiltros>
        {error && <div className="error-msg">{error}</div>}
        {!datos ? <p>Cargando...</p> : datos.registros.length === 0 ? (
          <p className="muted" style={{ textAlign: 'center', padding: 24 }}>No hay actividad con esos filtros.</p>
        ) : (
          <>
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>Fecha y hora</th><th>Persona</th><th>Acción</th><th>Detalle</th><th>Resultado</th><th>Origen</th></tr></thead>
                <tbody>
                  {datos.registros.map(r => (
                    <tr key={r.id}>
                      <td style={{ whiteSpace: 'nowrap' }}>{fechaHora(r.fecha)}</td>
                      <td>
                        <strong>{r.usuario ?? r.email ?? 'Visitante'}</strong>
                        <div className="muted" style={{ fontSize: '0.8rem' }}>
                          {[r.usuario ? r.email : null, ETIQUETA_ROL[r.rol] ?? r.rol].filter(Boolean).join(' · ')}
                        </div>
                      </td>
                      <td>{r.accion}<div className="muted" style={{ fontSize: '0.75rem', fontFamily: 'monospace' }}>{r.metodo} {r.ruta}</div></td>
                      <td style={{ fontSize: '0.8rem', wordBreak: 'break-all' }}>{r.recurso ?? '—'}</td>
                      <td>
                        <span className={`badge ${r.exito ? 'badge-ok' : 'badge-off'}`}>
                          {r.exito ? 'Correcto' : r.estado === 401 || r.estado === 403 ? 'Denegado' : `Error ${r.estado}`}
                        </span>
                      </td>
                      <td style={{ fontSize: '0.8rem' }}>{r.ip ?? '—'}<div className="muted">{navegadorCorto(r.navegador)}</div></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div style={{ display: 'flex', gap: 12, alignItems: 'center', justifyContent: 'flex-end', marginTop: 12 }}>
              <span className="muted">Página {pagina} de {paginas} · {datos.total} registros</span>
              <button className="btn-secondary" disabled={pagina <= 1} onClick={() => setPagina(p => p - 1)}>Anterior</button>
              <button className="btn-secondary" disabled={pagina >= paginas} onClick={() => setPagina(p => p + 1)}>Siguiente</button>
            </div>
          </>
        )}
      </section>
    </div>
  );
}
