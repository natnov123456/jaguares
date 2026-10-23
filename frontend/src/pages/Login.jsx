import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import './Auth.css';

export default function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    try {
      const user = await login(email, password);
      
      // Redirección dependiendo del rol
      if (['DIRECTORA', 'DOCENTE', 'ENCARGADO_EVENTOS'].includes(user.rol)) {
        navigate('/admin');
      } else if (user.rol === 'VOLUNTARIO') {
        navigate('/voluntario');
      } else {
        navigate('/'); // Participante normal
      }
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