import { useCallback, useEffect, useRef, useState } from 'react';
import { eventosApi } from '../../services/api';
import LectorQr from '../../components/LectorQr';
import Estado from '../../components/Estado';
import { fechaHora } from '../../utils/formato';
import './Checkin.css';

// FUJ-113: check-in en la puerta del evento escaneando el QR del participante (pensado para celular)

const ESTILO_RESULTADO = {
  REGISTRADO: { clase: 'ok', icono: '✅' },
  YA_REGISTRADO: { clase: 'warn', icono: '⚠️' },
  OTRO_EVENTO: { clase: 'error', icono: '⛔' },
  FUERA_DE_FECHA: { clase: 'error', icono: '⛔' },
  INVALIDO: { clase: 'error', icono: '❌' },
};

// Vibración corta al aceptar y doble al rechazar, para no tener que mirar la pantalla
const vibrar = (patron) => { try { navigator.vibrate?.(patron); } catch { /* sin soporte */ } };

export default function Checkin() {
  const [eventos, setEventos] = useState([]);
  const [eventoId, setEventoId] = useState('');
  const [inscritos, setInscritos] = useState([]);
  const [resultado, setResultado] = useState(null);
  const [procesando, setProcesando] = useState(false);
  const [camara, setCamara] = useState(false);
  const [manual, setManual] = useState('');
  const [error, setError] = useState(null);
  const eventoRef = useRef('');

  useEffect(() => {
    eventosApi.listar()
      .then(r => {
        // Solo eventos que no terminaron hace más de un día, el más próximo primero
        const limite = Date.now() - 24 * 3600 * 1000;
        const lista = r.data
          .filter(e => new Date(e.fechaFin ?? e.fechaInicio).getTime() >= limite)
          .sort((a, b) => new Date(a.fechaInicio) - new Date(b.fechaInicio));
        setEventos(lista);
        if (lista.length > 0) setEventoId(lista[0].id);
      })
      .catch(() => setError('No se pudieron cargar los eventos'));
  }, []);

  const cargarInscritos = useCallback((id) => {
    if (!id) return;
    eventosApi.inscritos(id)
      .then(r => { if (eventoRef.current === id) setInscritos(r.data); })
      .catch(() => setError('No se pudo cargar la lista de inscritos'));
  }, []);

  useEffect(() => {
    eventoRef.current = eventoId;
    cargarInscritos(eventoId);
  }, [eventoId, cargarInscritos]);

  const cambiarEvento = (id) => {
    setResultado(null);
    setInscritos([]);
    setEventoId(id);
  };

  const registrar = useCallback(async (codigo) => {
    const id = eventoRef.current;
    if (!id || !codigo?.trim()) return;
    setProcesando(true);
    try {
      const { data } = await eventosApi.checkin(id, codigo.trim());
      setResultado(data);
      vibrar(120);
      cargarInscritos(id);
    } catch (err) {
      const data = err.response?.data;
      setResultado(data?.resultado ? data : { resultado: 'INVALIDO', mensaje: data?.error || 'No se pudo registrar' });
      vibrar([80, 60, 80]);
    } finally {
      setProcesando(false);
    }
  }, [cargarInscritos]);

  const enviarManual = (e) => {
    e.preventDefault();
    registrar(manual);
    setManual('');
  };

  const presentes = inscritos.filter(i => i.estado === 'ASISTIO').length;
  const estilo = resultado ? ESTILO_RESULTADO[resultado.resultado] ?? ESTILO_RESULTADO.INVALIDO : null;

  return (
    <div className="container page checkin">
      <div className="page-header">
        <div>
          <h1>Check-in con QR</h1>
          <p>Escanea el código QR del participante para registrar su asistencia.</p>
        </div>
        <div className="stat-box">
          <p>Presentes</p>
          <h2>{presentes}/{inscritos.length}</h2>
        </div>
      </div>

      {error && <div className="error-msg">{error}</div>}

      <section className="panel">
        <div className="form-stack">
          <div>
            <label htmlFor="evento">Evento</label>
            <select id="evento" value={eventoId} onChange={e => cambiarEvento(e.target.value)}>
              {eventos.length === 0 && <option value="">No hay eventos próximos</option>}
              {eventos.map(ev => (
                <option key={ev.id} value={ev.id}>{ev.titulo} — {fechaHora(ev.fechaInicio)}</option>
              ))}
            </select>
          </div>
        </div>

        {eventoId && (
          <>
            <div className="checkin-camara">
              {camara ? (
                <>
                  <LectorQr onDetectar={registrar} pausado={procesando} />
                  <button className="btn-secondary" onClick={() => setCamara(false)}>Detener cámara</button>
                </>
              ) : (
                <button className="btn-primary checkin-btn" onClick={() => setCamara(true)}>📷 Escanear QR</button>
              )}
            </div>

            {resultado && (
              <div className={`checkin-resultado ${estilo.clase}`} role="status" aria-live="polite">
                <span className="checkin-icono">{estilo.icono}</span>
                <div>
                  <strong>{resultado.mensaje}</strong>
                  {resultado.participante && <div>{resultado.participante}</div>}
                  {resultado.checkinEn && <small>{fechaHora(resultado.checkinEn)}</small>}
                </div>
              </div>
            )}

            <form onSubmit={enviarManual} className="checkin-manual">
              <input value={manual} onChange={e => setManual(e.target.value)} placeholder="O escribe el código"
                     aria-label="Código del participante" autoComplete="off" />
              <button type="submit" className="btn-primary" disabled={procesando || !manual.trim()}>Registrar</button>
            </form>
          </>
        )}
      </section>

      {eventoId && (
        <section className="panel">
          <h2>Inscritos</h2>
          {inscritos.length === 0 ? (
            <p className="muted">Todavía no hay inscritos en este evento.</p>
          ) : (
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>Participante</th><th>Estado</th><th>Ingreso</th></tr></thead>
                <tbody>
                  {inscritos.map(i => (
                    <tr key={i.inscripcionId}>
                      <td><strong>{i.nombre}</strong><div className="muted" style={{ fontSize: '0.85rem' }}>{i.email}</div></td>
                      <td><Estado valor={i.estado} /></td>
                      <td>{i.checkinEn ? fechaHora(i.checkinEn) : '-'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}
    </div>
  );
}
