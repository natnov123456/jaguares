import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { campanasApi } from '../../services/api';
import ProgresoCampana from '../../components/ProgresoCampana';

// FUJ-117: la Directora crea campañas con meta y fecha límite; al crearlas quedan publicadas en el sitio

const VACIA = { nombre: '', descripcion: '', meta: '', fechaLimite: '', activa: true };

const estadoDe = (c) => c.vigente
  ? { clase: 'badge-ok', texto: 'Publicada' }
  : c.activa ? { clase: 'badge-off', texto: 'Vencida' } : { clase: '', texto: 'Oculta' };

export default function AdminCampanas() {
  const [campanas, setCampanas] = useState([]);
  const [loading, setLoading] = useState(true);
  const [editando, setEditando] = useState(null);
  const [form, setForm] = useState(VACIA);
  const [msg, setMsg] = useState(null);

  const cargar = () =>
    campanasApi.todas()
      .then(r => setCampanas(r.data))
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudieron cargar las campañas' }))
      .finally(() => setLoading(false));

  useEffect(() => { cargar(); }, []);

  const campo = (k) => ({ value: form[k], onChange: e => setForm({ ...form, [k]: e.target.value }) });

  const editar = (c) => {
    setMsg(null);
    setEditando(c.id);
    setForm({ nombre: c.nombre, descripcion: c.descripcion ?? '', meta: c.meta, fechaLimite: c.fechaLimite, activa: c.activa });
  };

  const limpiar = () => { setEditando(null); setForm(VACIA); };

  const guardar = async (e) => {
    e.preventDefault();
    setMsg(null);
    const datos = { ...form, meta: parseFloat(form.meta), descripcion: form.descripcion.trim() || null };
    try {
      if (editando) await campanasApi.actualizar(editando, datos);
      else await campanasApi.crear(datos);
      setMsg({ tipo: 'success', texto: editando ? '✅ Campaña guardada' : '✅ Campaña publicada en el sitio' });
      limpiar();
      cargar();
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo guardar la campaña' });
    }
  };

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Campañas de donación</h1>
          <p>Las campañas publicadas aparecen en la página de donaciones con su barra de avance.</p>
        </div>
        <Link to="/donaciones" className="btn-secondary">Ver en el sitio</Link>
      </div>

      {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}

      <div className="split">
        <section className="panel">
          <h2>Campañas</h2>
          {loading ? <p>Cargando...</p> : campanas.length === 0 ? (
            <p className="muted">Todavía no hay campañas.</p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
              {campanas.map(c => {
                const estado = estadoDe(c);
                return (
                  <article key={c.id}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                      <strong>{c.nombre}</strong>
                      <span style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                        <span className={`badge ${estado.clase}`}>{estado.texto}</span>
                        <button className="btn-secondary" onClick={() => editar(c)}>Editar</button>
                      </span>
                    </div>
                    <ProgresoCampana campana={c} />
                    <small className="muted">{c.cantidadDonaciones} donación(es)</small>
                  </article>
                );
              })}
            </div>
          )}
        </section>

        <section className="panel">
          <h2>{editando ? 'Editar campaña' : 'Nueva campaña'}</h2>
          <form onSubmit={guardar} className="form-stack">
            <div>
              <label htmlFor="c-nombre">Nombre</label>
              <input id="c-nombre" maxLength={150} required {...campo('nombre')} />
            </div>
            <div>
              <label htmlFor="c-desc">Descripción (opcional)</label>
              <textarea id="c-desc" rows="3" maxLength={2000} {...campo('descripcion')} />
            </div>
            <div>
              <label htmlFor="c-meta">Meta (Bs.)</label>
              <input id="c-meta" type="number" min="1" step="0.01" required {...campo('meta')} />
            </div>
            <div>
              <label htmlFor="c-fecha">Fecha límite</label>
              <input id="c-fecha" type="date" required {...campo('fechaLimite')} />
            </div>
            {editando && (
              <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <input type="checkbox" style={{ width: 'auto' }} checked={form.activa}
                       onChange={e => setForm({ ...form, activa: e.target.checked })} />
                Publicada en el sitio
              </label>
            )}
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <button type="submit" className="btn-primary">{editando ? 'Guardar' : 'Publicar campaña'}</button>
              {editando && <button type="button" className="btn-secondary" onClick={limpiar}>Cancelar</button>}
            </div>
          </form>
        </section>
      </div>
    </div>
  );
}
