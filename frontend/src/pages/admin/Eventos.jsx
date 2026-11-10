import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { encuestasApi, eventosApi } from '../../services/api';
import Estado from '../../components/Estado';
import EditorEncuesta from '../../components/EditorEncuesta';
import { fechaHora } from '../../utils/formato';

const VACIO = { titulo: '', descripcion: '', lugar: '', fechaInicio: '', fechaFin: '' };

export default function AdminEventos() {
  const [eventos, setEventos] = useState([]);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState(VACIO);
  const [msg, setMsg] = useState(null);
  const [encuestas, setEncuestas] = useState(new Map()); // eventoId -> resumen de su encuesta (FUJ-120)
  const [cerrando, setCerrando] = useState(null);
  const [procesando, setProcesando] = useState(false);

  // Gestión: incluye los eventos ya realizados y cerrados (el listado público solo muestra los abiertos)
  const cargar = () =>
    Promise.all([eventosApi.gestion(), encuestasApi.listar()])
      .then(([ev, en]) => {
        setEventos([...ev.data].sort((a, b) => new Date(a.fechaInicio) - new Date(b.fechaInicio)));
        setEncuestas(new Map(en.data.filter(x => x.tipoActividad === 'EVENTO').map(x => [x.actividadId, x])));
      })
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudieron cargar los eventos' }))
      .finally(() => setLoading(false));

  useEffect(() => { cargar(); }, []);

  const crear = async (e) => {
    e.preventDefault();
    setMsg(null);
    if (form.fechaFin && form.fechaFin < form.fechaInicio) {
      setMsg({ tipo: 'error', texto: 'La fecha de fin no puede ser anterior a la de inicio' });
      return;
    }
    try {
      // datetime-local no lleva zona horaria: se envía en ISO (UTC) para que el backend la guarde bien
      await eventosApi.crear({
        ...form,
        fechaInicio: new Date(form.fechaInicio).toISOString(),
        fechaFin: form.fechaFin ? new Date(form.fechaFin).toISOString() : null,
      });
      setForm(VACIO);
      setMsg({ tipo: 'success', texto: '✅ Evento publicado' });
      cargar();
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || '❌ Error al crear el evento' });
    }
  };

  // FUJ-120: cerrar la asistencia finaliza el evento y envía la encuesta a quienes asistieron
  const cerrarAsistencia = async (preguntas) => {
    setProcesando(true);
    setMsg(null);
    try {
      const { data } = await eventosApi.cerrarAsistencia(cerrando.id, { preguntas });
      setMsg({
        tipo: 'success',
        texto: data.asistentes === 0
          ? `Asistencia de "${cerrando.titulo}" cerrada. No hubo asistentes registrados, así que no se envió encuesta.`
          : `Asistencia de "${cerrando.titulo}" cerrada: ${data.asistentes} asistente(s) y ${data.encuestasEnviadas} encuesta(s) enviada(s).`,
      });
      setCerrando(null);
      cargar();
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo cerrar la asistencia' });
    } finally {
      setProcesando(false);
    }
  };

  const ahora = new Date();
  const proximos = eventos.filter(e => e.estado !== 'FINALIZADO' && new Date(e.fechaFin ?? e.fechaInicio) >= ahora);

  const accionAsistencia = (ev) => {
    if (ev.estado === 'FINALIZADO') {
      const encuesta = encuestas.get(ev.id);
      return encuesta
        ? <Link to={`/admin/encuestas/${encuesta.id}`} className="btn-secondary">Encuesta ({encuesta.respuestas}/{encuesta.invitados})</Link>
        : <span className="muted">Cerrada</span>;
    }
    if (ev.estado !== 'CANCELADO' && new Date(ev.fechaInicio) <= ahora) {
      return (
        <button className="btn-primary" style={{ padding: '6px 12px' }} onClick={() => setCerrando(ev)}>
          Cerrar asistencia
        </button>
      );
    }
    return <span className="muted">—</span>;
  };

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Eventos</h1>
          <p>Programa las actividades de la fundación. Al terminar, cierra la asistencia para enviar la encuesta de satisfacción.</p>
        </div>
        <div className="stat-box">
          <p>Próximos eventos</p>
          <h2>{proximos.length}</h2>
        </div>
      </div>

      {msg && <div className={`${msg.tipo}-msg`} role="status">{msg.texto}</div>}

      <div className="split">
        <section className="panel">
          <h2>Eventos</h2>
          {loading ? (
            <p>Cargando...</p>
          ) : eventos.length === 0 ? (
            <p className="muted" style={{ textAlign: 'center', padding: 24 }}>No hay eventos publicados.</p>
          ) : (
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>Evento</th><th>Fecha</th><th>Lugar</th><th>Estado</th><th>Asistencia</th></tr></thead>
                <tbody>
                  {eventos.map(ev => {
                    const pasado = new Date(ev.fechaFin ?? ev.fechaInicio) < ahora;
                    const cerrado = ev.estado === 'FINALIZADO';
                    return (
                      <tr key={ev.id} style={pasado || cerrado ? { opacity: 0.75 } : undefined}>
                        <td>
                          <strong>{ev.titulo}</strong>
                          {ev.descripcion && <div className="muted" style={{ fontSize: '0.9rem' }}>{ev.descripcion}</div>}
                        </td>
                        <td>{fechaHora(ev.fechaInicio)}</td>
                        <td>{ev.lugar || '-'}</td>
                        <td>
                          {cerrado ? <span className="badge">Cerrado</span>
                            : pasado ? <span className="badge">Realizado</span>
                            : <Estado valor={ev.estado} />}
                        </td>
                        <td>{accionAsistencia(ev)}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>

        <section className="panel">
          <h2>Nuevo evento</h2>
          <form onSubmit={crear} className="form-stack">
            <div>
              <label>Título</label>
              <input type="text" value={form.titulo} onChange={e => setForm({ ...form, titulo: e.target.value })} required maxLength={200} />
            </div>
            <div>
              <label>Descripción</label>
              <textarea rows="3" value={form.descripcion} onChange={e => setForm({ ...form, descripcion: e.target.value })} />
            </div>
            <div>
              <label>Lugar</label>
              <input type="text" value={form.lugar} onChange={e => setForm({ ...form, lugar: e.target.value })} placeholder="Ej: Plaza Murillo" />
            </div>
            <div>
              <label>Inicio</label>
              <input type="datetime-local" value={form.fechaInicio} onChange={e => setForm({ ...form, fechaInicio: e.target.value })} required />
            </div>
            <div>
              <label>Fin (opcional)</label>
              <input type="datetime-local" value={form.fechaFin} min={form.fechaInicio} onChange={e => setForm({ ...form, fechaFin: e.target.value })} />
            </div>
            <button type="submit" className="btn-primary">Publicar evento</button>
          </form>
        </section>
      </div>

      {cerrando && (
        <EditorEncuesta
          titulo={`Cerrar asistencia: ${cerrando.titulo}`}
          aviso="El evento quedará finalizado y no se aceptarán más check-ins. Quienes asistieron (por QR o registro manual) recibirán por email el enlace a esta encuesta."
          textoConfirmar="Cerrar y enviar encuesta"
          procesando={procesando}
          onConfirmar={cerrarAsistencia}
          onCerrar={() => setCerrando(null)}
        />
      )}
    </div>
  );
}
