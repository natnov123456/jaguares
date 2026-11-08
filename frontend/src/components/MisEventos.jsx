import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { eventosApi } from '../services/api';
import Estado from './Estado';
import { fechaHora } from '../utils/formato';

// FUJ-113: eventos en los que el participante está inscrito, con su código QR de ingreso
export default function MisEventos() {
  const [inscripciones, setInscripciones] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [qr, setQr] = useState(null); // { inscripcion, url }

  useEffect(() => {
    eventosApi.misInscripciones()
      .then(r => setInscripciones(r.data))
      .catch(() => setError('No se pudieron cargar tus eventos'))
      .finally(() => setLoading(false));
  }, []);

  // Libera la imagen en memoria al cerrar el QR
  useEffect(() => () => { if (qr?.url) URL.revokeObjectURL(qr.url); }, [qr]);

  const verQr = async (inscripcion) => {
    setError(null);
    try {
      const { data } = await eventosApi.qr(inscripcion.id);
      setQr({ inscripcion, url: URL.createObjectURL(data) });
    } catch {
      setError('No se pudo generar tu código QR');
    }
  };

  if (loading) return <p>Cargando...</p>;

  return (
    <>
      {error && <div className="error-msg">{error}</div>}
      {inscripciones.length === 0 ? (
        <p className="muted">
          No estás inscrito/a en ningún evento. <Link to="/calendario" style={{ color: '#b45309' }}>Ver calendario →</Link>
        </p>
      ) : (
        <div className="table-wrap">
          <table className="table">
            <thead><tr><th>Evento</th><th>Fecha</th><th>Estado</th><th /></tr></thead>
            <tbody>
              {inscripciones.map(i => (
                <tr key={i.id}>
                  <td><strong>{i.titulo}</strong>{i.lugar && <div className="muted" style={{ fontSize: '0.85rem' }}>📍 {i.lugar}</div>}</td>
                  <td>{fechaHora(i.fechaInicio)}</td>
                  <td><Estado valor={i.estado} /></td>
                  <td>
                    {i.estado === 'CONFIRMADA' && <button className="btn-secondary" onClick={() => verQr(i)}>Ver QR</button>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {qr && (
        <div className="modal-fondo" onClick={() => setQr(null)}>
          <div className="modal" role="dialog" aria-modal="true" aria-label="Código QR" onClick={e => e.stopPropagation()}>
            <button className="modal-cerrar" onClick={() => setQr(null)} aria-label="Cerrar">×</button>
            <h2>{qr.inscripcion.titulo}</h2>
            <p className="muted">{fechaHora(qr.inscripcion.fechaInicio)}</p>
            <img src={qr.url} alt="Código QR de ingreso" className="qr-img" />
            <p className="qr-codigo">{qr.inscripcion.codigoQr}</p>
            <p className="muted" style={{ textAlign: 'center', marginTop: 8, fontSize: '0.9rem' }}>
              Muestra este código al ingresar. Es personal: no lo compartas.
            </p>
          </div>
        </div>
      )}
    </>
  );
}
