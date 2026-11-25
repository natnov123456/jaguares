import { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { archivoUrl } from '../services/api';

// Vista ampliada de la galería (FUJ-126): solo la foto, a pantalla completa, o el video insertado.
// Flechas y teclado para pasar de una a otra; Esc o un clic fuera de la foto la cierran.
export default function VisorGaleria({ items, indice, onCambiar, onCerrar }) {
  const item = items[indice];
  const hayVarios = items.length > 1;
  const mover = (paso) => onCambiar((indice + paso + items.length) % items.length);

  // Mientras está abierta, la página de fondo no se desplaza
  useEffect(() => {
    const antes = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = antes; };
  }, []);

  useEffect(() => {
    const tecla = (e) => {
      if (e.key === 'Escape') onCerrar();
      else if (e.key === 'ArrowRight' && hayVarios) onCambiar((indice + 1) % items.length);
      else if (e.key === 'ArrowLeft' && hayVarios) onCambiar((indice - 1 + items.length) % items.length);
    };
    window.addEventListener('keydown', tecla);
    return () => window.removeEventListener('keydown', tecla);
  }, [indice, items.length, hayVarios, onCambiar, onCerrar]);

  if (!item) return null;

  // Se dibuja directo en <body>: si la página tiene animaciones con transform, un position: fixed
  // dentro de ella quedaría encerrado en el contenido en vez de cubrir toda la ventana.
  return createPortal(
    <div className="visor" role="dialog" aria-modal="true" aria-label={item.titulo || 'Vista ampliada'} onClick={onCerrar}>
      <button className="visor-cerrar" onClick={onCerrar} aria-label="Cerrar">×</button>
      {hayVarios && (
        <button className="visor-flecha izq" onClick={e => { e.stopPropagation(); mover(-1); }} aria-label="Anterior">‹</button>
      )}
      <figure className="visor-contenido">
        {item.tipo === 'VIDEO' ? (
          <div className="visor-video" onClick={e => e.stopPropagation()}>
            <iframe src={item.videoUrl} title={item.titulo || 'Video'} allowFullScreen
                    allow="accelerometer; encrypted-media; gyroscope; picture-in-picture; fullscreen"
                    referrerPolicy="strict-origin-when-cross-origin" />
          </div>
        ) : (
          <img src={archivoUrl(item.imagenUrl)} alt={item.titulo || 'Foto de la galería'} onClick={e => e.stopPropagation()} />
        )}
        {(item.titulo || hayVarios) && (
          <figcaption onClick={e => e.stopPropagation()}>
            {item.titulo && <span>{item.titulo}</span>}
            {hayVarios && <span className="visor-contador">{indice + 1} / {items.length}</span>}
          </figcaption>
        )}
      </figure>
      {hayVarios && (
        <button className="visor-flecha der" onClick={e => { e.stopPropagation(); mover(1); }} aria-label="Siguiente">›</button>
      )}
    </div>,
    document.body
  );
}
