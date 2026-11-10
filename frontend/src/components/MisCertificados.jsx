import { useEffect, useState } from 'react';
import { certificadosApi } from '../services/api';
import { fecha } from '../utils/formato';
import BotonCertificado from './BotonCertificado';

// Si se pasan `certificados` (p. ej. desde "Mi panel") no se vuelven a pedir a la API
export default function MisCertificados({ certificados: recibidos }) {
  const [propios, setPropios] = useState([]);
  const [loading, setLoading] = useState(!recibidos);
  const [error, setError] = useState('');
  const [copiado, setCopiado] = useState(null);
  const certificados = recibidos ?? propios;

  useEffect(() => {
    if (recibidos) return;
    certificadosApi.mios()
      .then(r => setPropios(r.data))
      .catch(() => setError('No se pudieron cargar tus certificados'))
      .finally(() => setLoading(false));
  }, [recibidos]);

  const copiar = async (codigo) => {
    try {
      await navigator.clipboard.writeText(codigo);
      setCopiado(codigo);
      setTimeout(() => setCopiado(null), 1500);
    } catch { /* el código sigue visible para copiarlo a mano */ }
  };

  if (loading) return <p>Cargando...</p>;
  if (error) return <div className="error-msg">{error}</div>;
  if (certificados.length === 0) {
    return (
      <p className="muted">
        Aún no tienes certificados. Se emiten automáticamente cuando el docente finaliza un curso en el que estás inscrito/a.
      </p>
    );
  }

  return (
    <div className="table-wrap">
      <table className="table">
        <thead>
          <tr><th>Curso</th><th>Emitido</th><th>Código de verificación</th><th /></tr>
        </thead>
        <tbody>
          {certificados.map(c => (
            <tr key={c.id}>
              <td style={{ fontWeight: 600 }}>{c.nombreCurso}</td>
              <td>{fecha(c.emitidoEn)}</td>
              <td>
                <code>{c.codigoUnico}</code>
                {!c.valido && <span className="badge badge-off" style={{ marginLeft: 8 }}>Anulado</span>}
              </td>
              <td>
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  <button className="btn-secondary" onClick={() => copiar(c.codigoUnico)}>
                    {copiado === c.codigoUnico ? '¡Copiado!' : 'Copiar código'}
                  </button>
                  {c.valido && <BotonCertificado id={c.id} codigo={c.codigoUnico} />}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="muted" style={{ marginTop: 12, fontSize: '0.9rem' }}>
        Comparte el código con quien necesite validar tu certificado en "Verificar Certificado".
      </p>
    </div>
  );
}
