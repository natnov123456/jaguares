import { useState } from 'react';
import { donacionesApi } from '../services/api';

const MEDIOS = {
  PASARELA: 'Pasarela de pago',
  QR: 'Código QR',
  TRANSFERENCIA: 'Transferencia bancaria',
};

export default function Donaciones() {
  const [monto, setMonto] = useState('');
  const [medio, setMedio] = useState('PASARELA');
  const [error, setError] = useState('');
  const [confirmacion, setConfirmacion] = useState(null);
  const [procesando, setProcesando] = useState(false);

  const donar = async (e) => {
    e.preventDefault();
    setError('');
    setProcesando(true);
    try {
      const { data } = await donacionesApi.enLinea({ monto: parseFloat(monto), medio, referencia: null, donanteId: null });
      setConfirmacion(data);
      setMonto('');
    } catch (err) {
      setError(err.response?.data?.error || 'Error al procesar donación');
    } finally {
      setProcesando(false);
    }
  };

  if (confirmacion) {
    return (
      <div className="auth-container">
        <h1>¡Gracias por tu donación!</h1>
        <div className="success-msg">Transacción confirmada y registrada ✅</div>
        <p><strong>Monto:</strong> Bs. {confirmacion.monto}</p>
        <p><strong>Método:</strong> {MEDIOS[confirmacion.medio] || confirmacion.medio}</p>
        <p><strong>Referencia:</strong> {confirmacion.referencia}</p>
        <p><strong>Fecha:</strong> {new Date(confirmacion.fecha).toLocaleString('es-BO')}</p>
        <button className="btn-primary" style={{ marginTop: 16 }} onClick={() => setConfirmacion(null)}>
          Hacer otra donación
        </button>
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
        <button type="submit" className="btn-primary" disabled={procesando}>
          {procesando ? 'Procesando pago...' : 'Donar'}
        </button>
      </form>
    </div>
  );
}
