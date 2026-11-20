import { useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { campanasApi, donacionesApi } from '../services/api';
import BotonComprobante from '../components/BotonComprobante';
import ProgresoCampana from '../components/ProgresoCampana';
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
  const [campanas, setCampanas] = useState([]);
  const [campanaId, setCampanaId] = useState('');
  const [error, setError] = useState('');
  const [confirmacion, setConfirmacion] = useState(null);
  const [procesando, setProcesando] = useState(false);

  // Si la lista no carga se puede donar igual, sin elegir campaña
  const cargarCampanas = () => campanasApi.vigentes().then(r => setCampanas(r.data)).catch(() => {});
  useEffect(() => { cargarCampanas(); }, []);

  const donar = async (e) => {
    e.preventDefault();
    setError('');
    setProcesando(true);
    try {
      const { data } = await donacionesApi.enLinea({
        monto: parseFloat(monto), medio, referencia: null, donanteId: null,
        donanteNombre: usuario ? null : nombre, donanteEmail: usuario ? null : email,
        campanaId: campanaId || null,
      });
      await cargarCampanas(); // la barra ya incluye esta donación
      setConfirmacion(data);
      setMonto('');
      setCampanaId('');
    } catch (err) {
      setError(err.response?.status === 429
        ? 'Se hicieron varias donaciones seguidas desde esta conexión. Intenta de nuevo más tarde.'
        : err.response?.data?.error || 'Error al procesar donación');
    } finally {
      setProcesando(false);
    }
  };

  if (confirmacion) {
    const campanaDonada = campanas.find(c => c.id === confirmacion.campanaId);
    return (
      <div className="auth-container">
        <h1>¡Gracias por tu donación!</h1>
        <div className="success-msg">Transacción confirmada y registrada ✅</div>
        <p><strong>Comprobante N°:</strong> {numeroComprobante(confirmacion.numeroComprobante)}</p>
        <p><strong>Monto:</strong> Bs. {confirmacion.monto}</p>
        <p><strong>Método:</strong> {MEDIOS[confirmacion.medio] || confirmacion.medio}</p>
        <p><strong>Referencia:</strong> {confirmacion.referencia}</p>
        <p><strong>Fecha:</strong> {new Date(confirmacion.fecha).toLocaleString('es-BO')}</p>
        {campanaDonada && (
          <div style={{ marginTop: 12 }}>
            <strong>{campanaDonada.nombre}</strong>
            <ProgresoCampana campana={campanaDonada} />
          </div>
        )}
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
      {campanas.length > 0 && (
        <section style={{ marginBottom: 24 }}>
          <h2 style={{ fontSize: '1.1rem' }}>Campañas activas</h2>
          {campanas.map(c => (
            <article key={c.id} style={{ marginTop: 14 }}>
              <strong>{c.nombre}</strong>
              {c.descripcion && <p className="muted" style={{ fontSize: '0.9rem' }}>{c.descripcion}</p>}
              <ProgresoCampana campana={c} />
              <button type="button" className={campanaId === c.id ? 'btn-primary' : 'btn-secondary'}
                      style={{ marginTop: 6 }} onClick={() => setCampanaId(c.id)}>
                {campanaId === c.id ? 'Campaña elegida' : 'Donar a esta campaña'}
              </button>
            </article>
          ))}
        </section>
      )}
      <form onSubmit={donar}>
        {campanas.length > 0 && (
          <>
            <label htmlFor="d-campana">Destinar a</label>
            <select id="d-campana" value={campanaId} onChange={e => setCampanaId(e.target.value)}>
              <option value="">Donación general</option>
              {campanas.map(c => <option key={c.id} value={c.id}>{c.nombre}</option>)}
            </select>
          </>
        )}
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
