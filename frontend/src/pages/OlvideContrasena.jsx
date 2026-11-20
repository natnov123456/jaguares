import { useState } from 'react';
import { Link } from 'react-router-dom';
import { authApi } from '../services/api';
import './Auth.css';

// "¿Olvidaste tu contraseña?": llega al correo un enlace (1 hora, un solo uso) para definir una nueva.
// La respuesta es la misma exista o no la cuenta, para no revelar qué correos están registrados.
export default function OlvideContrasena() {
  const [email, setEmail] = useState('');
  const [error, setError] = useState('');
  const [mensaje, setMensaje] = useState('');
  const [enviando, setEnviando] = useState(false);

  const enviar = async (e) => {
    e.preventDefault();
    setError('');
    setEnviando(true);
    try {
      const { data } = await authApi.olvideContrasena(email);
      setMensaje(data.mensaje);
    } catch (err) {
      setError(err.response?.status === 429
        ? 'Hiciste demasiados pedidos seguidos. Espera unos minutos y vuelve a intentarlo.'
        : err.response?.data?.error || 'No se pudo enviar el correo. Intenta de nuevo.');
    } finally {
      setEnviando(false);
    }
  };

  return (
    <div className="auth-container">
      <h1>¿Olvidaste tu contraseña?</h1>
      {mensaje ? (
        <>
          <div className="success-msg" role="status">{mensaje}</div>
          <p><Link to="/login">Volver a iniciar sesión</Link></p>
        </>
      ) : (
        <>
          <p className="muted">Escribe el correo de tu cuenta y te enviaremos un enlace para definir una contraseña nueva.</p>
          {error && <div className="error-msg" role="alert">{error}</div>}
          <form onSubmit={enviar}>
            <label htmlFor="oc-email">Email</label>
            <input id="oc-email" type="email" autoComplete="email" value={email} onChange={e => setEmail(e.target.value)} required autoFocus />
            <button type="submit" className="btn-primary" disabled={enviando}>{enviando ? 'Enviando...' : 'Enviarme el enlace'}</button>
          </form>
          <p><Link to="/login">Volver a iniciar sesión</Link></p>
        </>
      )}
    </div>
  );
}
