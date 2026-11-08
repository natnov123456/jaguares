import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { donacionesApi } from '../services/api';
import { bs, fechaHora, numeroComprobante } from '../utils/formato';
import BotonComprobante from './BotonComprobante';

export default function MisDonaciones() {
  const [donaciones, setDonaciones] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    donacionesApi.mias()
      .then(r => setDonaciones(r.data))
      .catch(() => setError('No se pudieron cargar tus donaciones'))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <p>Cargando...</p>;
  if (error) return <div className="error-msg">{error}</div>;
  if (donaciones.length === 0) {
    return <p className="muted">Aún no tienes donaciones. <Link to="/donaciones" style={{ color: '#b45309' }}>Hacer una donación →</Link></p>;
  }

  return (
    <div className="table-wrap">
      <table className="table">
        <thead><tr><th>N°</th><th>Fecha</th><th>Monto</th><th /></tr></thead>
        <tbody>
          {donaciones.map(d => (
            <tr key={d.id}>
              <td><code>{numeroComprobante(d.numeroComprobante)}</code></td>
              <td>{fechaHora(d.fecha)}</td>
              <td style={{ fontWeight: 600 }}>{bs(d.monto)}</td>
              <td>{d.estado === 'CONFIRMADA' && <BotonComprobante donacion={d} />}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
