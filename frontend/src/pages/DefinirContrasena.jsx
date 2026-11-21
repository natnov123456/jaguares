import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { authApi } from '../services/api';

// JAG-83 / FUJ-122: se define la contraseña con el enlace de un solo uso que llegó por correo
// (cuenta nueva del personal: 24 h; "¿Olvidaste tu contraseña?": 1 h)
export default function DefinirContrasena() {
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';
  const [password, setPassword] = useState('');
  const [repetir, setRepetir] = useState('');
  const [error, setError] = useState('');
  const [listo, setListo] = useState(false);
  const [enviando, setEnviando] = useState(false);

  const enviar = async (e) => {
    e.preventDefault();
    setError('');
    if (password.length < 8 || !/[A-Za-z]/.test(password) || !/\d/.test(password)) {
      setError('La contraseña debe tener al menos 8 caracteres y combinar letras y números.');
      return;
    }
    if (password !== repetir) { setError('Las contraseñas no coinciden.'); return; }
    setEnviando(true);
    try {
      await authApi.definirContrasena(token, password);
      setListo(true);
    } catch (err) {
      setError(err.response?.data?.error || 'No se pudo guardar la contraseña.');
    } finally {
      setEnviando(false);
    }
  };

  return (
    <div className="container page" style={{ maxWidth: 460 }}>
      <h1>Define tu contraseña</h1>
      {!token ? (
        <div className="error-msg">
          El enlace no es válido. Ábrelo desde el correo que te envió la fundación o pide uno nuevo en{' '}
          <Link to="/olvide-contrasena" style={{ textDecoration: 'underline' }}>¿Olvidaste tu contraseña?</Link>.
        </div>
      ) : listo ? (
        <div className="success-msg">
          ¡Listo! Tu contraseña quedó guardada. <Link to="/login" style={{ color: '#b45309' }}>Inicia sesión</Link>.
        </div>
      ) : (
        <form onSubmit={enviar} className="form-stack">
          <p className="muted">Usa al menos 8 caracteres combinando letras y números. El enlace sirve una sola vez.</p>
          {error && <div className="error-msg" role="alert">{error}</div>}
          <div><label htmlFor="dc-pass">Nueva contraseña</label>
            <input id="dc-pass" type="password" autoComplete="new-password" required value={password} onChange={e => setPassword(e.target.value)} /></div>
          <div><label htmlFor="dc-rep">Repite la contraseña</label>
            <input id="dc-rep" type="password" autoComplete="new-password" required value={repetir} onChange={e => setRepetir(e.target.value)} /></div>
          <button type="submit" className="btn-primary" disabled={enviando}>{enviando ? 'Guardando...' : 'Guardar contraseña'}</button>
        </form>
      )}
    </div>
  );
}
