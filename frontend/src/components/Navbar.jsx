import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
import { JaguarCabeza } from './JaguarSvg';
import { useAuth } from '../context/AuthContext';
import { ETIQUETA_ROL, GRUPOS_PANEL, SECCIONES_PANEL, esStaff, puede } from '../config/permisos';
import './Navbar.css';

// Enlaces públicos: los más usados quedan a la vista y el resto en "Más"
const PUBLICOS = [
  { ruta: '/', titulo: 'Inicio', end: true },
  { ruta: '/quienes-somos', titulo: 'Quiénes somos' },
  { ruta: '/cursos', titulo: 'Cursos' },
  { ruta: '/calendario', titulo: 'Calendario' },
  { ruta: '/noticias', titulo: 'Noticias' },
];
const PUBLICOS_MAS = [
  { ruta: '/galeria', titulo: 'Galería' },
  { ruta: '/transparencia', titulo: 'Transparencia' },
  { ruta: '/certificados', titulo: 'Verificar certificado' },
];

// Menú desplegable. Se abre con clic (también en el celular) y se cierra al elegir, al hacer clic fuera o con Esc.
function Desplegable({ id, abierto, alternar, cerrar, boton, activo, alineado = 'izquierda', children }) {
  const ref = useRef(null);

  useEffect(() => {
    if (!abierto) return undefined;
    const fuera = (e) => { if (ref.current && !ref.current.contains(e.target)) cerrar(); };
    const esc = (e) => { if (e.key === 'Escape') cerrar(); };
    document.addEventListener('mousedown', fuera);
    document.addEventListener('keydown', esc);
    return () => {
      document.removeEventListener('mousedown', fuera);
      document.removeEventListener('keydown', esc);
    };
  }, [abierto, cerrar]);

  return (
    <div className={`nav-desplegable ${abierto ? 'abierto' : ''}`} ref={ref}>
      <button type="button" className={`nav-desplegable-boton ${activo ? 'active' : ''}`}
              aria-expanded={abierto} aria-controls={`menu-${id}`} onClick={alternar}>
        {boton}
        <svg className="nav-flecha" viewBox="0 0 12 12" aria-hidden="true"><path d="M3 4.5 6 7.5 9 4.5" /></svg>
      </button>
      <div id={`menu-${id}`} className={`nav-menu nav-menu-${alineado}`} hidden={!abierto}>
        {children}
      </div>
    </div>
  );
}

export default function Navbar() {
  const { usuario, logout } = useAuth();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const staff = esStaff(usuario);
  const [conSombra, setConSombra] = useState(false);
  const [abierto, setAbierto] = useState(null);   // id del desplegable abierto
  const [movilAbierto, setMovilAbierto] = useState(false);
  const [rutaAnterior, setRutaAnterior] = useState(pathname);

  // Al cambiar de página se cierran los menús (ajuste durante el render, sin efecto)
  if (rutaAnterior !== pathname) {
    setRutaAnterior(pathname);
    setAbierto(null);
    setMovilAbierto(false);
  }

  useEffect(() => {
    const alBajar = () => setConSombra(window.scrollY > 8);
    alBajar();
    window.addEventListener('scroll', alBajar, { passive: true });
    return () => window.removeEventListener('scroll', alBajar);
  }, []);

  const cerrar = useCallback(() => setAbierto(null), []);
  const alternar = (id) => () => setAbierto(a => (a === id ? null : id));
  const activa = (ruta) => pathname === ruta;

  // Staff: secciones de su rol agrupadas por área
  const secciones = staff ? SECCIONES_PANEL.filter(s => puede(usuario, s.permiso)) : [];
  const grupos = GRUPOS_PANEL
    .map(g => ({ ...g, items: secciones.filter(s => s.grupo === g.id) }))
    .filter(g => g.items.length > 0);

  const salir = () => { logout(); navigate('/'); };
  const nombre = usuario?.nombreCompleto ?? 'Usuario';
  const inicial = nombre.trim().charAt(0).toUpperCase() || 'U';

  return (
    <nav className={`navbar ${conSombra ? 'navbar-scroll' : ''}`} aria-label="Principal">
      <div className="navbar-container">
        <Link to={staff ? '/admin' : '/'} className="navbar-logo">
          <JaguarCabeza className="logo-img" titulo="" />
          <span>Fundación Unión de los Jaguares</span>
        </Link>

        <button type="button" className="navbar-hamburguesa" aria-expanded={movilAbierto} aria-controls="navbar-links"
                aria-label={movilAbierto ? 'Cerrar menú' : 'Abrir menú'} onClick={() => setMovilAbierto(v => !v)}>
          <span /><span /><span />
        </button>

        <div id="navbar-links" className={`navbar-links ${movilAbierto ? 'abierto' : ''}`}>
          {staff ? (
            <>
              <NavLink to="/admin" end>Panel</NavLink>
              {grupos.map(g => (g.items.length === 1 ? (
                <NavLink key={g.id} to={g.items[0].ruta} end>{g.items[0].titulo}</NavLink>
              ) : (
                <Desplegable key={g.id} id={g.id} abierto={abierto === g.id} alternar={alternar(g.id)} cerrar={cerrar}
                             boton={g.titulo} activo={g.items.some(s => activa(s.ruta))}>
                  {g.items.map(s => (
                    <NavLink key={s.ruta} to={s.ruta} end className="nav-menu-item">
                      <span className="nav-menu-icono" aria-hidden="true">{s.icono}</span>
                      {s.titulo}
                    </NavLink>
                  ))}
                </Desplegable>
              )))}
            </>
          ) : (
            <>
              {PUBLICOS.map(l => <NavLink key={l.ruta} to={l.ruta} end={l.end}>{l.titulo}</NavLink>)}
              <Desplegable id="mas" abierto={abierto === 'mas'} alternar={alternar('mas')} cerrar={cerrar}
                           boton="Más" activo={PUBLICOS_MAS.some(l => activa(l.ruta))}>
                {PUBLICOS_MAS.map(l => <NavLink key={l.ruta} to={l.ruta} className="nav-menu-item">{l.titulo}</NavLink>)}
              </Desplegable>
              <Link to="/donaciones" className="btn-donate">Donar</Link>
            </>
          )}

          {usuario ? (
            <Desplegable id="cuenta" abierto={abierto === 'cuenta'} alternar={alternar('cuenta')} cerrar={cerrar}
                         alineado="derecha"
                         boton={<><span className="nav-avatar" aria-hidden="true">{inicial}</span>
                                  <span className="nav-usuario-nombre">{nombre.split(' ')[0]}</span></>}>
              <div className="nav-menu-cabecera">
                <strong>{nombre}</strong>
                <span className="role-badge">{ETIQUETA_ROL[usuario.rol] ?? usuario.rol}</span>
              </div>
              {puede(usuario, 'verMiCuenta') && <NavLink to="/mi-cuenta" className="nav-menu-item">Mi panel</NavLink>}
              {puede(usuario, 'verPortalVoluntario') && <NavLink to="/voluntario" className="nav-menu-item">Mi portal de voluntariado</NavLink>}
              {staff && <NavLink to="/" end className="nav-menu-item">Ver sitio público</NavLink>}
              <NavLink to="/mi-contrasena" className="nav-menu-item">Cambiar mi contraseña</NavLink>
              <button type="button" className="nav-menu-item nav-menu-salir" onClick={salir}>Cerrar sesión</button>
            </Desplegable>
          ) : (
            <Link to="/login" className="btn-login">Iniciar sesión</Link>
          )}
        </div>
      </div>
    </nav>
  );
}
