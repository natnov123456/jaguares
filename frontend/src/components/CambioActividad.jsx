import { useState } from 'react';

// FUJ-119: diálogo para reprogramar o cancelar un evento o un curso.
// Al confirmar, el backend avisa por correo a todos los inscritos (y a la lista de espera en cursos).
//   modo: 'reprogramar' | 'cancelar'   tipo: 'EVENTO' | 'CURSO'
//   onConfirmar(datos) debe devolver una promesa; si falla, el error se muestra dentro del diálogo.

// "2026-11-12T19:30:00Z" -> "2026-11-12T15:30" (hora local del navegador, para <input type="datetime-local">)
const aLocal = (iso) => {
  if (!iso) return '';
  const d = new Date(iso);
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
};

export default function CambioActividad({ modo, tipo, actividad, onConfirmar, onCerrar }) {
  const esEvento = tipo === 'EVENTO';
  const reprogramar = modo === 'reprogramar';
  const [form, setForm] = useState(() => ({
    inicio: esEvento ? aLocal(actividad.fechaInicio) : actividad.fechaInicio,
    fin: esEvento ? aLocal(actividad.fechaFin) : actividad.fechaFin,
    lugar: actividad.lugar ?? '',
    motivo: '',
  }));
  const [error, setError] = useState(null);
  const [enviando, setEnviando] = useState(false);
  const set = (campo) => (e) => setForm({ ...form, [campo]: e.target.value });

  const confirmar = async (e) => {
    e.preventDefault();
    setError(null);
    if (reprogramar && form.fin && form.fin < form.inicio) {
      setError('La fecha de fin no puede ser anterior a la de inicio');
      return;
    }
    const motivo = form.motivo.trim() || null;
    const datos = !reprogramar ? { motivo }
      : esEvento ? {
          // datetime-local no lleva zona horaria: se envía en UTC, igual que al crear el evento
          fechaInicio: new Date(form.inicio).toISOString(),
          fechaFin: form.fin ? new Date(form.fin).toISOString() : null,
          lugar: form.lugar.trim() || null,
          motivo,
        }
      : { fechaInicio: form.inicio, fechaFin: form.fin, motivo };
    setEnviando(true);
    try {
      await onConfirmar(datos);
    } catch (err) {
      setError(err.response?.status === 403
        ? 'Solo puedes modificar las actividades que tú creaste.'
        : err.response?.data?.error || 'No se pudo guardar el cambio');
      setEnviando(false);
    }
  };

  const nombre = esEvento ? 'evento' : 'curso';
  const destinatarios = esEvento ? 'Todos los inscritos' : 'Todos los inscritos y las personas en lista de espera';

  return (
    <div className="modal-fondo" onClick={onCerrar}>
      <div className="modal" role="dialog" aria-modal="true" aria-label={`${reprogramar ? 'Reprogramar' : 'Cancelar'} ${nombre}`}
           onClick={e => e.stopPropagation()}>
        <button className="modal-cerrar" onClick={onCerrar} aria-label="Cerrar">×</button>
        <h2>{reprogramar ? 'Reprogramar' : 'Cancelar'} {nombre}</h2>
        <p style={{ fontWeight: 600, margin: '4px 0 12px' }}>{actividad.titulo}</p>

        <form onSubmit={confirmar} className="form-stack">
          {reprogramar && (
            <>
              <div>
                <label htmlFor="ca-inicio">{esEvento ? 'Nuevo inicio' : 'Nueva fecha de inicio'}</label>
                <input id="ca-inicio" type={esEvento ? 'datetime-local' : 'date'} value={form.inicio} onChange={set('inicio')} required />
              </div>
              <div>
                <label htmlFor="ca-fin">{esEvento ? 'Nuevo fin (opcional)' : 'Nueva fecha de fin'}</label>
                <input id="ca-fin" type={esEvento ? 'datetime-local' : 'date'} value={form.fin ?? ''} min={form.inicio}
                       onChange={set('fin')} required={!esEvento} />
              </div>
              {esEvento && (
                <div>
                  <label htmlFor="ca-lugar">Lugar</label>
                  <input id="ca-lugar" value={form.lugar} onChange={set('lugar')} maxLength={200} />
                </div>
              )}
            </>
          )}
          <div>
            <label htmlFor="ca-motivo">Motivo (opcional, se incluye en el aviso)</label>
            <textarea id="ca-motivo" rows="2" maxLength={300} value={form.motivo} onChange={set('motivo')}
                      placeholder={reprogramar ? 'Ej: pronóstico de lluvia' : 'Ej: no se alcanzó el mínimo de inscritos'} />
          </div>

          <div className={reprogramar ? 'info-msg' : 'error-msg'} style={{ marginBottom: 0 }}>
            {reprogramar
              ? `${destinatarios} recibirán un correo con la fecha nueva y un recordatorio 24 h antes.`
              : `${destinatarios} recibirán un correo de cancelación. Esta acción no se puede deshacer.`}
          </div>
          {error && <div className="error-msg" role="alert">{error}</div>}

          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <button type="submit" className={reprogramar ? 'btn-primary' : 'btn-danger'} disabled={enviando}>
              {enviando ? 'Guardando...' : reprogramar ? 'Reprogramar y avisar' : `Cancelar ${nombre} y avisar`}
            </button>
            <button type="button" className="btn-secondary" onClick={onCerrar} disabled={enviando}>Volver</button>
          </div>
        </form>
      </div>
    </div>
  );
}
