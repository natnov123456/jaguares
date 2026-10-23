import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';

export default function VoluntarioDashboard() {
  const { usuario } = useAuth();

  return (
    <div className="container" style={{ padding: '48px 24px' }}>
      <h1 style={{ fontSize: '2rem', marginBottom: '8px' }}>Portal del Voluntario</h1>
      <p style={{ color: '#6b7280', marginBottom: '32px' }}>Bienvenido/a, {usuario?.nombreCompleto}</p>
      
      <div className="grid">
        <Link to="/voluntario/certificados" className="card card-body" style={{ textAlign: 'center' }}>
          <div style={{ fontSize: '2rem' }}>🎓</div>
          <h3>Mis Certificados</h3>
          <p>Ver los certificados obtenidos en cursos.</p>
        </Link>
        <Link to="/voluntario/participaciones" className="card card-body" style={{ textAlign: 'center' }}>
          <div style={{ fontSize: '2rem' }}>🤝</div>
          <h3>Mis Participaciones</h3>
          <p>Historial de eventos y voluntariados.</p>
        </Link>
      </div>
    </div>
  );
}