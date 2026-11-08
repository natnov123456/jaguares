import { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { donacionesApi } from '../services/api';
import BotonComprobante from '../components/BotonComprobante';
import { numeroComprobante } from '../utils/formato';

const MEDIOS = {
  PASARELA: 'Pasarela de pago',
  QR: 'Código QR',
  TRANSFERENCIA: 'Transferencia bancaria',
};

export default function Donaciones() {
  const { usuario } = useAuth();
  const [monto, setMonto] = useState('');
  const [medio, setMedio] = useState('PASARELA');
  const [nombre, setNombre] = useState('');
  const [email, setEmail] = useState('');
  const [error, setError] = useState('');
  const [confirmacion, setConfirmacion] = useState(null);
  const [procesando, setProcesando] = useState(false);

  const donar = async (e) => {
    e.preventDefault();
    setError('');
    setProcesando(true);
    try {
      const { data } = await donacionesApi.enLinea({
        monto: parseFloat(monto), medio, referencia: null, donanteId: null,
        donanteNombre: usuario ? null : nombre, donanteEmail: usuario ? null : email,
      });
      setConfirmacion(data);
      setMonto('');
    } catch (err) {
      setError(err.response?.status === 429
        ? 'Se hicieron varias donaciones seguidas desde esta conexión. Intenta de nuevo más tarde.'
        : err.response?.data?.error || 'Error al procesar donación');
    } finally {
      setProcesando(false);
    }
  };

  if (confirmacion) {
    return (
      <div className="auth-container">
        <h1>¡Gracias por tu donación!</h1>
        <div className="success-msg">Transacción confirmada y registrada ✅</div>
        <p><strong>Comprobante N°:</strong> {numeroComprobante(confirmacion.numeroComprobante)}</p>
        <p><strong>Monto:</strong> Bs. {confirmacion.monto}</p>
        <p><strong>Método:</strong> {MEDIOS[confirmacion.medio] || confirmacion.medio}</p>
        <p><strong>Referencia:</strong> {confirmacion.referencia}</p>
        <p><strong>Fecha:</strong> {new Date(confirmacion.fecha).toLocaleString('es-BO')}</p>
        {confirmacion.comprobanteEnviadoEn
          ? <div className="info-msg" style={{ marginTop: 12 }}>Te enviamos el comprobante en PDF a tu correo.</div>
          : <div className="error-msg" style={{ marginTop: 12 }}>
              Tu donación quedó registrada, pero no pudimos enviar el comprobante por correo.
              {usuario ? ' Puedes descargarlo aquí o desde Mi cuenta.' : ' Escríbenos indicando el número de comprobante.'}
            </div>}
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 16 }}>
          {usuario && <BotonComprobante donacion={confirmacion} />}
          <button className="btn-primary" onClick={() => setConfirmacion(null)}>Hacer otra donación</button>
        </div>
      </div>
    );
  }

  return (
    <div className="auth-container">
      <h1>Hacer una donación</h1>
      {error && <div className="error-msg">{error}</div>}
      <form onSubmit={donar}>
        <label>Monto (BOB)</label>
        <input type="number" min="1" step="0.01" value={monto} onChange={e => setMonto(e.target.value)} required />
        <label>Método de pago</label>
        <select value={medio} onChange={e => setMedio(e.target.value)}>
          {Object.entries(MEDIOS).map(([valor, texto]) => <option key={valor} value={valor}>{texto}</option>)}
        </select>
        {usuario ? (
          <p className="muted">El comprobante saldrá a nombre de {usuario.nombreCompleto} y se enviará al correo de tu cuenta.</p>
        ) : (
          <>
            <label htmlFor="d-nombre">Nombre para el comprobante</label>
            <input id="d-nombre" type="text" maxLength={150} value={nombre} onChange={e => setNombre(e.target.value)} required />
            <label htmlFor="d-email">Correo para recibir el comprobante</label>
            <input id="d-email" type="email" maxLength={150} value={email} onChange={e => setEmail(e.target.value)} required />
          </>
        )}
        <button type="submit" className="btn-primary" disabled={procesando}>
          {procesando ? 'Procesando pago...' : 'Donar'}
        </button>
      </form>
    </div>
  );
}
