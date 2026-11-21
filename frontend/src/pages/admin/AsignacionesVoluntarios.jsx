import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { voluntariosApi } from '../../services/api';
import Estado from '../../components/Estado';
import { fechaActividad, horas as formatoHoras, horario, horasDeHorario } from '../../utils/formato';
import BarraFiltros from '../../components/filtros/BarraFiltros';
import BotonExportar from '../../components/BotonExportar';
import useFiltros from '../../hooks/useFiltros';
import { aplicarFiltros, ESTADOS_ASIGNACION } from '../../utils/filtros';

// FUJ-118: el coordinador asigna voluntarios a eventos o cursos publicados y, cuando la actividad
// termina, registra las horas de cada uno (se suman a su historial).
const VISTAS = [
  { id: 'pendientes', titulo: 'Horas por registrar', filtro: a => a.finalizada && a.estado === 'ASIGNADA' },
  { id: 'proximas', titulo: 'Próximas', filtro: a => !a.finalizada },
  { id: 'completadas', titulo: 'Completadas', filtro: a => a.estado === 'COMPLETADA' },
];

const claveActividad = (a) => `${a.tipo}:${a.id}`;

export default function AsignacionesVoluntarios() {
  const [actividades, setActividades] = useState([]);
  const [voluntarios, setVoluntarios] = useState([]);
  const [asignaciones, setAsignaciones] = useState([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState(null);

  const [actividad, setActividad] = useState('');
  const [seleccion, setSeleccion] = useState(new Set());
  const [tarea, setTarea] = useState('');
  const [busqueda, setBusqueda] = useState('');
  const [enviando, setEnviando] = useState(false);

  const [vista, setVista] = useState('pendientes');
  const { valores, cambiar, limpiar } = useFiltros();
  const [horasEditadas, setHorasEditadas] = useState({});
  const [guardando, setGuardando] = useState(null);

  const cargar = () =>
    Promise.all([voluntariosApi.actividadesAsignables(), voluntariosApi.listar(), voluntariosApi.asignaciones()])
      .then(([a, v, s]) => {
        setActividades(a.data);
        setVoluntarios(v.data.filter(x => x.estado === 'ACTIVO'));
        setAsignaciones(s.data);
      })
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudieron cargar los datos de voluntariado' }))
      .finally(() => setLoading(false));

  useEffect(() => { cargar(); }, []);

  const elegida = actividades.find(a => claveActividad(a) === actividad);

  // Quienes ya están asignados a la actividad elegida no se pueden volver a marcar
  const yaAsignados = useMemo(() => new Set(
    elegida ? asignaciones.filter(a => a.tipo === elegida.tipo && a.actividadId === elegida.id).map(a => a.voluntarioId) : []
  ), [elegida, asignaciones]);

  const visibles = voluntarios.filter(v =>
    `${v.nombreCompleto} ${v.email}`.toLowerCase().includes(busqueda.trim().toLowerCase()));

  const alternar = (id) => setSeleccion(s => {
    const n = new Set(s);
    if (n.has(id)) n.delete(id); else n.add(id);
    return n;
  });

  const asignar = async (e) => {
    e.preventDefault();
    if (!elegida || seleccion.size === 0) return;
    setEnviando(true);
    setMsg(null);
    try {
      const { data } = await voluntariosApi.asignar({
        voluntarioIds: [...seleccion],
        eventoId: elegida.tipo === 'EVENTO' ? elegida.id : null,
        cursoId: elegida.tipo === 'CURSO' ? elegida.id : null,
        tarea: tarea || null,
      });
      const partes = [];
      if (data.asignadas.length) partes.push(`✅ ${data.asignadas.length} voluntario(s) asignado(s) y notificado(s).`);
      if (data.omitidas.length) partes.push(`No se asignó a: ${data.omitidas.map(o => o.motivo).join('; ')}.`);
      setMsg({ tipo: data.asignadas.length ? 'success' : 'error', texto: partes.join(' ') });
      setSeleccion(new Set());
      setTarea('');
      setVista('proximas');
      cargar();
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo asignar' });
    } finally {
      setEnviando(false);
    }
  };

  // Horario que se está editando para una asignación (o el ya registrado)
  const horarioDe = (a) => horasEditadas[a.id] ?? { inicio: a.horaInicio ?? '', fin: a.horaFin ?? '', dias: a.dias ?? 1 };
  const editarHorario = (a, campo, valor) => setHorasEditadas(h => ({ ...h, [a.id]: { ...horarioDe(a), [campo]: valor } }));

  const guardarHoras = async (a) => {
    const { inicio, fin, dias } = horarioDe(a);
    const total = horasDeHorario(inicio, fin, dias);
    if (total === null) {
      setMsg({ tipo: 'error', texto: 'Indica la hora de inicio y la de fin; la de fin debe ser posterior (ej. de 15:30 a 18:15).' });
      return;
    }
    if (total > 200) { setMsg({ tipo: 'error', texto: 'El total no puede superar 200 horas.' }); return; }
    setGuardando(a.id);
    setMsg(null);
    try {
      await voluntariosApi.registrarHoras(a.id, { horaInicio: inicio, horaFin: fin, dias: Number(dias) || 1 });
      setMsg({ tipo: 'success', texto: `Horas de ${a.voluntario} registradas: ${horario(inicio, fin, Number(dias))} = ${formatoHoras(total)}. Se sumaron a su historial.` });
      setHorasEditadas(h => { const n = { ...h }; delete n[a.id]; return n; });
      cargar();
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudieron registrar las horas' });
    } finally {
      setGuardando(null);
    }
  };

  const quitar = async (a) => {
    if (!window.confirm(`¿Quitar a ${a.voluntario} de "${a.actividad}"? Se le avisará por email.`)) return;
    setMsg(null);
    try {
      await voluntariosApi.cancelarAsignacion(a.id);
      setMsg({ tipo: 'success', texto: 'Asignación cancelada' });
      cargar();
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo cancelar la asignación' });
    }
  };

  // Agrupadas por actividad dentro de la vista elegida
  const filtro = VISTAS.find(v => v.id === vista).filtro;
  const grupos = useMemo(() => {
    const mapa = new Map();
    const filtradas = aplicarFiltros(asignaciones.filter(filtro), valores,
      { texto: a => [a.voluntario, a.email, a.actividad, a.tarea, a.lugar], estado: a => a.estado, fecha: a => a.inicio });
    for (const a of filtradas) {
      const k = `${a.tipo}:${a.actividadId}`;
      if (!mapa.has(k)) mapa.set(k, { ...a, filas: [] });
      mapa.get(k).filas.push(a);
    }
    return [...mapa.values()];
  }, [asignaciones, filtro, valores]);

  const pendientes = asignaciones.filter(VISTAS[0].filtro).length;

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Asignación de voluntarios</h1>
          <p>Asigna voluntarios a eventos y cursos publicados. Cuando la actividad termina, registra sus horas.</p>
        </div>
        <div style={{ display: 'flex', gap: 16, alignItems: 'center', flexWrap: 'wrap' }}>
          <div className="stat-box">
            <p>Horas por registrar</p>
            <h2>{pendientes}</h2>
          </div>
          <Link to="/admin/voluntarios" className="btn-secondary">Registro de voluntarios</Link>
        </div>
      </div>

      {msg && <div className={`${msg.tipo}-msg`} role="status">{msg.texto}</div>}

      {loading ? <p>Cargando...</p> : (
        <div className="split">
          <section className="panel">
            <div className="pestanas" role="tablist" aria-label="Asignaciones">
              {VISTAS.map(v => (
                <button key={v.id} type="button" role="tab" aria-selected={vista === v.id}
                        className={`pestana ${vista === v.id ? 'activa' : ''}`} onClick={() => setVista(v.id)}>
                  {v.titulo} ({asignaciones.filter(v.filtro).length})
                </button>
              ))}
            </div>

            <BarraFiltros valores={valores} cambiar={cambiar} limpiar={limpiar} texto="Voluntario, actividad o tarea"
                          estados={ESTADOS_ASIGNACION} fechas="Actividad" total={asignaciones.filter(filtro).length}
                          visibles={grupos.reduce((n, g) => n + g.filas.length, 0)}>
              <BotonExportar reporte="asignaciones-voluntariado" filtros={valores} />
            </BarraFiltros>

            {grupos.length === 0 ? (
              <p className="muted" style={{ padding: 16, textAlign: 'center' }}>
                {vista === 'pendientes' ? 'No hay horas pendientes de registrar.' : 'No hay asignaciones en esta vista.'}
              </p>
            ) : grupos.map(g => (
              <div key={`${g.tipo}:${g.actividadId}`} style={{ marginBottom: 20 }}>
                <h3 style={{ fontSize: '1.05rem' }}>
                  {g.tipo === 'CURSO' ? '📚' : '📅'} {g.actividad}
                </h3>
                <p className="muted" style={{ fontSize: '0.9rem', marginBottom: 8 }}>
                  {fechaActividad(g)}{g.lugar ? ` · 📍 ${g.lugar}` : ''}
                </p>
                <div className="table-wrap">
                  <table className="table">
                    <thead><tr><th>Voluntario/a</th><th>Tarea</th><th>Estado</th><th>Horas</th></tr></thead>
                    <tbody>
                      {g.filas.map(a => (
                        <tr key={a.id}>
                          <td><strong>{a.voluntario}</strong><div className="muted" style={{ fontSize: '0.85rem' }}>{a.email}</div></td>
                          <td>{a.tarea || <span className="muted">—</span>}</td>
                          <td><Estado valor={a.estado} /></td>
                          <td>
                            {a.finalizada ? (
                              <form onSubmit={(e) => { e.preventDefault(); guardarHoras(a); }} className="horario-form">
                                <label className="horario-campo">de
                                  <input type="time" required aria-label={`Hora de inicio de ${a.voluntario}`}
                                         value={horarioDe(a).inicio} onChange={e => editarHorario(a, 'inicio', e.target.value)} />
                                </label>
                                <label className="horario-campo">a
                                  <input type="time" required aria-label={`Hora de fin de ${a.voluntario}`}
                                         value={horarioDe(a).fin} onChange={e => editarHorario(a, 'fin', e.target.value)} />
                                </label>
                                {a.tipo === 'CURSO' && (
                                  <label className="horario-campo" title="Días que colaboró con ese horario">×
                                    <input type="number" min="1" max="60" required aria-label={`Días de ${a.voluntario}`}
                                           value={horarioDe(a).dias} onChange={e => editarHorario(a, 'dias', e.target.value)}
                                           style={{ width: 56 }} /> días
                                  </label>
                                )}
                                <span className="horario-total" aria-live="polite">
                                  {(() => { const t = horasDeHorario(horarioDe(a).inicio, horarioDe(a).fin, horarioDe(a).dias); return t ? `= ${formatoHoras(t)}` : ''; })()}
                                </span>
                                <button type="submit" className="btn-primary" style={{ padding: '6px 10px' }} disabled={guardando === a.id}>
                                  {guardando === a.id ? '...' : a.estado === 'COMPLETADA' ? 'Corregir' : 'Guardar'}
                                </button>
                              </form>
                            ) : (
                              <button className="btn-secondary" onClick={() => quitar(a)}>Quitar</button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            ))}
          </section>

          <section className="panel">
            <h2>Nueva asignación</h2>
            {actividades.length === 0 ? (
              <p className="muted">No hay eventos ni cursos publicados próximos a los que asignar voluntarios.</p>
            ) : (
              <form onSubmit={asignar} className="form-stack">
                <div>
                  <label htmlFor="actividad">Actividad</label>
                  <select id="actividad" value={actividad} required
                          onChange={e => { setActividad(e.target.value); setSeleccion(new Set()); }}>
                    <option value="">Selecciona un evento o curso...</option>
                    {actividades.map(a => (
                      <option key={claveActividad(a)} value={claveActividad(a)}>
                        {a.tipo === 'CURSO' ? 'Curso' : 'Evento'} · {a.titulo} · {fechaActividad(a)} ({a.asignados} asignado/s)
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label htmlFor="tarea">Tarea (opcional)</label>
                  <input id="tarea" type="text" value={tarea} maxLength={200} onChange={e => setTarea(e.target.value)}
                         placeholder="Ej: Mesa de inscripción, logística, fotografía" />
                </div>
                <div>
                  <label htmlFor="buscar-vol">Voluntarios activos ({seleccion.size} seleccionado/s)</label>
                  <input id="buscar-vol" type="search" value={busqueda} onChange={e => setBusqueda(e.target.value)}
                         placeholder="Buscar por nombre o email" />
                  <div style={{ maxHeight: 260, overflowY: 'auto', border: '1px solid #e5e7eb', borderRadius: 4, marginTop: 6 }}>
                    {visibles.length === 0 ? (
                      <p className="muted" style={{ padding: 8 }}>No hay voluntarios activos.</p>
                    ) : visibles.map(v => {
                      const asignado = yaAsignados.has(v.id);
                      return (
                        <label key={v.id} style={{ display: 'flex', gap: 8, padding: '6px 8px', fontWeight: 400,
                                                   alignItems: 'center', opacity: asignado ? 0.55 : 1, cursor: asignado ? 'default' : 'pointer' }}>
                          <input type="checkbox" style={{ width: 'auto' }} disabled={!elegida || asignado}
                                 checked={asignado || seleccion.has(v.id)} onChange={() => alternar(v.id)} />
                          <span>
                            {v.nombreCompleto}
                            <span className="muted" style={{ fontSize: '0.85rem' }}> · {asignado ? 'ya asignado/a' : formatoHoras(v.totalHoras)}</span>
                          </span>
                        </label>
                      );
                    })}
                  </div>
                  {!elegida && <p className="muted" style={{ fontSize: '0.85rem', marginTop: 4 }}>Primero elige la actividad.</p>}
                </div>
                <button type="submit" className="btn-primary" disabled={enviando || !elegida || seleccion.size === 0}>
                  {enviando ? 'Asignando...' : `Asignar y notificar${seleccion.size ? ` (${seleccion.size})` : ''}`}
                </button>
              </form>
            )}
          </section>
        </div>
      )}
    </div>
  );
}
