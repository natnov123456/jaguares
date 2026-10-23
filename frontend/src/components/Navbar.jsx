import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import './Navbar.css';

export default function Navbar() {
  const { usuario, logout } = useAuth();
  const navigate = useNavigate();

  return (
    <nav className="navbar">
      <div className="navbar-container">
        <Link to="/" className="navbar-logo">
          <img src="/logo-jaguar.png" alt="Jaguar" className="logo-img" onError={(e) => { e.target.style.display = 'none'; }} />
          Fundación Unión de los Jaguares
        </Link>
        
        <div className="navbar-links">
          {/* Enlaces públicos */}
          {(!usuario || usuario.rol === 'PARTICIPANTE' || usuario.rol === 'VISITANTE') && (
            <>
              <Link to="/">Inicio</Link>
              <Link to="/cursos">Cursos</Link>
              <Link to="/certificados">Verificar Certificado</Link>
              <Link to="/donaciones" className="btn-donate">Donar</Link>
            </>
          )}

          {/* Enlaces para ADMIN / DIRECTORA */}
          {(usuario?.rol === 'DIRECTORA' || usuario?.rol === 'DOCENTE' || usuario?.rol === 'ENCARGADO_EVENTOS') && (
            <>
              <Link to="/admin">Panel de Control</Link>
              <Link to="/admin/crear-curso">Crear Curso</Link>
              {usuario.rol === 'DIRECTORA' && <Link to="/admin/publicar-noticia">Noticias</Link>}
            </>
          )}

          {/* Enlaces para VOLUNTARIO */}
          {usuario?.rol === 'VOLUNTARIO' && (
            <>
              <Link to="/voluntario">Mi Portal</Link>
              <Link to="/voluntario/certificados">Mis Certificados</Link>
            </>
          )}

          {/* Sesión */}
          {usuario ? (
            <div className="user-menu">
              <span className="user-name">Hola, {usuario.nombreCompleto.split(' ')[0]}</span>
              <button onClick={() => { logout(); navigate('/'); }} className="btn-logout">Salir</button>
            </div>
          ) : (
            <Link to="/login" className="btn-login">Iniciar Sesión</Link>
          )}
        </div>
      </div>
    </nav>
  );
}