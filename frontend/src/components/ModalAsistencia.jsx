import { useEffect, useState } from 'react';
import { cursosApi } from '../services/api';
import { fecha } from '../utils/formato';

// Asistencia del participante en un curso: cada día de clase registrado y su porcentaje acumulado
export default function ModalAsistencia({ curso, onCerrar }) {
  const [datos, setDatos] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    cursosApi.miAsistencia(curso.actividadId)
      .then(r => setDatos(r.data))
      .catch(err => setError(err.response?.data?.error || 'No se pudo cargar tu asistencia'));
  }, [curso.actividadId]);

  const cumple = datos?.porcentaje != null && datos.porcentaje >= datos.asistenciaMinimaPct;

  return (
    <div className="modal-fondo" onClick={onCerrar}>
      <div className="modal" role="dialog" aria-modal="true" aria-label="Mi asistencia" onClick={e => e.stopPropagation()}>
        <button className="modal-cerrar" onClick={onCerrar} aria-label="Cerrar">×</button>
        <h2>Mi asistencia</h2>
        <p className="muted" style={{ marginBottom: 12 }}>{curso.titulo}</p>

        {error ? <div className="error-msg">{error}</div>
          : !datos ? <p>Cargando...</p>
          : datos.diasRegistrados === 0 ? (
            <p className="muted">El docente todavía no registró ninguna clase. Aquí verás cada día en cuanto lo haga.</p>
          ) : (
            <>
              <div className="stat-box" style={{ marginBottom: 12 }}>
                <p>Asistencia en el curso</p>
                <h2>
                  <span className={`badge ${cumple ? 'badge-ok' : 'badge-off'}`} style={{ fontSize: '1.2rem' }}>
                    {datos.porcentaje.toLocaleString('es-BO')} %
                  </span>
                </h2>
                <p className="muted" style={{ fontSize: '0.85rem' }}>
                  {datos.diasPresente} de {datos.diasRegistrados} clases · mínimo para el certificado: {datos.asistenciaMinimaPct} %
                </p>
              </div>
              <ul style={{ listStyle: 'none', padding: 0, display: 'flex', flexDirection: 'column', gap: 6 }}>
                {[...datos.dias].reverse().map(d => (
                  <li key={d.fecha} style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
                    <span>{fecha(d.fecha)}</span>
                    <span className={`badge ${d.presente ? 'badge-ok' : 'badge-off'}`}>{d.presente ? 'Presente' : 'Ausente'}</span>
                  </li>
                ))}
              </ul>
            </>
          )}
      </div>
    </div>
  );
}
