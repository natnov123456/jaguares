import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { puede, rutaInicio } from '../config/permisos';

export default function ProtectedRoute({ children, permiso }) {
  const { usuario, loading } = useAuth();
  const location = useLocation();
  if (loading) return <div style={{ padding: 32, textAlign: 'center' }}>Cargando...</div>;
  if (!usuario) return <Navigate to="/login" state={{ desde: location.pathname }} replace />;
  // Sin permiso: se lo devuelve a su propio inicio en lugar de a la portada pública
  if (permiso && !puede(usuario, permiso)) return <Navigate to={rutaInicio(usuario)} replace />;
  return children;
}
