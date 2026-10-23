import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { authApi } from '../services/api';
import { useAuth } from '../context/AuthContext';
import './Auth.css';

export default function Registro() {
  const [form, setForm] = useState({ nombreCompleto: '', email: '', password: '', telefono: '' });
  const [error, setError] = useState('');
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      await authApi.registro(form);
      await login(form.email, form.password);
      navigate('/mi-cuenta');
    } catch (err) {
      setError(err.response?.data?.error || 'Error al registrarse');
    }
  };

  return (
    <div className="auth-container">
      <h1>Crear cuenta</h1>
      {error && <div className="error-msg">{error}</div>}
      <form onSubmit={handleSubmit}>
        <label>Nombre completo</label>
        <input type="text" value={form.nombreCompleto} onChange={e => setForm({...form, nombreCompleto: e.target.value})} required />
        <label>Email</label>
        <input type="email" value={form.email} onChange={e => setForm({...form, email: e.target.value})} required />
        <label>Contraseña</label>
        <input type="password" value={form.password} onChange={e => setForm({...form, password: e.target.value})} required />
        <label>Teléfono</label>
        <input type="tel" value={form.telefono} onChange={e => setForm({...form, telefono: e.target.value})} />
        <button type="submit" className="btn-primary">Registrarse</button>
      </form>
      <p>¿Ya tienes cuenta? <Link to="/login">Inicia sesión</Link></p>
    </div>
  );
}