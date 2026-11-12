import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { cursosApi } from '../services/api';
import { useAuth } from '../context/AuthContext';
import { puede } from '../config/permisos';
import { fecha } from '../utils/formato';

export default function Cursos() {
  const [cursos, setCursos] = useState([]);
  const [inscritos, setInscritos] = useState(new Set());
  const [espera, setEspera] = useState(new Map()); // cursoId -> { posicion, totalEnEspera }
  const [procesando, setProcesando] = useState(null);
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
      cursosApi.miListaEspera()
        .then(r => setEspera(new Map(r.data.map(l => [l.cursoId, l]))))
        .catch(console.error);
    }
  };

  useEffect(cargar, [puedeInscribirse]);

  const ejecutar = async (id, accion, exito) => {
    setProcesando(id);
    setMsg(null);
    try {
      const { data } = await accion(id);
      setMsg({ tipo: 'success', texto: exito(data) });
      cargar();
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo completar la operación' });
      cargar(); // el cupo pudo cambiar mientras tanto: se refresca la tarjeta
    } finally {
      setProcesando(null);
    }
  };

  const inscribir = (id) => ejecutar(id, cursosApi.inscribir,
    () => 'Inscripción exitosa ✅ ');

  const unirse = (id) => ejecutar(id, cursosApi.unirseListaEspera,
    (d) => `Te anotaste en la lista de espera ⏳ Tu posición es la ${d.posicion}. Si se libera un cupo quedarás inscrito/a automáticamente y te avisaremos por email.`);

  const salir = (c) => {
    if (!window.confirm(`¿Salir de la lista de espera de "${c.titulo}"? Perderás tu posición.`)) return;
    ejecutar(c.id, cursosApi.salirListaEspera, () => 'Saliste de la lista de espera.');
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

    const ocupado = procesando === c.id;
    const lugar = espera.get(c.id);
    if (lugar) {
      return (
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <span className="badge badge-warn">⏳ En lista de espera · posición {lugar.posicion} de {lugar.totalEnEspera}</span>
          <button className="btn-secondary" disabled={ocupado} onClick={() => salir(c)}>Salir de la lista</button>
        </div>
      );
    }
    if (c.cupoDisponible === 0) {
      return (
        <button className="btn-secondary" disabled={ocupado} onClick={() => unirse(c.id)}>
          {ocupado ? 'Anotando...' : 'Unirme a la lista de espera'}
        </button>
      );
    }
    return (
      <button onClick={() => inscribir(c.id)} disabled={ocupado} className="btn-primary">
        {ocupado ? 'Inscribiendo...' : 'Inscribirme'}
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
      {msg && <div className={`${msg.tipo}-msg`} style={{ marginTop: 12 }} role="status">{msg.texto}</div>}
      {cursos.length === 0 && <p className="muted" style={{ marginTop: 16 }}>No hay cursos abiertos en este momento.</p>}
      <div className="grid">
        {cursos.map(c => (
          <div key={c.id} className="card">
            <div className="card-body" style={{ display: 'flex', flexDirection: 'column', gap: 8, height: '100%' }}>
              <h3>{c.titulo}</h3>
              <p style={{ flex: 1 }}>{c.descripcion}</p>
              <p className="muted" style={{ fontSize: '0.9rem' }}>📅 {fecha(c.fechaInicio)} – {fecha(c.fechaFin)}</p>
              <p>
                <strong>Cupos:</strong> {c.cupoDisponible} / {c.cupoMaximo}
                {c.cupoDisponible === 0 && <span className="badge badge-off" style={{ marginLeft: 8 }}>Curso lleno</span>}
              </p>
              <div>{accion(c)}</div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
