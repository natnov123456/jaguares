import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { cursosApi } from '../services/api';
import { useAuth } from '../context/AuthContext';
import { puede } from '../config/permisos';
import { fecha } from '../utils/formato';

export default function Cursos() {
  const [cursos, setCursos] = useState([]);
  const [inscritos, setInscritos] = useState(new Set());
  const [msg, setMsg] = useState(null);
  const { usuario } = useAuth();
  const navigate = useNavigate();
  const puedeInscribirse = puede(usuario, 'inscribirseCursos');

  const cargar = () => {
    cursosApi.listar().then(r => setCursos(r.data)).catch(console.error);
    if (puedeInscribirse) {
      cursosApi.misInscripciones()
        .then(r => setInscritos(new Set(r.data.map(i => i.cursoId))))
        .catch(console.error);
    }
  };

  useEffect(cargar, [puedeInscribirse]);

  const inscribir = async (id) => {
    try {
      await cursosApi.inscribir(id);
      setMsg({ tipo: 'success', texto: 'Inscripción exitosa ✅ Te enviamos la confirmación por email.' });
      cargar();
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'Error al inscribirse' });
    }
  };

  const accion = (c) => {
    // Visitante: se le pide iniciar sesión y vuelve aquí después
    if (!usuario) {
      return (
        <button className="btn-primary" onClick={() => navigate('/login', { state: { desde: '/cursos' } })}>
          Inicia sesión para inscribirte
        </button>
      );
    }
    // Staff: los cursos se gestionan desde el panel, no se inscriben
    if (!puedeInscribirse) return null;
    if (inscritos.has(c.id)) return <Link to="/mi-cuenta" className="badge badge-ok">✓ Ya estás inscrito/a</Link>;
    return (
      <button onClick={() => inscribir(c.id)} disabled={c.cupoDisponible === 0} className="btn-primary">
        {c.cupoDisponible === 0 ? 'Sin cupos' : 'Inscribirme'}
      </button>
    );
  };

  return (
    <div className="container" style={{ padding: '32px 24px' }}>
      <h1>Cursos y talleres</h1>
      {usuario && !puedeInscribirse && (
        <div className="info-msg" style={{ marginTop: 12 }}>
          Estás viendo el catálogo público. Los cursos se gestionan desde el <Link to="/admin" style={{ textDecoration: 'underline' }}>Panel</Link>.
        </div>
      )}
      {msg && <div className={`${msg.tipo}-msg`} style={{ marginTop: 12 }}>{msg.texto}</div>}
      {cursos.length === 0 && <p className="muted" style={{ marginTop: 16 }}>No hay cursos abiertos en este momento.</p>}
      <div className="grid">
        {cursos.map(c => (
          <div key={c.id} className="card">
            <div className="card-body" style={{ display: 'flex', flexDirection: 'column', gap: 8, height: '100%' }}>
              <h3>{c.titulo}</h3>
              <p style={{ flex: 1 }}>{c.descripcion}</p>
              <p className="muted" style={{ fontSize: '0.9rem' }}>📅 {fecha(c.fechaInicio)} – {fecha(c.fechaFin)}</p>
              <p><strong>Cupos:</strong> {c.cupoDisponible} / {c.cupoMaximo}</p>
              <div>{accion(c)}</div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
