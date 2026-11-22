import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import CasillasLegales, { SIN_ACEPTAR } from '../components/CasillasLegales';
import { useAuth } from '../context/AuthContext';
import { campanasApi, donacionesApi, institucionalApi } from '../services/api';
import ProgresoCampana from '../components/ProgresoCampana';
import { numeroComprobante } from '../utils/formato';
import './Donaciones.css';

// Donación en línea por QR o transferencia (sin pasarela de pago):
// 1) el donante paga desde su banco con el QR o a la cuenta de la fundación,
// 2) envía el número de operación y la foto o PDF del comprobante,
// 3) Contabilidad o la Dirección confirma en el banco que el dinero llegó y recién ahí se emite el comprobante.
const MEDIOS = { QR: 'Código QR', TRANSFERENCIA: 'Transferencia bancaria' };
const MAX_MB = 5;

export default function Donaciones() {
  const { usuario } = useAuth();
  const [monto, setMonto] = useState('');
  const [medio, setMedio] = useState('QR');
  const [referencia, setReferencia] = useState('');
  const [archivo, setArchivo] = useState(null);
  const [nombre, setNombre] = useState('');
  const [email, setEmail] = useState('');
  const [campanas, setCampanas] = useState([]);
  const [campanaId, setCampanaId] = useState('');
  const [pago, setPago] = useState(null);
  const [urlQr, setUrlQr] = useState('');
  const [error, setError] = useState('');
  const [confirmacion, setConfirmacion] = useState(null);
  const [procesando, setProcesando] = useState(false);
  const [legal, setLegal] = useState(SIN_ACEPTAR);

  // Si la lista no carga se puede donar igual, sin elegir campaña
  useEffect(() => {
    campanasApi.vigentes().then(r => setCampanas(r.data)).catch(() => {});
    institucionalApi.datosPago()
      .then(r => { setPago(r.data); if (r.data.tieneQr) setUrlQr(institucionalApi.urlQr()); })
      .catch(() => setPago({ tieneQr: false }));
  }, []);

  const hayCuenta = Boolean(pago?.bancoCuenta);
  const medioDisponible = medio === 'QR' ? pago?.tieneQr : hayCuenta;

  const elegirArchivo = (e) => {
    const f = e.target.files?.[0] ?? null;
    setError('');
    if (f && f.size > MAX_MB * 1024 * 1024) { setError(`El comprobante no puede pesar más de ${MAX_MB} MB`); e.target.value = ''; return; }
    setArchivo(f);
  };

  const donar = async (e) => {
    e.preventDefault();
    setError('');
    if (!archivo) { setError('Adjunta la foto o el PDF del comprobante de tu banco'); return; }
    setProcesando(true);
    try {
      const { data } = await donacionesApi.enLinea({
        monto: String(monto).replace(',', '.'), medio, referencia: referencia.trim(),
        donanteNombre: usuario ? null : nombre, donanteEmail: usuario ? null : email,
        campanaId: campanaId || null, ...legal,
      }, archivo);
      setConfirmacion(data);
      setMonto(''); setReferencia(''); setArchivo(null); setCampanaId('');
    } catch (err) {
      setError(err.response?.status === 429
        ? 'Se enviaron varias donaciones seguidas desde esta conexión. Intenta de nuevo más tarde.'
        : err.response?.data?.error || 'No se pudo enviar la donación');
    } finally {
      setProcesando(false);
    }
  };

  if (confirmacion) {
    return (
      <div className="auth-container donar">
        <h1>¡Gracias por tu donación!</h1>
        <div className="info-msg">Tu donación está <strong>en verificación</strong>.</div>
        <p>La fundación revisará en su cuenta bancaria que el pago haya llegado. Cuando lo confirme, te enviaremos el
           <strong> comprobante de donación</strong> por correo (suele tardar de 1 a 2 días hábiles).</p>
        <dl className="donar-resumen">
          <dt>N° de registro</dt><dd>{numeroComprobante(confirmacion.numeroComprobante)}</dd>
          <dt>Monto</dt><dd>Bs. {Number(confirmacion.monto).toFixed(2)}</dd>
          <dt>Medio</dt><dd>{MEDIOS[confirmacion.medio] || confirmacion.medio}</dd>
          <dt>N° de operación</dt><dd>{confirmacion.referencia}</dd>
        </dl>
        {usuario && <p className="muted">Puedes ver el estado en <Link to="/mi-cuenta">Mi cuenta</Link>.</p>}
        <button className="btn-primary" onClick={() => setConfirmacion(null)}>Hacer otra donación</button>
      </div>
    );
  }

  return (
    <div className="auth-container donar">
      <h1>Hacer una donación</h1>
      {campanas.length > 0 && (
        <section className="donar-campanas">
          <h2>Campañas activas</h2>
          {campanas.map(c => (
            <article key={c.id}>
              <strong>{c.nombre}</strong>
              {c.descripcion && <p className="muted">{c.descripcion}</p>}
              <ProgresoCampana campana={c} />
              <button type="button" className={campanaId === c.id ? 'btn-primary' : 'btn-secondary'} onClick={() => setCampanaId(c.id)}>
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
        <label htmlFor="d-monto">Monto (Bs.)</label>
        <input id="d-monto" type="number" inputMode="decimal" min="1" step="0.01" value={monto} onChange={e => setMonto(e.target.value)} required />

        <span className="donar-etiqueta">1. ¿Cómo vas a pagar?</span>
        <div className="donar-medios" role="radiogroup" aria-label="Medio de pago">
          {Object.entries(MEDIOS).map(([valor, texto]) => (
            <button key={valor} type="button" role="radio" aria-checked={medio === valor}
                    className={medio === valor ? 'activo' : ''} onClick={() => setMedio(valor)}>
              {valor === 'QR' ? '📱' : '🏦'} {texto}
            </button>
          ))}
        </div>

        <div className="donar-instrucciones">
          {pago === null ? <p>Cargando datos de pago...</p> : !medioDisponible ? (
            <p className="error-msg" style={{ margin: 0 }}>
              La fundación todavía no publicó {medio === 'QR' ? 'su código QR' : 'su cuenta bancaria'}.
              {(medio === 'QR' ? hayCuenta : pago.tieneQr) ? ' Elige la otra opción.' : ' Escríbenos desde "Quiénes somos" y te ayudamos.'}
            </p>
          ) : medio === 'QR' ? (
            <>
              <p><strong>2.</strong> Escanea este QR desde la app de tu banco y paga el monto{monto ? ` de Bs. ${monto}` : ''}.</p>
              <img src={urlQr} alt="Código QR para donar a la fundación" className="donar-qr" />
              <a href={urlQr} download="qr-donacion-jaguares.png" className="btn-secondary donar-descargar">Descargar QR</a>
              <p className="muted">En el celular: descárgalo y ábrelo desde la opción "Pagar con QR" de tu banco.</p>
            </>
          ) : (
            <>
              <p><strong>2.</strong> Transfiere{monto ? ` Bs. ${monto}` : ' el monto'} a esta cuenta:</p>
              <dl className="donar-resumen">
                <dt>Banco</dt><dd>{pago.bancoNombre}</dd>
                {pago.bancoTipoCuenta && <><dt>Tipo de cuenta</dt><dd>{pago.bancoTipoCuenta}</dd></>}
                <dt>N° de cuenta</dt><dd><strong>{pago.bancoCuenta}</strong></dd>
                <dt>Titular</dt><dd>{pago.bancoTitular}</dd>
                {pago.bancoDocumento && <><dt>NIT / CI</dt><dd>{pago.bancoDocumento}</dd></>}
              </dl>
            </>
          )}
        </div>

        <span className="donar-etiqueta">3. Cuéntanos que ya pagaste</span>
        <label htmlFor="d-ref">N° de operación o transacción</label>
        <input id="d-ref" type="text" maxLength={60} value={referencia} onChange={e => setReferencia(e.target.value)}
               placeholder="Figura en el comprobante de tu banco" required />
        <label htmlFor="d-archivo">Foto o PDF del comprobante del banco</label>
        <input id="d-archivo" type="file" accept="image/jpeg,image/png,application/pdf" onChange={elegirArchivo} required />
        <p className="muted donar-nota">JPG, PNG o PDF de hasta {MAX_MB} MB. Puede ser una captura de pantalla de tu app del banco.</p>

        {usuario ? (
          <p className="muted">El comprobante de donación saldrá a nombre de {usuario.nombreCompleto} y se enviará al correo de tu cuenta.</p>
        ) : (
          <>
            <label htmlFor="d-nombre">Nombre para el comprobante</label>
            <input id="d-nombre" type="text" maxLength={150} value={nombre} onChange={e => setNombre(e.target.value)} required />
            <label htmlFor="d-email">Correo para recibir el comprobante</label>
            <input id="d-email" type="email" maxLength={150} value={email} onChange={e => setEmail(e.target.value)} required />
          </>
        )}
        <CasillasLegales valores={legal} cambiar={setLegal} prefijo="d" />
        <p className="muted donar-nota">
          La fundación verifica cada pago en su cuenta bancaria antes de emitir el comprobante. Si hubo un error en el monto,
          revisa la <Link to="/reembolsos" style={{ textDecoration: 'underline' }}>Política de reembolsos</Link>.
        </p>
        {error && <div className="error-msg">{error}</div>}
        <button type="submit" className="btn-primary" disabled={procesando || !medioDisponible}>
          {procesando ? 'Enviando...' : 'Enviar para verificación'}
        </button>
      </form>
    </div>
  );
}
