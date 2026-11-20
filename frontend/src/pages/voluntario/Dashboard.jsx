import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { voluntariosApi } from '../../services/api';
import Estado from '../../components/Estado';
import { fecha, fechaActividad, horas as formatoHoras } from '../../utils/formato';

export default function VoluntarioDashboard() {
  const { usuario } = useAuth();
  const [perfil, setPerfil] = useState(null);
  const [asignaciones, setAsignaciones] = useState([]);
  const [loading, setLoading] = useState(true);
  const [noRegistrado, setNoRegistrado] = useState(false);

  useEffect(() => {
    Promise.allSettled([voluntariosApi.miHistorial(), voluntariosApi.misAsignaciones()])
      .then(([h, a]) => {
        if (h.status === 'fulfilled') setPerfil(h.value.data);
        else if (h.reason.response?.status === 404) setNoRegistrado(true);
        if (a.status === 'fulfilled') setAsignaciones(a.value.data);
      })
      .finally(() => setLoading(false));
  }, []);

  // FUJ-118: las asignaciones que aún no terminaron se muestran como próximas
  const proximas = asignaciones.filter(a => !a.finalizada);

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Portal del Voluntario</h1>
          <p>Bienvenido/a, {usuario?.nombreCompleto}</p>
        </div>
        {perfil && (
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <div className="stat-box">
              <p>Horas aportadas</p>
              <h2>{formatoHoras(perfil.totalHoras)}</h2>
            </div>
            <div className="stat-box">
              <p>Actividades registradas</p>
              <h2>{perfil.historial.length}</h2>
            </div>
          </div>
        )}
      </div>

      {noRegistrado && (
        <div className="info-msg">
          Tu cuenta aún no está vinculada al registro de voluntarios. Pide al encargado de eventos que la vincule
          o <Link to="/voluntariado" style={{ textDecoration: 'underline' }}>postúlate con el mismo email</Link>.
        </div>
      )}

      <div className="split">
        <div>
          <section className="panel">
            <h2>Mis próximas asignaciones</h2>
            {loading ? (
              <p>Cargando...</p>
            ) : proximas.length === 0 ? (
              <p className="muted">No tienes asignaciones próximas. El coordinador te avisará por email cuando te asigne a una actividad.</p>
            ) : (
              <ul style={{ listStyle: 'none', display: 'flex', flexDirection: 'column', gap: 12 }}>
                {proximas.map(a => (
                  <li key={a.id} style={{ borderLeft: '3px solid #0284c7', paddingLeft: 12 }}>
                    <strong>{a.tipo === 'CURSO' ? '📚' : '📅'} {a.actividad}</strong>
                    <div className="muted" style={{ fontSize: '0.9rem' }}>
                      {fechaActividad(a)}{a.lugar ? ` · 📍 ${a.lugar}` : ''}
                    </div>
                    {a.tarea && <div>Tarea: {a.tarea}</div>}
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="panel">
            <h2>Mis participaciones</h2>
            {loading ? (
              <p>Cargando...</p>
            ) : !perfil || perfil.historial.length === 0 ? (
              <p className="muted">
                Aún no hay actividades registradas. El coordinador registra tus horas después de cada actividad.
              </p>
            ) : (
              <ul style={{ listStyle: 'none', display: 'flex', flexDirection: 'column', gap: 12 }}>
                {perfil.historial.map(h => (
                  <li key={h.id} style={{ borderLeft: '3px solid #f59e0b', paddingLeft: 12 }}>
                    <strong>{fecha(h.fecha)}</strong>
                    {(h.eventoTitulo || h.cursoTitulo) && <span className="muted"> · {h.eventoTitulo || h.cursoTitulo}</span>}
                    {h.horas != null && <span className="badge badge-ok" style={{ marginLeft: 8 }}>{formatoHoras(h.horas)}</span>}
                    <div>{h.descripcion}</div>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>

        <aside>
          {perfil && (
            <section className="panel">
              <h2>Mis datos</h2>
              <p><strong>Estado:</strong> <Estado valor={perfil.estado} /></p>
              <p><strong>Email:</strong> {perfil.email}</p>
              <p><strong>Teléfono:</strong> {perfil.telefono || '-'}</p>
              <p><strong>Voluntario/a desde:</strong> {fecha(perfil.postuladoEn)}</p>
            </section>
          )}
          <section className="panel">
            <h2>Formación</h2>
            <p className="muted" style={{ marginBottom: 12 }}>Como voluntario/a también puedes tomar cursos de la fundación.</p>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <Link to="/mi-cuenta" className="btn-primary">Mi panel</Link>
              <Link to="/voluntario/certificados" className="btn-secondary">Mis certificados</Link>
            </div>
          </section>
        </aside>
      </div>
    </div>
  );
}
