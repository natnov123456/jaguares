import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { ETIQUETA_ROL, SECCIONES_PANEL, puede } from '../../config/permisos';

const RESUMEN_ROL = {
  DIRECTORA: 'Tienes acceso completo: cursos, eventos, voluntarios, finanzas y contenido público.',
  DOCENTE: 'Gestionas tus propios cursos: creas talleres, sigues las inscripciones y emites certificados al finalizar.',
  ENCARGADO_EVENTOS: 'Coordinas eventos y voluntarios, y registras las donaciones en efectivo que recibes.',
};

export default function Dashboard() {
  const { usuario } = useAuth();
  const secciones = SECCIONES_PANEL.filter(s => puede(usuario, s.permiso));

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1 style={{ fontSize: '2.5rem' }}>Panel de Control</h1>
          <p>Bienvenido/a, {usuario?.nombreCompleto} · <strong>{ETIQUETA_ROL[usuario?.rol]}</strong></p>
          <p style={{ marginTop: 4 }}>{RESUMEN_ROL[usuario?.rol]}</p>
        </div>
      </div>

      <div className="grid">
        {secciones.map(s => (
          <Link key={s.ruta} to={s.ruta} className="card card-body" style={{ textAlign: 'center', borderTop: '4px solid #f59e0b' }}>
            <div style={{ fontSize: '3rem', marginBottom: '16px' }}>{s.icono}</div>
            <h3 style={{ fontSize: '1.2rem', marginBottom: '8px' }}>{s.titulo}</h3>
            <p className="muted" style={{ fontSize: '0.9rem' }}>{s.descripcion}</p>
          </Link>
        ))}
      </div>
    </div>
  );
}
