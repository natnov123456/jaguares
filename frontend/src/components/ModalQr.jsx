import { useEffect, useState } from 'react';
import { eventosApi } from '../services/api';
import { fechaHora } from '../utils/formato';

// FUJ-113: código QR de ingreso a un evento. Se pide con la sesión del participante (solo el dueño lo ve).
// inscripcion: { inscripcionId, titulo, inicio }
export default function ModalQr({ inscripcion, onCerrar }) {
  const [url, setUrl] = useState(null);
  const [error, setError] = useState(false);
  const [codigo, setCodigo] = useState(null);

  useEffect(() => {
    let creada = null;
    let activo = true;
    eventosApi.qr(inscripcion.inscripcionId)
      .then(({ data }) => {
        creada = URL.createObjectURL(data);
        if (activo) setUrl(creada);
      })
      .catch(() => { if (activo) setError(true); });
    // El código en texto sirve de respaldo si el lector no puede escanear la imagen
    eventosApi.misInscripciones()
      .then(({ data }) => { if (activo) setCodigo(data.find(i => i.id === inscripcion.inscripcionId)?.codigoQr ?? null); })
      .catch(() => { /* el QR sigue sirviendo sin el texto */ });
    // Libera la imagen en memoria al cerrar
    return () => { activo = false; if (creada) URL.revokeObjectURL(creada); };
  }, [inscripcion.inscripcionId]);

  return (
    <div className="modal-fondo" onClick={onCerrar}>
      <div className="modal" role="dialog" aria-modal="true" aria-label="Código QR" onClick={e => e.stopPropagation()}>
        <button className="modal-cerrar" onClick={onCerrar} aria-label="Cerrar">×</button>
        <h2>{inscripcion.titulo}</h2>
        <p className="muted">{fechaHora(inscripcion.inicio)}</p>
        {error ? (
          <div className="error-msg" style={{ marginTop: 12 }}>No se pudo generar tu código QR</div>
        ) : url ? (
          <img src={url} alt="Código QR de ingreso" className="qr-img" />
        ) : (
          <p style={{ textAlign: 'center', padding: 24 }}>Generando...</p>
        )}
        {codigo && <p className="qr-codigo">{codigo}</p>}
        <p className="muted" style={{ textAlign: 'center', marginTop: 8, fontSize: '0.9rem' }}>
          Muestra este código al ingresar. Es personal: no lo compartas.
        </p>
      </div>
    </div>
  );
}
