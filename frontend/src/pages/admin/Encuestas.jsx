import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { encuestasApi } from '../../services/api';
import { fecha, fechaHora } from '../../utils/formato';
import '../Encuesta.css';

// FUJ-120: resultados de las encuestas de satisfacción. La directora ve todas, el docente las
// de sus cursos y el encargado de eventos las de eventos (lo filtra el backend).
const fechaDe = (r) => (r.tipoActividad === 'CURSO' ? fecha(r.fechaActividad.slice(0, 10)) : fechaHora(r.fechaActividad));
const promedio = (v) => (v == null ? '—' : `${Number(v).toLocaleString('es-BO', { maximumFractionDigits: 2 })} / 5`);

function Estrellas({ valor }) {
  if (valor == null) return <span className="muted">Sin respuestas</span>;
  const llenas = Math.round(valor);
  return <span aria-label={`${valor} de 5`} style={{ color: '#d97706', letterSpacing: 1 }}>{'★'.repeat(llenas)}{'☆'.repeat(5 - llenas)}</span>;
}

function Lista() {
  const [encuestas, setEncuestas] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    encuestasApi.listar()
      .then(r => setEncuestas(r.data))
      .catch(() => setError('No se pudieron cargar las encuestas'))
      .finally(() => setLoading(false));
  }, []);

  const respondidas = encuestas.reduce((n, e) => n + e.respuestas, 0);
  const conDatos = encuestas.filter(e => e.promedioSatisfaccion != null);
  const general = conDatos.length
    ? conDatos.reduce((n, e) => n + e.promedioSatisfaccion, 0) / conDatos.length
    : null;

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Encuestas de satisfacción</h1>
          <p>Se envían automáticamente al cerrar la asistencia de un evento o al finalizar un curso.</p>
        </div>
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
          <div className="stat-box"><p>Satisfacción promedio</p><h2>{promedio(general)}</h2></div>
          <div className="stat-box"><p>Respuestas recibidas</p><h2>{respondidas}</h2></div>
        </div>
      </div>

      {error && <div className="error-msg">{error}</div>}
      <section className="panel">
        {loading ? <p>Cargando...</p> : encuestas.length === 0 ? (
          <p className="muted" style={{ textAlign: 'center', padding: 24 }}>
            Aún no hay encuestas. Se crean al cerrar la asistencia de un evento o al finalizar un curso.
          </p>
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>Actividad</th><th>Fecha</th><th>Satisfacción</th><th>Respuestas</th><th /></tr></thead>
              <tbody>
                {encuestas.map(e => (
                  <tr key={e.id}>
                    <td>
                      <span className={`badge ${e.tipoActividad === 'CURSO' ? 'badge-warn' : ''}`} style={{ marginRight: 8 }}>
                        {e.tipoActividad === 'CURSO' ? 'Curso' : 'Evento'}
                      </span>
                      <strong>{e.actividad}</strong>
                    </td>
                    <td>{fechaDe(e)}</td>
                    <td><Estrellas valor={e.promedioSatisfaccion} /> <span className="muted">{promedio(e.promedioSatisfaccion)}</span></td>
                    <td>{e.respuestas} de {e.invitados}</td>
                    <td><Link to={`/admin/encuestas/${e.id}`} className="btn-secondary">Ver resultados</Link></td>
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

function Detalle({ id }) {
  const [datos, setDatos] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    encuestasApi.resultados(id)
      .then(r => setDatos(r.data))
      .catch(err => setError(err.response?.status === 404 ? 'Encuesta no encontrada' : 'No se pudieron cargar los resultados'));
  }, [id]);

  if (error) return <div className="container page"><div className="error-msg">{error}</div><Link to="/admin/encuestas" className="btn-secondary">← Volver</Link></div>;
  if (!datos) return <div className="container page"><p>Cargando...</p></div>;

  const r = datos.resumen;
  const tasa = r.invitados ? Math.round((r.respuestas / r.invitados) * 100) : 0;

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <p className="muted">{r.tipoActividad === 'CURSO' ? 'Curso' : 'Evento'} · {fechaDe(r)}</p>
          <h1>{r.actividad}</h1>
          <p>Encuesta enviada el {fechaHora(r.creadaEn)}</p>
        </div>
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'center' }}>
          <div className="stat-box"><p>Satisfacción promedio</p><h2>{promedio(r.promedioSatisfaccion)}</h2></div>
          <div className="stat-box"><p>Respondieron</p><h2>{r.respuestas} / {r.invitados} ({tasa}%)</h2></div>
          <Link to="/admin/encuestas" className="btn-secondary">← Todas las encuestas</Link>
        </div>
      </div>

      <div className="split">
        <section className="panel">
          <h2>Resultados por pregunta</h2>
          {datos.preguntas.filter(p => p.tipo === 'ESCALA').map(p => {
            const max = Math.max(1, ...p.distribucion);
            return (
              <div key={p.id} style={{ marginBottom: 20 }}>
                <p style={{ fontWeight: 600 }}>{p.orden}. {p.texto}</p>
                <p className="muted" style={{ fontSize: '0.9rem', marginBottom: 6 }}>
                  <Estrellas valor={p.promedio} /> {promedio(p.promedio)} · {p.respuestas} respuesta(s)
                </p>
                {[5, 4, 3, 2, 1].map(n => (
                  <div key={n} className="barra-resultado">
                    <span>{n} ★</span>
                    <div className="barra-resultado-pista">
                      <div className="barra-resultado-relleno" style={{ width: `${(p.distribucion[n - 1] / max) * 100}%` }} />
                    </div>
                    <span className="muted">{p.distribucion[n - 1]}</span>
                  </div>
                ))}
              </div>
            );
          })}
        </section>

        <section className="panel">
          <h2>Comentarios ({datos.comentarios.length})</h2>
          {datos.comentarios.length === 0 ? (
            <p className="muted">Todavía no hay comentarios.</p>
          ) : (
            <ul style={{ listStyle: 'none', display: 'flex', flexDirection: 'column', gap: 12 }}>
              {datos.comentarios.map((c, i) => (
                <li key={i} style={{ borderLeft: '3px solid #f59e0b', paddingLeft: 12 }}>
                  <div style={{ whiteSpace: 'pre-wrap' }}>“{c.texto}”</div>
                  <div className="muted" style={{ fontSize: '0.8rem' }}>{c.pregunta} · {fechaHora(c.respondidoEn)}</div>
                </li>
              ))}
            </ul>
          )}
          <p className="muted" style={{ fontSize: '0.8rem', marginTop: 12 }}>Los comentarios se muestran sin el nombre de quien respondió.</p>
        </section>
      </div>
    </div>
  );
}

export default function AdminEncuestas() {
  const { id } = useParams();
  return id ? <Detalle id={id} /> : <Lista />;
}
