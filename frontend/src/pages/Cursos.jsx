import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { cursosApi } from '../services/api';
import { useAuth } from '../context/AuthContext';
import { puede } from '../config/permisos';
import { fecha } from '../utils/formato';
import BarraFiltros from '../components/filtros/BarraFiltros';
import useFiltros from '../hooks/useFiltros';
import { aplicarFiltros } from '../utils/filtros';

const DISPONIBILIDAD = [{ valor: 'CON_CUPO', etiqueta: 'Con cupos' }, { valor: 'LLENO', etiqueta: 'Llenos (lista de espera)' }];

export default function Cursos() {
  const [cursos, setCursos] = useState([]);
  const [inscritos, setInscritos] = useState(new Set());
  const [espera, setEspera] = useState(new Map()); // cursoId -> { posicion, totalEnEspera }
  const [procesando, setProcesando] = useState(null);
  const [msg, setMsg] = useState(null);
  const { usuario } = useAuth();
  const navigate = useNavigate();
  const puedeInscribirse = puede(usuario, 'inscribirseCursos');
  const { valores, cambiar, limpiar } = useFiltros();
  // RNF-03: el curso elegido antes de iniciar sesión o registrarse (?inscribir=id) se confirma con un clic al volver
  const [params, setParams] = useSearchParams();
  const pendienteId = params.get('inscribir');
  const quitarPendiente = () => setParams(p => { const n = new URLSearchParams(p); n.delete('inscribir'); return n; }, { replace: true });

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

  const inscribir = (id) => {
    if (id === pendienteId) quitarPendiente();
    return ejecutar(id, cursosApi.inscribir, () => 'Inscripción exitosa ✅ La verás en "Mi panel".');
  };

  const unirse = (id) => ejecutar(id, cursosApi.unirseListaEspera,
    (d) => `Te anotaste en la lista de espera ⏳ Tu posición es la ${d.posicion}. Si se libera un cupo quedarás inscrito/a automáticamente; lo verás en Mi panel.`);

  const salir = (c) => {
    if (!window.confirm(`¿Salir de la lista de espera de "${c.titulo}"? Perderás tu posición.`)) return;
    ejecutar(c.id, cursosApi.salirListaEspera, () => 'Saliste de la lista de espera.');
  };

  const accion = (c) => {
    // Visitante: se le pide iniciar sesión y vuelve aquí después
    if (!usuario) {
      return (
        <div>
          <button className="btn-primary" onClick={() => navigate('/login', { state: { desde: `/cursos?inscribir=${c.id}` } })}>
            Inscribirme
          </button>
          <p className="muted" style={{ fontSize: '0.8rem', marginTop: 4 }}>Te pediremos iniciar sesión o crear tu cuenta (1 minuto).</p>
        </div>
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

  const visibles = aplicarFiltros(cursos, valores, {
    texto: c => [c.titulo, c.descripcion], estado: c => (c.cupoDisponible > 0 ? 'CON_CUPO' : 'LLENO'), fecha: c => c.fechaInicio,
  });
  const pendiente = puedeInscribirse && pendienteId && !inscritos.has(pendienteId) && !espera.has(pendienteId)
    ? cursos.find(c => c.id === pendienteId) : null;

  return (
    <div className="container" style={{ padding: '32px 24px' }}>
      <h1>Cursos y talleres</h1>
      {usuario && !puedeInscribirse && (
        <div className="info-msg" style={{ marginTop: 12 }}>
          Estás viendo el catálogo público. Los cursos se gestionan desde el <Link to="/admin" style={{ textDecoration: 'underline' }}>Panel</Link>.
        </div>
      )}
      {msg && <div className={`${msg.tipo}-msg`} style={{ marginTop: 12 }} role="status">{msg.texto}</div>}
      {pendiente && (
        <div className="info-msg" style={{ marginTop: 12, display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }} role="status">
          <span>Estás por inscribirte en <strong>{pendiente.titulo}</strong> ({fecha(pendiente.fechaInicio)} – {fecha(pendiente.fechaFin)}).</span>
          {pendiente.cupoDisponible > 0 ? (
            <button className="btn-primary" disabled={procesando === pendiente.id} onClick={() => inscribir(pendiente.id)}>
              {procesando === pendiente.id ? 'Inscribiendo...' : 'Confirmar inscripción'}
            </button>
          ) : (
            <button className="btn-secondary" onClick={() => { quitarPendiente(); unirse(pendiente.id); }}>Unirme a la lista de espera</button>
          )}
          <button className="btn-link" onClick={quitarPendiente}>Ahora no</button>
        </div>
      )}
      {cursos.length === 0 && <p className="muted" style={{ marginTop: 16 }}>No hay cursos abiertos en este momento.</p>}
      {cursos.length > 0 && (
        <div style={{ marginTop: 16 }}>
          <BarraFiltros valores={valores} cambiar={cambiar} limpiar={limpiar} texto="Nombre o tema del curso" estados={DISPONIBILIDAD}
                        etiquetaEstado="Disponibilidad" fechas="Empieza" total={cursos.length} visibles={visibles.length} />
        </div>
      )}
      {cursos.length > 0 && visibles.length === 0 && <p className="muted">Ningún curso coincide con la búsqueda.</p>}
      <div className="grid">
        {visibles.map(c => (
          <div key={c.id} className="card" style={pendienteId === c.id ? { outline: '3px solid #f59e0b' } : undefined}>
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
