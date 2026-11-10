import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';

// Los elementos con la clase "revelar" aparecen suavemente cuando entran en pantalla.
// Se vuelve a buscar en cada cambio de página.
export default function RevelarAlScroll() {
  const { pathname } = useLocation();

  useEffect(() => {
    const elementos = document.querySelectorAll('.revelar:not(.visible)');
    if (!('IntersectionObserver' in window)) {
      elementos.forEach(e => e.classList.add('visible'));
      return undefined;
    }
    const observador = new IntersectionObserver((entradas) => {
      for (const e of entradas) {
        if (e.isIntersecting) {
          e.target.classList.add('visible');
          observador.unobserve(e.target);
        }
      }
    }, { threshold: 0.15, rootMargin: '0px 0px -40px 0px' });
    elementos.forEach(e => observador.observe(e));
    return () => observador.disconnect();
  }, [pathname]);

  return null;
}
