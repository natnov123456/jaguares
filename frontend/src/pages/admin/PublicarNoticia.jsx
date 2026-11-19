import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { contenidoApi } from '../../services/api';
import BarraFiltros from '../../components/filtros/BarraFiltros';
import BotonExportar from '../../components/BotonExportar';
import useFiltros from '../../hooks/useFiltros';
import { aplicarFiltros } from '../../utils/filtros';
import { fecha } from '../../utils/formato';

const VACIA = { titulo: '', resumen: '', contenido: '', imagenUrl: '', publicada: true };
const ESTADOS = [{ valor: 'PUBLICADA', etiqueta: 'Publicadas' }, { valor: 'BORRADOR', etiqueta: 'Borradores' }];

// FUJ-108: publicar noticias. Evaluación: listado de gestión con filtros combinados y exportación.
export default function PublicarNoticia() {
  const [form, setForm] = useState(VACIA);
  const [msg, setMsg] = useState(null);
  const [noticias, setNoticias] = useState([]);
  const [cargando, setCargando] = useState(true);
  const { valores, cambiar, limpiar } = useFiltros();

  const cargar = () =>
    contenidoApi.noticiasAdmin()
      .then(r => setNoticias(r.data))
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudieron cargar las noticias' }))
      .finally(() => setCargando(false));

  useEffect(() => { cargar(); }, []);

  const publicar = async (e) => {
    e.preventDefault();
    try {
      await contenidoApi.crearNoticia({ ...form, imagenUrl: form.imagenUrl || null });
      setMsg({ tipo: 'success', texto: form.publicada ? 'Noticia publicada ✅ Ya aparece en el sitio.' : 'Borrador guardado ✅' });
      setForm(VACIA);
      cargar();
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'Error al publicar la noticia' });
    }
  };

  const visibles = aplicarFiltros(noticias, valores, {
    texto: n => [n.titulo, n.resumen, n.contenido], estado: n => (n.publicada ? 'PUBLICADA' : 'BORRADOR'),
    fecha: n => n.publicadoEn ?? n.creadoEn,
  });

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Noticias</h1>
          <p>Publica novedades en el sitio o guárdalas como borrador. <Link to="/noticias" style={{ color: '#b45309' }}>Ver en el sitio →</Link></p>
        </div>
      </div>
      {msg && <div className={`${msg.tipo}-msg`} role="status">{msg.texto}</div>}

      <div className="split">
        <section className="panel">
          <h2>Noticias</h2>
          <BarraFiltros valores={valores} cambiar={cambiar} limpiar={limpiar} texto="Título o contenido" estados={ESTADOS}
                        fechas="Fecha" total={noticias.length} visibles={visibles.length}>
            <BotonExportar reporte="noticias" filtros={valores} />
          </BarraFiltros>
          {cargando ? <p>Cargando...</p> : visibles.length === 0 ? (
            <p className="muted" style={{ textAlign: 'center', padding: 24 }}>
              {noticias.length === 0 ? 'Todavía no hay noticias.' : 'Ninguna noticia coincide con los filtros.'}
            </p>
          ) : (
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>Título</th><th>Estado</th><th>Fecha</th></tr></thead>
                <tbody>
                  {visibles.map(n => (
                    <tr key={n.id}>
                      <td><strong>{n.titulo}</strong>{n.resumen && <div className="muted" style={{ fontSize: '0.85rem' }}>{n.resumen}</div>}</td>
                      <td><span className={`badge ${n.publicada ? 'badge-ok' : ''}`}>{n.publicada ? 'Publicada' : 'Borrador'}</span></td>
                      <td>{fecha(n.publicadoEn ?? n.creadoEn)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>

        <section className="panel">
          <h2>Nueva noticia</h2>
          <form onSubmit={publicar} className="form-stack">
            <div><label htmlFor="n-titulo">Título</label>
              <input id="n-titulo" type="text" value={form.titulo} onChange={e => setForm({ ...form, titulo: e.target.value })} required /></div>
            <div><label htmlFor="n-resumen">Resumen</label>
              <input id="n-resumen" type="text" value={form.resumen} onChange={e => setForm({ ...form, resumen: e.target.value })} /></div>
            <div><label htmlFor="n-contenido">Contenido</label>
              <textarea id="n-contenido" value={form.contenido} onChange={e => setForm({ ...form, contenido: e.target.value })} required rows="5" /></div>
            <div><label htmlFor="n-imagen">URL de la imagen (opcional)</label>
              <input id="n-imagen" type="url" value={form.imagenUrl} onChange={e => setForm({ ...form, imagenUrl: e.target.value })} placeholder="https://..." /></div>
            <label style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <input type="checkbox" checked={form.publicada} onChange={e => setForm({ ...form, publicada: e.target.checked })} />
              Publicar ahora (si no, queda como borrador)
            </label>
            <button type="submit" className="btn-primary">{form.publicada ? 'Publicar' : 'Guardar borrador'}</button>
          </form>
        </section>
      </div>
    </div>
  );
}
