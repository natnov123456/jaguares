import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { voluntariosApi } from '../../services/api';
import Estado from '../../components/Estado';
import { fecha } from '../../utils/formato';

export default function VoluntarioDashboard() {
  const { usuario } = useAuth();
  const [perfil, setPerfil] = useState(null);
  const [loading, setLoading] = useState(true);
  const [noRegistrado, setNoRegistrado] = useState(false);

  useEffect(() => {
    voluntariosApi.miHistorial()
      .then(r => setPerfil(r.data))
      .catch(err => { if (err.response?.status === 404) setNoRegistrado(true); })
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Portal del Voluntario</h1>
          <p>Bienvenido/a, {usuario?.nombreCompleto}</p>
        </div>
        {perfil && (
          <div className="stat-box">
            <p>Actividades registradas</p>
            <h2>{perfil.historial.length}</h2>
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
        <section className="panel">
          <h2>Mis participaciones</h2>
          {loading ? (
            <p>Cargando...</p>
          ) : !perfil || perfil.historial.length === 0 ? (
            <p className="muted">
              Aún no hay actividades registradas. El encargado de eventos registra tu participación después de cada actividad.
            </p>
          ) : (
            <ul style={{ listStyle: 'none', display: 'flex', flexDirection: 'column', gap: 12 }}>
              {perfil.historial.map(h => (
                <li key={h.id} style={{ borderLeft: '3px solid #f59e0b', paddingLeft: 12 }}>
                  <strong>{fecha(h.fecha)}</strong>
                  {h.eventoTitulo && <span className="muted"> · {h.eventoTitulo}</span>}
                  <div>{h.descripcion}</div>
                </li>
              ))}
            </ul>
          )}
        </section>

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
              <Link to="/mi-cuenta" className="btn-primary">Mis cursos</Link>
              <Link to="/voluntario/certificados" className="btn-secondary">Mis certificados</Link>
            </div>
          </section>
        </aside>
      </div>
    </div>
  );
}
