import { useState } from 'react';
import { donacionesApi } from '../services/api';

export default function Donaciones() {
  const [monto, setMonto] = useState('');
  const [medio, setMedio] = useState('PASARELA');
  const [msg, setMsg] = useState('');

  const donar = async (e) => {
    e.preventDefault();
    try {
      await donacionesApi.enLinea({ monto: parseFloat(monto), medio, referencia: null, donanteId: null });
      setMsg('¡Gracias por tu donación!');
      setMonto('');
    } catch (err) {
      setMsg(err.response?.data?.error || 'Error al procesar donación');
    }
  };

  return (
    <div className="auth-container">
      <h1>Hacer una donación</h1>
      {msg && <div className="success-msg">{msg}</div>}
      <form onSubmit={donar}>
        <label>Monto (BOB)</label>
        <input type="number" min="1" step="0.01" value={monto} onChange={e => setMonto(e.target.value)} required />
        <label>Método de pago</label>
        <select value={medio} onChange={e => setMedio(e.target.value)}>
          <option value="PASARELA">Pasarela de pago</option>
          <option value="QR">Código QR</option>
          <option value="TRANSFERENCIA">Transferencia bancaria</option>
        </select>
        <button type="submit" className="btn-primary">Donar</button>
      </form>
    </div>
  );
}