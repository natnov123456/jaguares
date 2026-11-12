import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { cursosApi } from '../../services/api';
import Estado from '../../components/Estado';
import CambioActividad from '../../components/CambioActividad';
import { fecha } from '../../utils/formato';

// Gestión de un curso por su docente (o la directora): asistencia de cada clase y datos del curso.
// Al guardar la asistencia, cada participante recibe un aviso con el día y su porcentaje.

// Fecha local en formato "AAAA-MM-DD" (toISOString daría el día UTC, que en Bolivia puede ser mañana)
function hoyLocal() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

const ABIERTOS = ['PUBLICADO', 'EN_CURSO'];

function Porcentaje({ valor, minimo }) {
  if (valor == null) return <span className="muted">—</span>;
  return <span className={`badge ${valor >= minimo ? 'badge-ok' : 'badge-off'}`}>{valor.toLocaleString('es-BO')} %</span>;
}

export default function CursoDetalle() {
  const { id } = useParams();
  const [curso, setCurso] = useState(null);
  const [error, setError] = useState(null);
  const [pestana, setPestana] = useState('asistencia');

  const cargarCurso = useCallback(() =>
    cursosApi.detalle(id)
      .then(r => { setCurso(r.data); return r.data; })
      .catch(err => setError(err.response?.status === 403
        ? 'Este curso no está asignado a ti.'
        : err.response?.data?.error || 'No se pudo cargar el curso')), [id]);

  useEffect(() => { cargarCurso(); }, [cargarCurso]);

  if (error) return <div className="container page"><div className="error-msg">{error}</div><Link to="/admin/cursos">← Volver a los cursos</Link></div>;
  if (!curso) return <div className="container page"><p>Cargando...</p></div>;

  const abierto = ABIERTOS.includes(curso.estado);

  return (
    <div className="container page">
      <p><Link to="/admin/cursos" style={{ color: '#b45309' }}>← Volver a los cursos</Link></p>
      <div className="page-header">
        <div>
          <h1>{curso.titulo}</h1>
          <p>
            {fecha(curso.fechaInicio)} – {fecha(curso.fechaFin)} · Docente: <strong>{curso.docenteNombre ?? 'sin asignar'}</strong> · <Estado valor={curso.estado} />
          </p>
        </div>
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
          <div className="stat-box"><p>Inscritos</p><h2>{curso.inscritos} / {curso.cupoMaximo}</h2></div>
          <div className="stat-box"><p>Clases registradas</p><h2>{curso.diasConAsistencia.length}</h2></div>
          <div className="stat-box"><p>Asistencia mínima</p><h2>{curso.asistenciaMinimaPct} %</h2></div>
        </div>
      </div>

      <div className="pestanas" role="tablist" aria-label="Secciones del curso">
        {[['asistencia', 'Asistencia'], ['datos', 'Datos del curso']].map(([clave, titulo]) => (
          <button key={clave} type="button" role="tab" aria-selected={pestana === clave}
                  className={`pestana ${pestana === clave ? 'activa' : ''}`} onClick={() => setPestana(clave)}>
            {titulo}
          </button>
        ))}
      </div>

      <section className="panel" role="tabpanel">
        {pestana === 'asistencia'
          ? <Asistencia curso={curso} abierto={abierto} onGuardado={cargarCurso} />
          : <DatosCurso key={`${curso.fechaInicio}-${curso.fechaFin}`} curso={curso} abierto={abierto}
                        onGuardado={setCurso} onReprogramado={cargarCurso} />}
      </section>
    </div>
  );
}

function Asistencia({ curso, abierto, onGuardado }) {
  const hoy = hoyLocal();
  const maximo = curso.fechaFin < hoy ? curso.fechaFin : hoy;
  const empezo = curso.fechaInicio <= hoy;
  const ultimoDia = curso.diasConAsistencia.at(-1);

  const [dia, setDia] = useState(empezo ? (abierto ? maximo : ultimoDia ?? maximo) : '');
  const [datos, setDatos] = useState(null);
  const [marcas, setMarcas] = useState({}); // participanteId -> presente
  const [cargando, setCargando] = useState(false);
  const [guardando, setGuardando] = useState(false);
  const [msg, setMsg] = useState(null);

  useEffect(() => {
    if (!dia) return;
    setCargando(true);
    cursosApi.asistencia(curso.id, dia)
      .then(r => {
        setDatos(r.data);
        // Quien aún no tiene marca ese día aparece como presente: el docente solo toca a los ausentes
        setMarcas(Object.fromEntries(r.data.participantes.map(p => [p.participanteId, p.presente ?? true])));
      })
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudo cargar la asistencia de ese día' }))
      .finally(() => setCargando(false));
  }, [curso.id, dia]);

  const guardar = async () => {
    setGuardando(true);
    setMsg(null);
    try {
      const registros = Object.entries(marcas).map(([participanteId, presente]) => ({ participanteId, presente }));
      const { data } = await cursosApi.registrarAsistencia(curso.id, dia, registros);
      setDatos(data);
      const presentes = registros.filter(r => r.presente).length;
      setMsg({
        tipo: 'success',
        texto: `Asistencia del ${fecha(dia)} guardada: ${presentes} presente(s) y ${registros.length - presentes} ausente(s). ` +
               'Cada participante recibió un aviso con el día y su porcentaje de asistencia.',
      });
      onGuardado();
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo guardar la asistencia' });
    } finally {
      setGuardando(false);
    }
  };

  if (!empezo)
    return <p className="muted">El curso empieza el {fecha(curso.fechaInicio)}. Desde ese día podrás registrar la asistencia.</p>;

  const yaRegistrado = curso.diasConAsistencia.includes(dia);
  const todos = (valor) => setMarcas(m => Object.fromEntries(Object.keys(m).map(k => [k, valor])));

  return (
    <>
      {msg && <div className={`${msg.tipo}-msg`} role="status">{msg.texto}</div>}

      <div style={{ display: 'flex', gap: 16, alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: 12 }}>
        <div className="form-stack" style={{ minWidth: 200 }}>
          <label htmlFor="a-dia">Día de clase</label>
          <input id="a-dia" type="date" value={dia} min={curso.fechaInicio} max={maximo}
                 onChange={e => { setDia(e.target.value); setMsg(null); }} />
        </div>
        {dia && (
          <span className={`badge ${yaRegistrado ? 'badge-ok' : 'badge-warn'}`}>
            {yaRegistrado ? 'Asistencia ya registrada (puedes corregirla)' : 'Sin registrar'}
          </span>
        )}
      </div>

      {curso.diasConAsistencia.length > 0 && (
        <p className="muted" style={{ marginBottom: 12, display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center' }}>
          Clases registradas:
          {curso.diasConAsistencia.map(f => (
            <button key={f} type="button" className="btn-secondary" style={{ padding: '2px 8px', fontSize: '0.85rem' }}
                    aria-pressed={f === dia} onClick={() => setDia(f)}>
              {fecha(f)}
            </button>
          ))}
        </p>
      )}

      {cargando || !datos ? (
        <p>Cargando...</p>
      ) : datos.participantes.length === 0 ? (
        <p className="muted">Este curso todavía no tiene inscritos.</p>
      ) : (
        <>
          {abierto && (
            <div style={{ display: 'flex', gap: 8, marginBottom: 8, flexWrap: 'wrap' }}>
              <button type="button" className="btn-secondary" onClick={() => todos(true)}>Todos presentes</button>
              <button type="button" className="btn-secondary" onClick={() => todos(false)}>Todos ausentes</button>
            </div>
          )}
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr><th>Participante</th><th>{fecha(dia)}</th><th>Asistencia acumulada</th></tr>
              </thead>
              <tbody>
                {datos.participantes.map(p => {
                  const presente = marcas[p.participanteId];
                  return (
                    <tr key={p.participanteId}>
                      <td>
                        <strong>{p.nombre}</strong>
                        <div className="muted" style={{ fontSize: '0.85rem' }}>{p.email}</div>
                      </td>
                      <td>
                        {abierto ? (
                          <div style={{ display: 'flex', gap: 6 }}>
                            <button type="button" aria-pressed={presente}
                                    className={presente ? 'btn-primary' : 'btn-secondary'} style={{ padding: '4px 10px' }}
                                    onClick={() => setMarcas(m => ({ ...m, [p.participanteId]: true }))}>
                              Presente
                            </button>
                            <button type="button" aria-pressed={!presente}
                                    className={!presente ? 'btn-primary' : 'btn-secondary'} style={{ padding: '4px 10px' }}
                                    onClick={() => setMarcas(m => ({ ...m, [p.participanteId]: false }))}>
                              Ausente
                            </button>
                          </div>
                        ) : p.presente == null ? <span className="muted">—</span>
                          : <span className={`badge ${p.presente ? 'badge-ok' : 'badge-off'}`}>{p.presente ? 'Presente' : 'Ausente'}</span>}
                      </td>
                      <td>
                        <Porcentaje valor={p.porcentaje} minimo={datos.asistenciaMinimaPct} />
                        {p.diasRegistrados > 0 && (
                          <span className="muted" style={{ fontSize: '0.85rem' }}> {p.diasPresente} de {p.diasRegistrados} clases</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          {abierto ? (
            <button type="button" className="btn-primary" style={{ marginTop: 12 }} disabled={guardando} onClick={guardar}>
              {guardando ? 'Guardando...' : yaRegistrado ? 'Guardar corrección' : 'Guardar asistencia'}
            </button>
          ) : (
            <p className="muted" style={{ marginTop: 12 }}>El curso está cerrado: la asistencia ya no se puede modificar.</p>
          )}
        </>
      )}
    </>
  );
}

function DatosCurso({ curso, abierto, onGuardado, onReprogramado }) {
  const [form, setForm] = useState({
    titulo: curso.titulo, descripcion: curso.descripcion ?? '',
    cupoMaximo: curso.cupoMaximo, asistenciaMinimaPct: curso.asistenciaMinimaPct,
  });
  const [guardando, setGuardando] = useState(false);
  const [msg, setMsg] = useState(null);
  const [reprogramando, setReprogramando] = useState(false);

  // FUJ-119: cambiar las fechas avisa a inscritos y lista de espera, y vuelve a programar el recordatorio
  const reprogramar = async (datos) => {
    const { data } = await cursosApi.reprogramar(curso.id, datos);
    setReprogramando(false);
    setMsg({ tipo: 'success', texto: `Curso reprogramado. Se avisó por correo a ${data.notificados} persona(s).` });
    onReprogramado();
  };

  const guardar = async (e) => {
    e.preventDefault();
    setGuardando(true);
    setMsg(null);
    try {
      const { data } = await cursosApi.editar(curso.id, {
        ...form, cupoMaximo: +form.cupoMaximo, asistenciaMinimaPct: +form.asistenciaMinimaPct,
      });
      onGuardado(data);
      setMsg({ tipo: 'success', texto: 'Datos del curso actualizados ✅' });
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudieron guardar los cambios' });
    } finally {
      setGuardando(false);
    }
  };

  const campo = (clave) => ({ value: form[clave], onChange: e => setForm({ ...form, [clave]: e.target.value }), disabled: !abierto });

  return (
    <>
      {msg && <div className={`${msg.tipo}-msg`} role="status">{msg.texto}</div>}
      {!abierto && <p className="muted" style={{ marginBottom: 12 }}>El curso está cerrado: sus datos ya no se pueden modificar.</p>}
      <form onSubmit={guardar} className="form-stack" style={{ maxWidth: 560 }}>
        <div><label htmlFor="d-titulo">Título</label><input id="d-titulo" type="text" required maxLength={200} {...campo('titulo')} /></div>
        <div><label htmlFor="d-desc">Descripción</label><textarea id="d-desc" rows="4" {...campo('descripcion')} /></div>
        <div>
          <label>Fechas</label>
          <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
            <strong>{fecha(curso.fechaInicio)} – {fecha(curso.fechaFin)}</strong>
            {abierto && (
              <button type="button" className="btn-secondary" style={{ padding: '4px 10px' }} onClick={() => setReprogramando(true)}>
                Reprogramar
              </button>
            )}
          </div>
          <p className="muted" style={{ fontSize: '0.85rem' }}>
            Al reprogramar se avisa por correo a los inscritos y a la lista de espera. Las clases con asistencia ya registrada deben quedar dentro del nuevo rango.
          </p>
        </div>
        <div>
          <label htmlFor="d-cupo">Cupo máximo</label>
          <input id="d-cupo" type="number" required min={Math.max(1, curso.inscritos)} {...campo('cupoMaximo')} />
          <p className="muted" style={{ fontSize: '0.85rem' }}>
            Hay {curso.inscritos} inscrito(s). Si amplías el cupo, los nuevos lugares pasan primero a la lista de espera.
          </p>
        </div>
        <div>
          <label htmlFor="d-min">Asistencia mínima para certificar (%)</label>
          <input id="d-min" type="number" required min="0" max="100" {...campo('asistenciaMinimaPct')} />
        </div>
        {abierto && (
          <button type="submit" className="btn-primary" disabled={guardando}>{guardando ? 'Guardando...' : 'Guardar cambios'}</button>
        )}
      </form>
      {reprogramando && (
        <CambioActividad modo="reprogramar" tipo="CURSO" actividad={curso}
                         onConfirmar={reprogramar} onCerrar={() => setReprogramando(false)} />
      )}
    </>
  );
}
