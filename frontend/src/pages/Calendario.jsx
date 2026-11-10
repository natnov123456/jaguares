import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { actividadesApi, cursosApi, eventosApi } from '../services/api';
import { useAuth } from '../context/AuthContext';
import { puede } from '../config/permisos';
import { fecha, fechaHora, MESES } from '../utils/formato';
import './Calendario.css';

// FUJ-114: calendario público de cursos y eventos (vista mensual y semanal)

const DIAS = ['Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb', 'Dom'];

const pad = (n) => String(n).padStart(2, '0');
// Clave de día en hora local (no UTC) para agrupar actividades por celda
const claveDia = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const sumarDias = (d, n) => { const r = new Date(d); r.setDate(r.getDate() + n); return r; };
const lunesDe = (d) => { const r = new Date(d.getFullYear(), d.getMonth(), d.getDate()); return sumarDias(r, -((r.getDay() + 6) % 7)); };

function rangoVista(vista, ref) {
  if (vista === 'semana') {
    const inicio = lunesDe(ref);
    return { inicio, fin: sumarDias(inicio, 6) };
  }
  const primero = new Date(ref.getFullYear(), ref.getMonth(), 1);
  const ultimo = new Date(ref.getFullYear(), ref.getMonth() + 1, 0);
  const inicio = lunesDe(primero);
  return { inicio, fin: sumarDias(lunesDe(ultimo), 6) };
}

function hora(a) {
  if (a.todoElDia) return 'Todo el día';
  return new Date(a.inicio).toLocaleTimeString('es-BO', { hour: '2-digit', minute: '2-digit' });
}

function Chip({ actividad, onClick }) {
  return (
    <button type="button" className={`cal-chip cal-${actividad.tipo.toLowerCase()}`} onClick={() => onClick(actividad)}
            title={actividad.titulo}>
      {!actividad.todoElDia && <span className="cal-chip-hora">{hora(actividad)}</span>}
      {actividad.titulo}
    </button>
  );
}

export default function Calendario() {
  const { usuario } = useAuth();
  const navigate = useNavigate();
  const puedeInscribirse = puede(usuario, 'inscribirseCursos');
  const puedeInscribirseEventos = puede(usuario, 'inscribirseEventos');

  const [vista, setVista] = useState('mes');
  const [ref, setRef] = useState(() => new Date());
  const [actividades, setActividades] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [diaSel, setDiaSel] = useState(() => claveDia(new Date()));
  const [detalle, setDetalle] = useState(null);
  const [cursosInscritos, setCursosInscritos] = useState(new Set());
  const [eventosInscritos, setEventosInscritos] = useState(new Set());
  const [msg, setMsg] = useState(null);

  const { inicio, fin } = useMemo(() => rangoVista(vista, ref), [vista, ref]);
  const desde = claveDia(inicio);
  const hasta = claveDia(fin);

  // Incrementar 'recarga' vuelve a pedir el rango actual (p. ej. tras inscribirse, para refrescar cupos)
  const [recarga, setRecarga] = useState(0);
  const cargar = () => setRecarga(n => n + 1);

  useEffect(() => {
    const ctrl = new AbortController();
    actividadesApi.rango(desde, hasta, ctrl.signal)
      .then(r => { setActividades(r.data); setError(null); })
      .catch(() => { if (!ctrl.signal.aborted) setError('No se pudo cargar el calendario'); })
      .finally(() => { if (!ctrl.signal.aborted) setLoading(false); });
    return () => ctrl.abort();
  }, [desde, hasta, recarga]);

  useEffect(() => {
    if (!puedeInscribirse) return;
    cursosApi.misInscripciones()
      .then(r => setCursosInscritos(new Set(r.data.filter(i => i.estadoInscripcion !== 'CANCELADA').map(i => i.cursoId))))
      .catch(console.error);
  }, [puedeInscribirse]);

  useEffect(() => {
    if (!puedeInscribirseEventos) return;
    eventosApi.misInscripciones()
      .then(r => setEventosInscritos(new Set(r.data.map(i => i.eventoId))))
      .catch(console.error);
  }, [puedeInscribirseEventos]);

  const porDia = useMemo(() => {
    const mapa = {};
    for (const a of actividades) (mapa[claveDia(new Date(a.inicio))] ??= []).push(a);
    return mapa;
  }, [actividades]);

  const dias = [];
  for (let d = inicio; d <= fin; d = sumarDias(d, 1)) dias.push(d);

  const mover = (paso) => {
    setLoading(true);
    const r = new Date(ref);
    if (vista === 'mes') r.setMonth(r.getMonth() + paso, 1);
    else r.setDate(r.getDate() + paso * 7);
    setRef(r);
  };

  const [ahora] = useState(() => Date.now());
  const hoyClave = claveDia(new Date(ahora));
  const titulo = vista === 'mes'
    ? `${MESES[ref.getMonth()]} ${ref.getFullYear()}`
    : `${fecha(claveDia(inicio))} – ${fecha(claveDia(fin))}`;

  const inscribirCurso = async (a) => {
    setMsg(null);
    try {
      await cursosApi.inscribir(a.id);
      setCursosInscritos(new Set([...cursosInscritos, a.id]));
      setMsg({ tipo: 'success', texto: '✅ Inscripción exitosa. Puedes verla en "Mi panel".' });
      cargar();
      setDetalle({ ...a, cupoDisponible: Math.max(0, (a.cupoDisponible ?? 1) - 1) });
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo completar la inscripción' });
    }
  };

  const inscribirEvento = async (a) => {
    setMsg(null);
    try {
      await eventosApi.inscribir(a.id);
      setEventosInscritos(new Set([...eventosInscritos, a.id]));
      setMsg({ tipo: 'success', texto: '✅ Inscripción confirmada. Tu código QR de ingreso está en "Mi panel".' });
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo completar la inscripción' });
    }
  };

  const accionEvento = (a) => {
    if (new Date(a.fin ?? a.inicio).getTime() < ahora) return <span className="badge">Evento realizado</span>;
    if (!usuario) {
      return (
        <button className="btn-primary" onClick={() => navigate('/login', { state: { desde: '/calendario' } })}>
          Inicia sesión para inscribirte
        </button>
      );
    }
    if (!puedeInscribirseEventos) return null;
    if (eventosInscritos.has(a.id)) return <Link to="/mi-cuenta" className="badge badge-ok">✓ Inscrito/a · ver mi QR</Link>;
    return <button className="btn-primary" onClick={() => inscribirEvento(a)}>Inscribirme al evento</button>;
  };

  const accionDetalle = (a) => {
    if (a.tipo === 'EVENTO') return accionEvento(a);
    if (!usuario) {
      return (
        <button className="btn-primary" onClick={() => navigate('/login', { state: { desde: '/calendario' } })}>
          Inicia sesión para inscribirte
        </button>
      );
    }
    if (!puedeInscribirse) return null;
    if (cursosInscritos.has(a.id)) return <span className="badge badge-ok">✓ Ya estás inscrito/a</span>;
    return (
      <button className="btn-primary" disabled={a.cupoDisponible === 0} onClick={() => inscribirCurso(a)}>
        {a.cupoDisponible === 0 ? 'Sin cupos' : 'Inscribirme'}
      </button>
    );
  };

  const abrir = (a) => { setMsg(null); setDetalle(a); };
  const agendaDia = porDia[diaSel] ?? [];

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Calendario de actividades</h1>
          <p>Cursos, talleres y eventos de la fundación. Selecciona una actividad para ver el detalle.</p>
        </div>
        <div className="cal-leyenda">
          <span><i className="cal-dot cal-curso" /> Cursos</span>
          <span><i className="cal-dot cal-evento" /> Eventos</span>
        </div>
      </div>

      <section className="panel">
        <div className="cal-toolbar">
          <div className="cal-nav">
            <button className="btn-secondary" onClick={() => mover(-1)} aria-label="Anterior">‹</button>
            <button className="btn-secondary" onClick={() => { setLoading(true); setRef(new Date()); setDiaSel(hoyClave); }}>Hoy</button>
            <button className="btn-secondary" onClick={() => mover(1)} aria-label="Siguiente">›</button>
            <h2 className="cal-titulo">{titulo}</h2>
          </div>
          <div className="cal-vistas" role="group" aria-label="Vista">
            <button className={vista === 'mes' ? 'activo' : ''} onClick={() => setVista('mes')}>Mes</button>
            <button className={vista === 'semana' ? 'activo' : ''} onClick={() => setVista('semana')}>Semana</button>
          </div>
        </div>

        {error && <div className="error-msg">{error}</div>}

        <div className={`cal-grid cal-${vista} ${loading ? 'cal-cargando' : ''}`}>
          {DIAS.map(d => <div key={d} className="cal-cabecera">{d}</div>)}
          {dias.map(d => {
            const clave = claveDia(d);
            const items = porDia[clave] ?? [];
            const fuera = vista === 'mes' && d.getMonth() !== ref.getMonth();
            return (
              <div key={clave}
                   className={`cal-celda ${fuera ? 'fuera' : ''} ${clave === hoyClave ? 'hoy' : ''} ${clave === diaSel ? 'sel' : ''}`}
                   onClick={() => setDiaSel(clave)}>
                <span className="cal-num">{vista === 'semana' ? `${DIAS[(d.getDay() + 6) % 7]} ${d.getDate()}` : d.getDate()}</span>
                <div className="cal-items">
                  {items.map(a => <Chip key={a.tipo + a.id} actividad={a} onClick={abrir} />)}
                </div>
                {items.length > 0 && (
                  <div className="cal-puntos" aria-hidden="true">
                    {items.slice(0, 4).map(a => <i key={a.tipo + a.id} className={`cal-dot cal-${a.tipo.toLowerCase()}`} />)}
                  </div>
                )}
              </div>
            );
          })}
        </div>

        {/* En celular las celdas solo muestran puntos: la agenda del día seleccionado va debajo */}
        <div className="cal-agenda">
          <h3>{fecha(diaSel)}</h3>
          {agendaDia.length === 0 ? (
            <p className="muted">Sin actividades este día.</p>
          ) : agendaDia.map(a => (
            <button key={a.tipo + a.id} className="cal-agenda-item" onClick={() => abrir(a)}>
              <i className={`cal-dot cal-${a.tipo.toLowerCase()}`} />
              <span><strong>{a.titulo}</strong><br /><small className="muted">{hora(a)}{a.lugar ? ` · ${a.lugar}` : ''}</small></span>
            </button>
          ))}
        </div>
      </section>

      {detalle && (
        <div className="modal-fondo" onClick={() => setDetalle(null)}>
          <div className="modal" role="dialog" aria-modal="true" aria-label={detalle.titulo} onClick={e => e.stopPropagation()}>
            <button className="modal-cerrar" onClick={() => setDetalle(null)} aria-label="Cerrar">×</button>
            <span className={`badge ${detalle.tipo === 'CURSO' ? 'badge-warn' : 'badge-ok'}`}>
              {detalle.tipo === 'CURSO' ? 'Curso / taller' : 'Evento'}
            </span>
            <h2 style={{ margin: '8px 0' }}>{detalle.titulo}</h2>
            {detalle.descripcion && <p style={{ whiteSpace: 'pre-line' }}>{detalle.descripcion}</p>}
            <ul className="modal-datos">
              {detalle.todoElDia ? (
                <li>📅 {fecha(detalle.inicio.slice(0, 10))}{detalle.fin && ` – ${fecha(detalle.fin.slice(0, 10))}`}</li>
              ) : (
                <li>📅 {fechaHora(detalle.inicio)}{detalle.fin && ` – ${fechaHora(detalle.fin)}`}</li>
              )}
              {detalle.lugar && <li>📍 {detalle.lugar}</li>}
              {detalle.tipo === 'CURSO' && (
                <li>👥 {detalle.cupoDisponible} de {detalle.cupoMaximo} cupos disponibles</li>
              )}
            </ul>
            {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}
            <div className="modal-acciones">{accionDetalle(detalle)}</div>
          </div>
        </div>
      )}
    </div>
  );
}
