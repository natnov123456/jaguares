import { Link, NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { ETIQUETA_ROL, SECCIONES_PANEL, esStaff, puede } from '../config/permisos';
import './Navbar.css';

export default function Navbar() {
  const { usuario, logout } = useAuth();
  const navigate = useNavigate();
  const staff = esStaff(usuario);

  return (
    <nav className="navbar">
      <div className="navbar-container">
        <Link to="/" className="navbar-logo">
          <img src="/logo-jaguar.png" alt="Jaguar" className="logo-img" onError={(e) => { e.target.style.display = 'none'; }} />
          Fundación Unión de los Jaguares
        </Link>

        <div className="navbar-links">
          {staff ? (
            // Staff: solo las secciones que su rol puede gestionar
            <>
              <NavLink to="/admin" end>Panel</NavLink>
              {SECCIONES_PANEL.filter(s => puede(usuario, s.permiso)).map(s => (
                <NavLink key={s.ruta} to={s.ruta}>{s.titulo}</NavLink>
              ))}
            </>
          ) : (
            // Visitantes, participantes y voluntarios: sitio público + su espacio personal
            <>
              <NavLink to="/" end>Inicio</NavLink>
              <NavLink to="/cursos">Cursos</NavLink>
              <NavLink to="/noticias">Noticias</NavLink>
              <NavLink to="/certificados">Verificar Certificado</NavLink>
              {puede(usuario, 'verPortalVoluntario') && <NavLink to="/voluntario">Mi Portal</NavLink>}
              {puede(usuario, 'verMiCuenta') && <NavLink to="/mi-cuenta">Mis Cursos</NavLink>}
              <Link to="/donaciones" className="btn-donate">Donar</Link>
            </>
          )}

          {usuario ? (
            <div className="user-menu">
              <span className="user-name">
                {usuario.nombreCompleto?.split(' ')[0] ?? 'usuario'}
                <span className="role-badge">{ETIQUETA_ROL[usuario.rol] ?? usuario.rol}</span>
              </span>
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
