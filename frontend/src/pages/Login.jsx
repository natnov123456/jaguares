import { useState } from 'react';
import { useNavigate, useLocation, Link, Navigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { rutaInicio } from '../config/permisos';
import './Auth.css';

export default function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const { usuario, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  if (usuario) return <Navigate to={rutaInicio(usuario)} replace />;

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    try {
      const user = await login(email, password);
      // Vuelve a la página que pidió el login (p. ej. inscribirse a un curso) o al inicio de su rol
      navigate(location.state?.desde ?? rutaInicio(user), { replace: true });
    } catch (err) {
      setError(err.response?.data?.error || 'Credenciales inválidas');
    }
  };

  return (
    <div className="auth-container">
      <h1>Iniciar sesión</h1>
      {error && <div className="error-msg">{error}</div>}
      <form onSubmit={handleSubmit}>
        <label>Email</label>
        <input type="email" value={email} onChange={e => setEmail(e.target.value)} required />
        <label>Contraseña</label>
        <input type="password" value={password} onChange={e => setPassword(e.target.value)} required />
        <button type="submit" className="btn-primary">Ingresar</button>
      </form>
      <p>¿No tienes cuenta? <Link to="/registro">Regístrate</Link></p>
    </div>
  );
}




// Notificacion de errores 
/* try {
      const user = await login(email, password);

      navigate(
        location.state?.desde ?? rutaInicio(user),
        { replace: true }
      );
    } catch (err) {
      setError(
        err.response?.data?.error ||
        'Correo electrónico o contraseña incorrectos'
      );
    } finally {
      setCargando(false);
    }
      */
