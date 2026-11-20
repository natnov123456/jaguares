import { useState } from 'react';
import { authApi } from '../services/api';
import './Auth.css';

// Cada persona cambia su propia contraseña confirmando la actual. Nadie más puede cambiarla.
export default function CambiarContrasena() {
  const [actual, setActual] = useState('');
  const [nueva, setNueva] = useState('');
  const [repetir, setRepetir] = useState('');
  const [error, setError] = useState('');
  const [listo, setListo] = useState(false);
  const [enviando, setEnviando] = useState(false);

  const enviar = async (e) => {
    e.preventDefault();
    setError('');
    if (nueva.length < 8 || !/[A-Za-z]/.test(nueva) || !/\d/.test(nueva)) {
      setError('La contraseña nueva debe tener al menos 8 caracteres y combinar letras y números.');
      return;
    }
    if (nueva !== repetir) { setError('Las contraseñas nuevas no coinciden.'); return; }
    setEnviando(true);
    try {
      await authApi.cambiarMiContrasena(actual, nueva);
      setListo(true);
      setActual(''); setNueva(''); setRepetir('');
    } catch (err) {
      setError(err.response?.data?.error || 'No se pudo cambiar la contraseña.');
    } finally {
      setEnviando(false);
    }
  };

  return (
    <div className="auth-container">
      <h1>Cambiar mi contraseña</h1>
      {listo && <div className="success-msg" role="status">Listo: tu contraseña quedó cambiada. Úsala la próxima vez que inicies sesión.</div>}
      {error && <div className="error-msg" role="alert">{error}</div>}
      <form onSubmit={enviar}>
        <label htmlFor="cc-actual">Contraseña actual</label>
        <input id="cc-actual" type="password" autoComplete="current-password" value={actual} onChange={e => setActual(e.target.value)} required />
        <label htmlFor="cc-nueva">Contraseña nueva</label>
        <input id="cc-nueva" type="password" autoComplete="new-password" value={nueva} onChange={e => setNueva(e.target.value)} required />
        <label htmlFor="cc-repetir">Repite la contraseña nueva</label>
        <input id="cc-repetir" type="password" autoComplete="new-password" value={repetir} onChange={e => setRepetir(e.target.value)} required />
        <p className="muted" style={{ fontSize: '0.85rem', margin: 0 }}>Al menos 8 caracteres, combinando letras y números.</p>
        <button type="submit" className="btn-primary" disabled={enviando}>{enviando ? 'Guardando...' : 'Cambiar contraseña'}</button>
      </form>
    </div>
  );
}
