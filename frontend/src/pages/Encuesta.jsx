import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { encuestasApi } from '../services/api';
import { fecha, fechaHora } from '../utils/formato';
import './Encuesta.css';

// FUJ-120: formulario público de la encuesta. El token del enlace identifica a la persona,
// así que no hace falta iniciar sesión.
const ETIQUETAS = ['Muy mal', 'Mal', 'Regular', 'Bien', 'Excelente'];

export default function Encuesta() {
  const { token } = useParams();
  const [encuesta, setEncuesta] = useState(null);
  const [estado, setEstado] = useState('cargando'); // cargando | invalida | lista | enviando | gracias
  const [valores, setValores] = useState({});
  const [textos, setTextos] = useState({});
  const [error, setError] = useState(null);

  useEffect(() => {
    encuestasApi.obtener(token)
      .then(r => { setEncuesta(r.data); setEstado(r.data.yaRespondida ? 'gracias' : 'lista'); })
      .catch(() => setEstado('invalida'));
  }, [token]);

  const enviar = async (e) => {
    e.preventDefault();
    const faltante = encuesta.preguntas.find(p => p.tipo === 'ESCALA' && !valores[p.id]);
    if (faltante) {
      setError(`Elige un puntaje en: ${faltante.texto}`);
      document.getElementById(`pregunta-${faltante.id}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      return;
    }
    setError(null);
    setEstado('enviando');
    const respuestas = encuesta.preguntas
      .map(p => (p.tipo === 'ESCALA' ? { preguntaId: p.id, valor: valores[p.id] } : { preguntaId: p.id, texto: textos[p.id]?.trim() || null }))
      .filter(r => r.valor || r.texto);
    try {
      await encuestasApi.responder(token, respuestas);
      setEstado('gracias');
    } catch (err) {
      if (err.response?.status === 409) { setEstado('gracias'); return; }
      setError(err.response?.data?.error || 'No se pudo enviar tu respuesta. Intenta de nuevo.');
      setEstado('lista');
    }
  };

  if (estado === 'cargando') return <div className="container page"><p>Cargando encuesta...</p></div>;

  if (estado === 'invalida') {
    return (
      <div className="container page encuesta">
        <section className="panel encuesta-tarjeta">
          <h1>Enlace no válido</h1>
          <p className="muted">Este enlace de encuesta no existe o está incompleto. Revisa que lo hayas copiado completo desde el correo.</p>
          <Link to="/" className="btn-primary" style={{ display: 'inline-block', marginTop: 16 }}>Ir al inicio</Link>
        </section>
      </div>
    );
  }

  const cuando = encuesta.tipoActividad === 'CURSO' ? fecha(encuesta.fecha.slice(0, 10)) : fechaHora(encuesta.fecha);

  if (estado === 'gracias') {
    return (
      <div className="container page encuesta">
        <section className="panel encuesta-tarjeta" style={{ textAlign: 'center' }}>
          <div style={{ fontSize: '3rem' }}>🐯</div>
          <h1>¡Gracias por tu opinión!</h1>
          <p className="muted">Tu respuesta sobre "{encuesta.actividad}" ya quedó registrada. Nos ayuda a mejorar las próximas actividades.</p>
          <Link to="/calendario" className="btn-primary" style={{ display: 'inline-block', marginTop: 16 }}>Ver próximas actividades</Link>
        </section>
      </div>
    );
  }

  return (
    <div className="container page encuesta">
      <section className="panel encuesta-tarjeta">
        <p className="muted" style={{ marginBottom: 4 }}>Encuesta de satisfacción · {encuesta.tipoActividad === 'CURSO' ? 'Curso' : 'Evento'} del {cuando}</p>
        <h1>{encuesta.actividad}</h1>
        <p className="muted">Hola {encuesta.participante}, ¿cómo te fue? Son {encuesta.preguntas.length} preguntas y toma menos de un minuto.</p>

        <form onSubmit={enviar} style={{ marginTop: 24 }}>
          {encuesta.preguntas.map(p => (
            <fieldset key={p.id} id={`pregunta-${p.id}`} className="encuesta-pregunta">
              <legend>{p.orden}. {p.texto}{p.tipo === 'TEXTO' && <span className="muted"> (opcional)</span>}</legend>
              {p.tipo === 'ESCALA' ? (
                <div className="encuesta-escala" role="radiogroup" aria-label={p.texto}>
                  {[1, 2, 3, 4, 5].map(n => (
                    <label key={n} className={`encuesta-opcion ${valores[p.id] === n ? 'elegida' : ''}`}>
                      <input type="radio" name={p.id} value={n} checked={valores[p.id] === n}
                             onChange={() => setValores(v => ({ ...v, [p.id]: n }))} />
                      <span className="encuesta-numero">{n}</span>
                      <span className="encuesta-etiqueta">{ETIQUETAS[n - 1]}</span>
                    </label>
                  ))}
                </div>
              ) : (
                <textarea rows="4" maxLength={1000} value={textos[p.id] ?? ''} placeholder="Escribe aquí tu comentario"
                          onChange={e => setTextos(t => ({ ...t, [p.id]: e.target.value }))} aria-label={p.texto} />
              )}
            </fieldset>
          ))}

          {error && <div className="error-msg" role="alert">{error}</div>}
          <button type="submit" className="btn-primary" disabled={estado === 'enviando'} style={{ width: '100%', padding: 12 }}>
            {estado === 'enviando' ? 'Enviando...' : 'Enviar respuesta'}
          </button>
          <p className="muted" style={{ fontSize: '0.85rem', marginTop: 8, textAlign: 'center' }}>
            Tus comentarios se muestran a la fundación sin tu nombre. Solo puedes responder una vez.
          </p>
        </form>
      </section>
    </div>
  );
}
