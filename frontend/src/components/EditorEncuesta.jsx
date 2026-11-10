import { useEffect, useState } from 'react';
import { encuestasApi } from '../services/api';

// FUJ-120: confirmación de cierre de una actividad con la encuesta que recibirán los asistentes.
// Parte de las preguntas por defecto; se pueden editar (3 a 5, al menos una de puntaje).
const MIN = 3, MAX = 5;

export default function EditorEncuesta({ titulo, aviso, textoConfirmar, procesando, onConfirmar, onCerrar }) {
  const [preguntas, setPreguntas] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    encuestasApi.porDefecto()
      .then(r => setPreguntas(r.data.map(p => ({ texto: p.texto, tipo: p.tipo }))))
      .catch(() => setError('No se pudieron cargar las preguntas por defecto'));
  }, []);

  const cambiar = (i, campo, valor) => setPreguntas(ps => ps.map((p, j) => (j === i ? { ...p, [campo]: valor } : p)));
  const quitar = (i) => setPreguntas(ps => ps.filter((_, j) => j !== i));
  const agregar = () => setPreguntas(ps => [...ps, { texto: '', tipo: 'ESCALA' }]);

  const confirmar = (e) => {
    e.preventDefault();
    const limpias = preguntas.map(p => ({ ...p, texto: p.texto.trim() }));
    if (limpias.some(p => !p.texto)) return setError('Ninguna pregunta puede quedar vacía');
    if (!limpias.some(p => p.tipo === 'ESCALA')) return setError('Incluye al menos una pregunta de puntaje (1 a 5)');
    setError(null);
    onConfirmar(limpias);
  };

  return (
    <div className="modal-fondo" onClick={procesando ? undefined : onCerrar}>
      <div className="modal" role="dialog" aria-modal="true" aria-label={titulo} style={{ maxWidth: 640 }}
           onClick={e => e.stopPropagation()}>
        <button className="modal-cerrar" onClick={onCerrar} aria-label="Cerrar" disabled={procesando}>×</button>
        <h2>{titulo}</h2>
        <p className="muted" style={{ margin: '8px 0 16px' }}>{aviso}</p>

        {error && <div className="error-msg">{error}</div>}
        {!preguntas ? (
          !error && <p>Cargando preguntas...</p>
        ) : (
          <form onSubmit={confirmar} className="form-stack">
            <label>Encuesta de satisfacción ({preguntas.length} de {MAX} preguntas)</label>
            {preguntas.map((p, i) => (
              <div key={i} style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                <span className="muted" style={{ width: 20 }}>{i + 1}.</span>
                <input type="text" value={p.texto} maxLength={200} required aria-label={`Pregunta ${i + 1}`}
                       onChange={e => cambiar(i, 'texto', e.target.value)} style={{ flex: '1 1 260px' }} />
                <select value={p.tipo} aria-label={`Tipo de la pregunta ${i + 1}`} style={{ width: 'auto' }}
                        onChange={e => cambiar(i, 'tipo', e.target.value)}>
                  <option value="ESCALA">Puntaje 1 a 5</option>
                  <option value="TEXTO">Comentario</option>
                </select>
                <button type="button" className="btn-secondary" disabled={preguntas.length <= MIN}
                        onClick={() => quitar(i)} aria-label={`Quitar pregunta ${i + 1}`}>Quitar</button>
              </div>
            ))}
            {preguntas.length < MAX && (
              <button type="button" className="btn-secondary" style={{ alignSelf: 'flex-start' }} onClick={agregar}>
                + Agregar pregunta
              </button>
            )}
            <p className="muted" style={{ fontSize: '0.85rem' }}>
              Las preguntas de puntaje son obligatorias y forman el promedio de satisfacción; los comentarios son opcionales.
            </p>
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', flexWrap: 'wrap' }}>
              <button type="button" className="btn-secondary" onClick={onCerrar} disabled={procesando}>Volver</button>
              <button type="submit" className="btn-primary" disabled={procesando}>
                {procesando ? 'Procesando...' : textoConfirmar}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
