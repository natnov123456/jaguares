import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';

export default function Dashboard() {
  const { usuario } = useAuth();

  return (
    <div className="container" style={{ padding: '48px 24px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '32px' }}>
        <div>
          <h1 style={{ fontSize: '2.5rem', color: '#111827' }}>Panel de Administración</h1>
          <p style={{ color: '#6b7280' }}>Bienvenido/a, {usuario?.nombreCompleto} ({usuario?.rol})</p>
        </div>
      </div>

      <div className="grid">
        <Link to="/admin/crear-curso" className="card card-body" style={{ textAlign: 'center', borderTop: '4px solid #f59e0b' }}>
          <div style={{ fontSize: '3rem', marginBottom: '16px' }}>📚</div>
          <h3 style={{ fontSize: '1.2rem', marginBottom: '8px' }}>Gestionar Cursos</h3>
          <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>Crear talleres y ver inscripciones.</p>
        </Link>

        {usuario?.rol === 'DIRECTORA' && (
          <Link to="/admin/publicar-noticia" className="card card-body" style={{ textAlign: 'center', borderTop: '4px solid #f59e0b' }}>
            <div style={{ fontSize: '3rem', marginBottom: '16px' }}>📝</div>
            <h3 style={{ fontSize: '1.2rem', marginBottom: '8px' }}>Noticias</h3>
            <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>Publicar novedades en la web pública.</p>
          </Link>
        )}

        {/* NUEVO BOTÓN DE DONACIONES */}
        <Link to="/admin/donaciones" className="card card-body" style={{ textAlign: 'center', borderTop: '4px solid #f59e0b' }}>
          <div style={{ fontSize: '3rem', marginBottom: '16px' }}>💵</div>
          <h3 style={{ fontSize: '1.2rem', marginBottom: '8px' }}>Donaciones</h3>
          <p style={{ color: '#6b7280', fontSize: '0.9rem' }}>Ver historial y registrar aportes en efectivo.</p>
        </Link>
      </div>
    </div>
  );
}