import { useEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';
import JaguarSvg from './JaguarSvg';
import './JaguarScroll.css';

// Jaguar que acompaña el scroll: baja por una liana al costado de la pantalla a medida que
// la página baja, camina mientras hay movimiento y se da la vuelta cuando se sube.
// La posición se actualiza directo en el DOM (variable CSS) para no re-renderizar en cada scroll.
export default function JaguarScroll() {
  const ref = useRef(null);
  const { pathname } = useLocation();

  useEffect(() => {
    const el = ref.current;
    if (!el) return undefined;
    let ultimoY = window.scrollY;
    let ancla = ultimoY;
    let cuadro = 0;
    let quieto = 0;

    const actualizar = () => {
      cuadro = 0;
      const max = document.documentElement.scrollHeight - window.innerHeight;
      const y = window.scrollY;
      // La liana empieza justo debajo del navbar (su alto cambia según el ancho de pantalla)
      const nav = document.querySelector('.navbar');
      el.style.top = `${(nav?.offsetHeight ?? 80) + 12}px`;
      const progreso = max > 0 ? Math.min(1, Math.max(0, y / max)) : 0;
      el.style.setProperty('--progreso', progreso.toFixed(4));
      el.classList.toggle('sin-recorrido', max < 120);
      // Margen de 8px: los reajustes de 1px del navegador (p. ej. cuando el navbar se achica)
      // no deben dar vuelta al jaguar
      if (Math.abs(y - ancla) > 8) {
        el.classList.toggle('subiendo', y < ancla);
        ancla = y;
      }
      if (Math.abs(y - ultimoY) > 1) {
        el.classList.add('caminando');
        clearTimeout(quieto);
        quieto = setTimeout(() => el.classList.remove('caminando'), 180);
      }
      el.classList.toggle('llego', progreso > 0.985);
      ultimoY = y;
    };
    const pedir = () => { if (!cuadro) cuadro = requestAnimationFrame(actualizar); };

    // La altura de la página cambia cuando terminan de cargar los datos de cada sección
    const observador = new ResizeObserver(pedir);
    observador.observe(document.body);
    window.addEventListener('scroll', pedir, { passive: true });
    window.addEventListener('resize', pedir);
    actualizar();
    el.classList.remove('caminando');

    return () => {
      observador.disconnect();
      window.removeEventListener('scroll', pedir);
      window.removeEventListener('resize', pedir);
      cancelAnimationFrame(cuadro);
      clearTimeout(quieto);
    };
  }, [pathname]);

  return (
    <div ref={ref} className="jaguar-scroll" aria-hidden="true">
      <div className="jaguar-scroll-liana" />
      <div className="jaguar-scroll-recorrido" />
      <button type="button" className="jaguar-scroll-figura" tabIndex={-1}
              title="Volver arriba" onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}>
        <JaguarSvg className="jaguar-scroll-svg" />
      </button>
    </div>
  );
}
