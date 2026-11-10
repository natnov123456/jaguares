import { Link } from 'react-router-dom';
import { JaguarCabeza } from './JaguarSvg';

const ANIO = new Date().getFullYear();

export default function PiePagina() {
  return (
    <footer className="pie">
      <div className="pie-rosetas" aria-hidden="true" />
      <div className="container pie-contenido">
        <div className="pie-marca">
          <JaguarCabeza className="pie-logo" />
          <div>
            <strong>Fundación Unión de los Jaguares</strong>
            <p>Conservación, educación y desarrollo comunitario en La Paz, Bolivia.</p>
          </div>
        </div>
        <nav className="pie-enlaces" aria-label="Enlaces del pie de página">
          <div>
            <h4>Participa</h4>
            <Link to="/cursos">Cursos y talleres</Link>
            <Link to="/calendario">Calendario</Link>
            <Link to="/voluntariado">Voluntariado</Link>
          </div>
          <div>
            <h4>Fundación</h4>
            <Link to="/quienes-somos">Quiénes somos</Link>
            <Link to="/noticias">Noticias</Link>
            <Link to="/galeria">Galería</Link>
          </div>
          <div>
            <h4>Confianza</h4>
            <Link to="/transparencia">Transparencia</Link>
            <Link to="/certificados">Verificar certificado</Link>
            <Link to="/donaciones">Donar</Link>
          </div>
        </nav>
      </div>
      <div className="pie-final">© {ANIO} Fundación Unión de los Jaguares · Hecho con 🧡 en La Paz</div>
    </footer>
  );
}
