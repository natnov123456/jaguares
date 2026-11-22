import { useEffect, useState } from 'react';
import { institucionalApi } from '../services/api';

// La Directora carga el QR que le dio el banco y los datos de la cuenta de la fundación.
// Se muestran en la página "Donar"; sin ellos, la donación en línea no está disponible.
const VACIO = { bancoNombre: '', bancoTipoCuenta: '', bancoCuenta: '', bancoTitular: '', bancoDocumento: '' };

export default function DatosParaDonar() {
  const [form, setForm] = useState(VACIO);
  const [tieneQr, setTieneQr] = useState(false);
  const [urlQr, setUrlQr] = useState('');
  const [errores, setErrores] = useState({});
  const [msg, setMsg] = useState(null);
  const [ocupado, setOcupado] = useState(false);

  const aplicar = (d) => {
    setForm(Object.fromEntries(Object.keys(VACIO).map(k => [k, d[k] ?? ''])));
    setTieneQr(d.tieneQr);
    setUrlQr(d.tieneQr ? institucionalApi.urlQr() : '');
  };
  useEffect(() => { institucionalApi.datosPago().then(r => aplicar(r.data)).catch(() => {}); }, []);

  const guardar = async () => {
    setOcupado(true); setMsg(null); setErrores({});
    try {
      aplicar((await institucionalApi.guardarDatosPago(form)).data);
      setMsg({ tipo: 'success', texto: 'Datos de la cuenta guardados.' });
    } catch (err) {
      setErrores(err.response?.data?.errores || {});
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo guardar' });
    } finally { setOcupado(false); }
  };

  const subirQr = async (archivo) => {
    setOcupado(true); setMsg(null);
    try {
      aplicar((await institucionalApi.subirQr(archivo)).data);
      setMsg({ tipo: 'success', texto: archivo ? 'QR actualizado.' : 'QR quitado.' });
    } catch (err) {
      setMsg({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo subir el QR' });
    } finally { setOcupado(false); }
  };

  const campo = (clave, etiqueta, max, ayuda) => (
    <div>
      <label htmlFor={`pago-${clave}`}>{etiqueta}</label>
      <input id={`pago-${clave}`} maxLength={max} value={form[clave]} onChange={e => setForm({ ...form, [clave]: e.target.value })} />
      {ayuda && <small className="muted">{ayuda}</small>}
      {errores[clave] && <div className="error-msg" style={{ marginTop: 4 }}>{errores[clave]}</div>}
    </div>
  );

  return (
    <section className="panel">
      <h2>Datos para donar</h2>
      <p className="muted" style={{ fontSize: '0.9rem' }}>
        Se muestran en la página "Donar". Cada donación en línea queda en verificación hasta que Contabilidad confirma el pago en el banco.
      </p>
      {msg && <div className={`${msg.tipo}-msg`}>{msg.texto}</div>}
      <div className="form-stack">
        <div>
          <label htmlFor="pago-qr">Código QR de la cuenta (imagen que da el banco)</label>
          {tieneQr && <img src={urlQr} alt="QR actual para donar" style={{ display: 'block', width: 160, maxWidth: '100%', margin: '6px 0', background: '#fff' }} />}
          <input id="pago-qr" type="file" accept="image/png,image/jpeg" disabled={ocupado}
                 onChange={e => { const f = e.target.files?.[0]; if (f) subirQr(f); e.target.value = ''; }} />
          {tieneQr && <button type="button" className="btn-secondary" style={{ marginTop: 6 }} disabled={ocupado} onClick={() => subirQr(null)}>Quitar QR</button>}
        </div>
        {campo('bancoNombre', 'Banco', 100)}
        {campo('bancoTipoCuenta', 'Tipo de cuenta', 50, 'Ej.: Caja de ahorro en bolivianos')}
        {campo('bancoCuenta', 'N° de cuenta', 50)}
        {campo('bancoTitular', 'Titular', 150)}
        {campo('bancoDocumento', 'NIT o CI del titular (opcional)', 30)}
        <button type="button" className="btn-primary" disabled={ocupado} onClick={guardar}>Guardar cuenta bancaria</button>
      </div>
    </section>
  );
}
