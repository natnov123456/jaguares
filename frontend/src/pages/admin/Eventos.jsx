import { useEffect, useState } from 'react';
import { eventosApi } from '../../services/api';
import Estado from '../../components/Estado';
import { fechaHora } from '../../utils/formato';

const VACIO = { titulo: '', descripcion: '', lugar: '', fechaInicio: '', fechaFin: '' };

export default function AdminEventos() {
  const [eventos, setEventos] = useState([]);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState(VACIO);
  const [msg, setMsg] = useState(null);

  const cargar = () =>
    eventosApi.listar()
      .then(r => setEventos([...r.data].sort((a, b) => new Date(a.fechaInicio) - new Date(b.fechaInicio))))
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

  const ahora = new Date();
  const proximos = eventos.filter(e => new Date(e.fechaFin ?? e.fechaInicio) >= ahora);

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Eventos</h1>
          <p>Programa las actividades de la fundación. Los eventos publicados son visibles para el público.</p>
        </div>
        <div className="stat-box">
          <p>Próximos eventos</p>
          <h2>{proximos.length}</h2>
        </div>
      </div>

      <div className="split">
        <section className="panel">
          <h2>Eventos publicados</h2>
          {loading ? (
            <p>Cargando...</p>
          ) : eventos.length === 0 ? (
            <p className="muted" style={{ textAlign: 'center', padding: 24 }}>No hay eventos publicados.</p>
          ) : (
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>Evento</th><th>Fecha</th><th>Lugar</th><th>Estado</th></tr></thead>
                <tbody>
                  {eventos.map(ev => {
                    const pasado = new Date(ev.fechaFin ?? ev.fechaInicio) < ahora;
                    return (
                      <tr key={ev.id} style={pasado ? { opacity: 0.6 } : undefined}>
                        <td>
                          <strong>{ev.titulo}</strong>
                          {ev.descripcion && <div className="muted" style={{ fontSize: '0.9rem' }}>{ev.descripcion}</div>}
                        </td>
                        <td>{fechaHora(ev.fechaInicio)}</td>
                        <td>{ev.lugar || '-'}</td>
                        <td>{pasado ? <span className="badge">Realizado</span> : <Estado valor={ev.estado} />}</td>
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
          {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}
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
    </div>
  );
}
