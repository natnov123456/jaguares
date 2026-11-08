import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { archivoUrl, galeriaApi } from '../services/api';
import VisorGaleria from '../components/VisorGaleria';
import { fecha } from '../utils/formato';
import './Galeria.css';

// FUJ-126: galería pública. /galeria muestra los álbumes; /galeria/:id las fotos y videos de uno.

export function Miniatura({ item, onClick }) {
  return (
    <button type="button" className="gal-mini" onClick={onClick} aria-label={item.titulo || (item.tipo === 'VIDEO' ? 'Video' : 'Foto')}>
      {item.miniaturaUrl
        ? <img src={archivoUrl(item.miniaturaUrl)} alt="" loading="lazy" />
        : <span className="gal-sin-mini">🎬</span>}
      {item.tipo === 'VIDEO' && <span className="gal-play" aria-hidden="true">▶</span>}
    </button>
  );
}

function ListaAlbumes() {
  const [albumes, setAlbumes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    galeriaApi.albumes()
      .then(r => setAlbumes(r.data))
      .catch(() => setError('No se pudo cargar la galería'))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>Galería</h1>
          <p>Fotos y videos de nuestras actividades, talleres y eventos.</p>
        </div>
      </div>
      {error && <div className="error-msg">{error}</div>}
      {loading ? <p>Cargando...</p> : albumes.length === 0 ? (
        <section className="panel" style={{ textAlign: 'center' }}><p className="muted">Todavía no hay álbumes publicados.</p></section>
      ) : (
        <div className="gal-albumes">
          {albumes.map(a => (
            <Link key={a.id} to={`/galeria/${a.id}`} className="card gal-album">
              <div className="gal-portada">
                {a.portadaUrl ? <img src={archivoUrl(a.portadaUrl)} alt="" loading="lazy" /> : <span>📷</span>}
              </div>
              <div className="card-body">
                <h3>{a.titulo}</h3>
                <p className="muted" style={{ fontSize: '0.85rem' }}>
                  {a.cantidadImagenes} fotos{a.cantidadVideos > 0 && ` · ${a.cantidadVideos} videos`} · {fecha(a.creadoEn)}
                </p>
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}

function DetalleAlbum({ id }) {
  const [album, setAlbum] = useState(null);
  const [error, setError] = useState(null);
  const [visor, setVisor] = useState(null);
  const cerrar = useCallback(() => setVisor(null), []);

  useEffect(() => {
    galeriaApi.album(id)
      .then(r => setAlbum(r.data))
      .catch(err => setError(err.response?.status === 404 ? 'El álbum no existe o no está publicado' : 'No se pudo cargar el álbum'));
  }, [id]);

  return (
    <div className="container page">
      <Link to="/galeria" className="gal-volver">← Todos los álbumes</Link>
      {error && <div className="error-msg" style={{ marginTop: 16 }}>{error}</div>}
      {!album && !error && <p>Cargando...</p>}
      {album && (
        <>
          <div className="page-header" style={{ marginTop: 12 }}>
            <div>
              <h1>{album.titulo}</h1>
              {album.descripcion && <p style={{ whiteSpace: 'pre-line' }}>{album.descripcion}</p>}
            </div>
          </div>
          {album.items.length === 0 ? (
            <p className="muted">Este álbum todavía no tiene fotos.</p>
          ) : (
            <div className="gal-grilla">
              {album.items.map((it, i) => <Miniatura key={it.id} item={it} onClick={() => setVisor(i)} />)}
            </div>
          )}
          {visor !== null && <VisorGaleria items={album.items} indice={visor} onCambiar={setVisor} onCerrar={cerrar} />}
        </>
      )}
    </div>
  );
}

export default function Galeria() {
  const { id } = useParams();
  return id ? <DetalleAlbum key={id} id={id} /> : <ListaAlbumes />;
}
