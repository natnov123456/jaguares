import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { cursosApi, eventosApi, participanteApi } from '../services/api';
import MisCertificados from '../components/MisCertificados';
import MisDonaciones from '../components/MisDonaciones';
import MiListaEspera from '../components/MiListaEspera';
import ModalQr from '../components/ModalQr';
import BotonCertificado from '../components/BotonCertificado';
import Estado from '../components/Estado';
import { fecha, fechaHora } from '../utils/formato';

// FUJ-112: "Mi panel" del participante — cursos y eventos (próximos y pasados), certificados,
// lista de espera y donaciones en un solo lugar.
const PESTANAS = [
  { id: 'proximas', titulo: 'Próximas actividades' },
  { id: 'historial', titulo: 'Historial' },
  { id: 'certificados', titulo: 'Certificados' },
  { id: 'espera', titulo: 'Lista de espera' },
  { id: 'donaciones', titulo: 'Donaciones' },
];

const ETIQUETA_RESULTADO = {
  ASISTIO: { texto: 'Asististe', clase: 'badge-ok' },
  NO_ASISTIO: { texto: 'No asististe', clase: 'badge-off' },
};

function Cuando({ a }) {
  if (a.todoElDia) return <>{fecha(a.inicio.slice(0, 10))} – {fecha((a.fin ?? a.inicio).slice(0, 10))}</>;
  return <>{fechaHora(a.inicio)}</>;
}

function TipoActividad({ tipo }) {
  return <span className={`badge ${tipo === 'CURSO' ? 'badge-warn' : ''}`}>{tipo === 'CURSO' ? '📚 Curso' : '📅 Evento'}</span>;
}

export default function MiCuenta() {
  const { usuario } = useAuth();
  const [panel, setPanel] = useState(null);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState(null);
  const [pestana, setPestana] = useState('proximas');
  const [qr, setQr] = useState(null);
  const [cancelando, setCancelando] = useState(null);

  const cargar = useCallback(() =>
    participanteApi.panel()
      .then(r => setPanel(r.data))
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudo cargar tu panel' }))
      .finally(() => setLoading(false)), []);

  useEffect(() => { cargar(); }, [cargar]);

  const cancelar = async (a) => {
    const aviso = a.tipo === 'CURSO'
      ? `¿Cancelar tu inscripción a "${a.titulo}"?\n\nTu cupo pasará a la primera persona de la lista de espera (o quedará libre para otra persona).`
      : `¿Cancelar tu inscripción a "${a.titulo}"?\n\nTu código QR dejará de servir. Si cambias de opinión puedes volver a inscribirte.`;
    if (!window.confirm(aviso)) return;
    setCancelando(a.actividadId);
    setMsg(null);
    try {
      if (a.tipo === 'CURSO') await cursosApi.cancelarInscripcion(a.actividadId);
      else await eventosApi.cancelarInscripcion(a.actividadId);
      setMsg({ tipo: 'success', texto: `Inscripción a "${a.titulo}" cancelada. Tu lugar quedó libre.` });
      await cargar();
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo cancelar la inscripción' });
    } finally {
      setCancelando(null);
    }
  };

  const r = panel?.resumen;
  const tarjetas = r ? [
    { titulo: 'Cursos en marcha', valor: r.cursosActivos, ir: 'proximas' },
    { titulo: 'Próximos eventos', valor: r.eventosProximos, ir: 'proximas' },
    { titulo: 'Actividades completadas', valor: r.actividadesCompletadas, ir: 'historial' },
    { titulo: 'Certificados', valor: r.certificados, ir: 'certificados' },
    { titulo: 'En lista de espera', valor: r.enListaEspera, ir: 'espera' },
  ] : [];

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Mi panel</h1>
          <p>Hola, {usuario?.nombreCompleto}. Aquí sigues tus cursos, eventos y certificados sin tener que consultar a la fundación.</p>
        </div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <Link to="/cursos" className="btn-primary">Buscar cursos</Link>
          <Link to="/calendario" className="btn-secondary">Ver calendario</Link>
        </div>
      </div>

      {msg && <div className={`${msg.tipo}-msg`} role="status">{msg.texto}</div>}

      {loading ? (
        <p>Cargando...</p>
      ) : panel && (
        <>
          <div className="panel-resumen">
            {tarjetas.map(t => (
              <button key={t.titulo} type="button" className="stat-box panel-resumen-item" onClick={() => setPestana(t.ir)}>
                <p>{t.titulo}</p>
                <h2>{t.valor}</h2>
              </button>
            ))}
          </div>

          <div className="pestanas" role="tablist" aria-label="Secciones de mi panel">
            {PESTANAS.map(p => (
              <button key={p.id} type="button" role="tab" aria-selected={pestana === p.id}
                      className={`pestana ${pestana === p.id ? 'activa' : ''}`} onClick={() => setPestana(p.id)}>
                {p.titulo}
              </button>
            ))}
          </div>

          <section className="panel" role="tabpanel">
            {pestana === 'proximas' && (
              panel.proximas.length === 0 ? (
                <p className="muted">
                  No tienes actividades próximas. <Link to="/calendario" style={{ color: '#b45309' }}>Revisa el calendario →</Link>
                </p>
              ) : (
                <div className="table-wrap">
                  <table className="table">
                    <thead><tr><th>Tipo</th><th>Actividad</th><th>Fecha</th><th>Inscripción</th><th /></tr></thead>
                    <tbody>
                      {panel.proximas.map(a => (
                        <tr key={`${a.tipo}-${a.actividadId}`}>
                          <td><TipoActividad tipo={a.tipo} /></td>
                          <td>
                            <strong>{a.titulo}</strong>
                            {a.lugar && <div className="muted" style={{ fontSize: '0.85rem' }}>📍 {a.lugar}</div>}
                          </td>
                          <td><Cuando a={a} /></td>
                          <td><Estado valor={a.estadoInscripcion} /></td>
                          <td>
                            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                              {a.tipo === 'EVENTO' && a.inscripcionEventoId && (
                                <button className="btn-primary" style={{ padding: '6px 12px' }}
                                        onClick={() => setQr({ inscripcionId: a.inscripcionEventoId, titulo: a.titulo, inicio: a.inicio })}>
                                  Ver QR
                                </button>
                              )}
                              {a.puedeCancelar && (
                                <button className="btn-secondary" disabled={cancelando === a.actividadId} onClick={() => cancelar(a)}>
                                  {cancelando === a.actividadId ? 'Cancelando...' : 'Cancelar'}
                                </button>
                              )}
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )
            )}

            {pestana === 'historial' && (
              panel.pasadas.length === 0 ? (
                <p className="muted">Todavía no tienes actividades pasadas.</p>
              ) : (
                <div className="table-wrap">
                  <table className="table">
                    <thead><tr><th>Tipo</th><th>Actividad</th><th>Fecha</th><th>Resultado</th><th /></tr></thead>
                    <tbody>
                      {panel.pasadas.map(a => {
                        const resultado = ETIQUETA_RESULTADO[a.estadoInscripcion];
                        return (
                          <tr key={`${a.tipo}-${a.actividadId}`}>
                            <td><TipoActividad tipo={a.tipo} /></td>
                            <td><strong>{a.titulo}</strong></td>
                            <td><Cuando a={a} /></td>
                            <td>
                              {a.codigoCertificado ? <span className="badge badge-ok">🎓 Certificado emitido</span>
                                : resultado ? <span className={`badge ${resultado.clase}`}>{resultado.texto}</span>
                                : <Estado valor={a.estadoActividad} />}
                            </td>
                            <td>
                              {a.codigoCertificado && (() => {
                                const cert = panel.certificados.find(c => c.codigoUnico === a.codigoCertificado);
                                return cert && <BotonCertificado id={cert.id} codigo={cert.codigoUnico} className="btn-secondary" />;
                              })()}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )
            )}

            {pestana === 'certificados' && <MisCertificados certificados={panel.certificados} />}
            {pestana === 'espera' && <MiListaEspera onCambio={cargar} />}
            {pestana === 'donaciones' && <MisDonaciones />}
          </section>
        </>
      )}

      {qr && <ModalQr inscripcion={qr} onCerrar={() => setQr(null)} />}
    </div>
  );
}
