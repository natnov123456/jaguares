import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { galeriaApi } from '../../services/api';
import { Miniatura } from '../Galeria';
import VisorGaleria from '../../components/VisorGaleria';
import '../Galeria.css';

// FUJ-126: la Directora crea álbumes, sube fotos (JPG/PNG ≤ 5 MB) y agrega enlaces de video

const MAX_BYTES = 5 * 1024 * 1024;
const TIPOS = ['image/jpeg', 'image/png'];
const ALBUM_VACIO = { titulo: '', descripcion: '', publicado: false };

// Misma regla que el backend, para avisar antes de subir
function errorArchivo(f) {
  const ext = f.name.split('.').pop()?.toLowerCase();
  if (!TIPOS.includes(f.type) && !['jpg', 'jpeg', 'png'].includes(ext)) return 'Formato no permitido: solo JPG o PNG';
  if (f.size > MAX_BYTES) return `Pesa ${(f.size / 1024 / 1024).toFixed(1)} MB y el máximo es 5 MB`;
  if (f.size === 0) return 'El archivo está vacío';
  return null;
}

export default function AdminGaleria() {
  const [albumes, setAlbumes] = useState([]);
  const [selId, setSelId] = useState(null);
  const [album, setAlbum] = useState(null);
  const [nuevo, setNuevo] = useState(ALBUM_VACIO);
  const [edicion, setEdicion] = useState(ALBUM_VACIO);
  const [video, setVideo] = useState({ url: '', titulo: '' });
  const [progreso, setProgreso] = useState(null);
  const [errores, setErrores] = useState([]);
  const [msg, setMsg] = useState(null);
  const [visor, setVisor] = useState(null);
  const cerrarVisor = useCallback(() => setVisor(null), []);

  const cargarAlbumes = useCallback(() =>
    galeriaApi.albumesAdmin()
      .then(r => setAlbumes(r.data))
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudieron cargar los álbumes' })), []);

  const cargarAlbum = useCallback((id) =>
    galeriaApi.album(id)
      .then(r => {
        setAlbum(r.data);
        setEdicion({ titulo: r.data.titulo, descripcion: r.data.descripcion ?? '', publicado: r.data.publicado });
      })
      .catch(() => setMsg({ tipo: 'error', texto: 'No se pudo cargar el álbum' })), []);

  useEffect(() => { cargarAlbumes(); }, [cargarAlbumes]);
  useEffect(() => { if (selId) cargarAlbum(selId); }, [selId, cargarAlbum]);

  const seleccionar = (id) => { setMsg(null); setErrores([]); setAlbum(null); setSelId(id); };
  const error = (err, texto) => setMsg({ tipo: 'error', texto: err.response?.data?.error || texto });

  const crear = async (e) => {
    e.preventDefault();
    setMsg(null);
    try {
      const { data } = await galeriaApi.crearAlbum(nuevo);
      setNuevo(ALBUM_VACIO);
      await cargarAlbumes();
      seleccionar(data.id);
      setMsg({ tipo: 'success', texto: '✅ Álbum creado. Ahora agrega fotos o videos.' });
    } catch (err) { error(err, 'No se pudo crear el álbum'); }
  };

  const guardar = async (e) => {
    e.preventDefault();
    setMsg(null);
    try {
      await galeriaApi.actualizarAlbum(selId, edicion);
      setMsg({ tipo: 'success', texto: edicion.publicado ? '✅ Álbum guardado y visible en el sitio' : '✅ Álbum guardado (oculto)' });
      cargarAlbumes();
      cargarAlbum(selId);
    } catch (err) { error(err, 'No se pudo guardar el álbum'); }
  };

  const eliminarAlbum = async () => {
    if (!window.confirm(`¿Eliminar el álbum "${album.titulo}" con todas sus fotos? No se puede deshacer.`)) return;
    try {
      await galeriaApi.eliminarAlbum(selId);
      setSelId(null);
      setAlbum(null);
      setMsg({ tipo: 'success', texto: 'Álbum eliminado' });
      cargarAlbumes();
    } catch (err) { error(err, 'No se pudo eliminar el álbum'); }
  };

  // Se sube una imagen por petición: muestra el avance y un error no frena a las demás
  const subir = async (e) => {
    const archivos = [...e.target.files];
    e.target.value = '';
    setMsg(null);
    const fallidos = [];
    const validos = [];
    for (const f of archivos) {
      const motivo = errorArchivo(f);
      if (motivo) fallidos.push({ archivo: f.name, error: motivo }); else validos.push(f);
    }
    for (let i = 0; i < validos.length; i++) {
      setProgreso({ actual: i + 1, total: validos.length });
      try {
        const { data } = await galeriaApi.subirImagen(selId, validos[i]);
        fallidos.push(...data.errores);
      } catch (err) {
        const r = err.response?.data;
        fallidos.push(...(r?.errores ?? [{ archivo: validos[i].name, error: r?.error || 'No se pudo subir' }]));
      }
    }
    setProgreso(null);
    setErrores(fallidos);
    const ok = validos.length - fallidos.filter(f => validos.some(v => v.name === f.archivo)).length;
    if (ok > 0) setMsg({ tipo: 'success', texto: `✅ ${ok} ${ok === 1 ? 'imagen subida' : 'imágenes subidas'}` });
    cargarAlbum(selId);
    cargarAlbumes();
  };

  const agregarVideo = async (e) => {
    e.preventDefault();
    setMsg(null);
    try {
      await galeriaApi.agregarVideo(selId, { url: video.url.trim(), titulo: video.titulo.trim() || null });
      setVideo({ url: '', titulo: '' });
      setMsg({ tipo: 'success', texto: '✅ Video agregado' });
      cargarAlbum(selId);
      cargarAlbumes();
    } catch (err) { error(err, 'No se pudo agregar el video'); }
  };

  const eliminarItem = async (item) => {
    if (!window.confirm('¿Quitar este elemento del álbum?')) return;
    try {
      await galeriaApi.eliminarItem(item.id);
      cargarAlbum(selId);
      cargarAlbumes();
    } catch (err) { error(err, 'No se pudo quitar'); }
  };

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Galería</h1>
          <p>Organiza fotos y videos en álbumes. Solo los álbumes publicados se ven en el sitio.</p>
        </div>
        <Link to="/galeria" className="btn-secondary">Ver galería pública</Link>
      </div>

      {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}

      <div className="split">
        <div>
          {!album ? (
            <section className="panel">
              <p className="muted">{selId ? 'Cargando...' : 'Selecciona un álbum o crea uno nuevo.'}</p>
            </section>
          ) : (
            <>
              <section className="panel">
                <h2>Datos del álbum</h2>
                <form onSubmit={guardar} className="form-stack">
                  <div>
                    <label htmlFor="a-titulo">Título</label>
                    <input id="a-titulo" value={edicion.titulo} maxLength={200} required
                           onChange={e => setEdicion({ ...edicion, titulo: e.target.value })} />
                  </div>
                  <div>
                    <label htmlFor="a-desc">Descripción</label>
                    <textarea id="a-desc" rows="2" value={edicion.descripcion} maxLength={2000}
                              onChange={e => setEdicion({ ...edicion, descripcion: e.target.value })} />
                  </div>
                  <label className="gal-check">
                    <input type="checkbox" checked={edicion.publicado}
                           onChange={e => setEdicion({ ...edicion, publicado: e.target.checked })} />
                    Publicado (visible en el sitio)
                  </label>
                  <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                    <button type="submit" className="btn-primary">Guardar</button>
                    <button type="button" className="btn-danger" onClick={eliminarAlbum}>Eliminar álbum</button>
                  </div>
                </form>
              </section>

              <section className="panel">
                <h2>Fotos y videos ({album.items.length})</h2>
                <div className="form-stack">
                  <div>
                    <label htmlFor="a-fotos">Subir fotos (JPG o PNG, máximo 5 MB cada una)</label>
                    <input id="a-fotos" type="file" accept="image/jpeg,image/png" multiple onChange={subir} disabled={!!progreso} />
                  </div>
                  {progreso && <div className="info-msg">Subiendo {progreso.actual} de {progreso.total}...</div>}
                  {errores.length > 0 && (
                    <div className="error-msg">
                      No se pudieron subir {errores.length} archivo(s):
                      <ul className="gal-errores">{errores.map((er, i) => <li key={i}><strong>{er.archivo}</strong>: {er.error}</li>)}</ul>
                    </div>
                  )}
                </div>

                <form onSubmit={agregarVideo} className="form-stack" style={{ marginTop: 16 }}>
                  <div>
                    <label htmlFor="a-video">Enlace de video (YouTube o Vimeo)</label>
                    <input id="a-video" type="url" placeholder="https://www.youtube.com/watch?v=..." value={video.url} required
                           onChange={e => setVideo({ ...video, url: e.target.value })} />
                  </div>
                  <div>
                    <input placeholder="Título del video (opcional)" value={video.titulo} maxLength={200}
                           onChange={e => setVideo({ ...video, titulo: e.target.value })} aria-label="Título del video" />
                  </div>
                  <div><button type="submit" className="btn-secondary">+ Agregar video</button></div>
                </form>

                {album.items.length > 0 && (
                  <div className="gal-grilla" style={{ marginTop: 16 }}>
                    {album.items.map((it, i) => (
                      <div key={it.id} className="gal-admin-item">
                        <Miniatura item={it} onClick={() => setVisor(i)} />
                        <button className="gal-borrar" onClick={() => eliminarItem(it)} aria-label="Quitar">✕</button>
                      </div>
                    ))}
                  </div>
                )}
              </section>
            </>
          )}
        </div>

        <div>
          <section className="panel">
            <h2>Álbumes</h2>
            {albumes.length === 0 ? <p className="muted">Todavía no hay álbumes.</p> : (
              <ul className="gal-lista-albumes">
                {albumes.map(a => (
                  <li key={a.id}>
                    <button className={a.id === selId ? 'activo' : ''} onClick={() => seleccionar(a.id)}>
                      <span><strong>{a.titulo}</strong><br /><small className="muted">{a.cantidadImagenes} fotos · {a.cantidadVideos} videos</small></span>
                      <span className={`badge ${a.publicado ? 'badge-ok' : ''}`}>{a.publicado ? 'Publicado' : 'Oculto'}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="panel">
            <h2>Nuevo álbum</h2>
            <form onSubmit={crear} className="form-stack">
              <input placeholder="Título del álbum" value={nuevo.titulo} maxLength={200} required
                     onChange={e => setNuevo({ ...nuevo, titulo: e.target.value })} aria-label="Título del álbum" />
              <textarea rows="2" placeholder="Descripción (opcional)" value={nuevo.descripcion} maxLength={2000}
                        onChange={e => setNuevo({ ...nuevo, descripcion: e.target.value })} aria-label="Descripción" />
              <button type="submit" className="btn-primary">Crear álbum</button>
            </form>
          </section>
        </div>
      </div>

      {visor !== null && album && <VisorGaleria items={album.items} indice={visor} onCambiar={setVisor} onCerrar={cerrarVisor} />}
    </div>
  );
}
