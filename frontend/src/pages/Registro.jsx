import { useState } from 'react';
import { useNavigate, useLocation, Link } from 'react-router-dom';
import { authApi } from '../services/api';
import { useAuth } from '../context/AuthContext';
import CasillasLegales, { SIN_ACEPTAR } from '../components/CasillasLegales';
import './Auth.css';

export default function Registro() {
  const [form, setForm] = useState({ nombreCompleto: '', email: '', password: '', telefono: '' });
  const [error, setError] = useState('');
  const [legal, setLegal] = useState(SIN_ACEPTAR);
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      await authApi.registro({ ...form, ...legal });
      await login(form.email, form.password);
      // RNF-03: si venía de "Inscribirme", vuelve al curso para confirmar con un clic
      navigate(location.state?.desde ?? '/mi-cuenta', { replace: true });
    } catch (err) {
      setError(err.response?.data?.error || 'Error al registrarse');
    }
  };

  return (
    <div className="auth-container">
      <h1>Crear cuenta</h1>
      {error && <div className="error-msg">{error}</div>}
      <form onSubmit={handleSubmit}>
        <label htmlFor="r-nombre">Nombre completo</label>
        <input id="r-nombre" type="text" autoComplete="name" value={form.nombreCompleto} onChange={e => setForm({...form, nombreCompleto: e.target.value})} required autoFocus />
        <label htmlFor="r-email">Email</label>
        <input id="r-email" type="email" autoComplete="email" value={form.email} onChange={e => setForm({...form, email: e.target.value})} required />
        <label htmlFor="r-pass">Contraseña</label>
        <input id="r-pass" type="password" autoComplete="new-password" minLength={6} value={form.password} onChange={e => setForm({...form, password: e.target.value})} required />
        <label htmlFor="r-tel">Teléfono (opcional)</label>
        <input id="r-tel" type="tel" autoComplete="tel" value={form.telefono} onChange={e => setForm({...form, telefono: e.target.value})} />
        <CasillasLegales valores={legal} cambiar={setLegal} prefijo="r" />
        <button type="submit" className="btn-primary">Registrarse</button>
      </form>
      <p>¿Ya tienes cuenta? <Link to="/login" state={location.state}>Inicia sesión</Link></p>
    </div>
  );
}