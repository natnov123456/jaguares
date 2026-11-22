import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import { puede } from '../../config/permisos';
import { campanasApi, donacionesApi } from '../../services/api';
import { bs, fechaHora, numeroComprobante } from '../../utils/formato';
import BotonComprobante from '../../components/BotonComprobante';
import BarraFiltros from '../../components/filtros/BarraFiltros';
import BotonExportar from '../../components/BotonExportar';
import useFiltros from '../../hooks/useFiltros';
import { aplicarFiltros } from '../../utils/filtros';

const ESTADOS = [
  { valor: 'CONFIRMADA', etiqueta: 'Confirmadas' }, { valor: 'PENDIENTE', etiqueta: 'En verificación' },
  { valor: 'RECHAZADA', etiqueta: 'Rechazadas' }, { valor: 'ANULADA', etiqueta: 'Anuladas' },
];
const ETIQUETA_ESTADO = { CONFIRMADA: 'Confirmada', PENDIENTE: 'En verificación', RECHAZADA: 'Rechazada', ANULADA: 'Anulada' };
const BADGE_ESTADO = { CONFIRMADA: 'badge-ok', PENDIENTE: 'badge-warn', RECHAZADA: 'badge-off', ANULADA: 'badge-off' };
// Ya no hay pasarela de pago (las donaciones antiguas con PASARELA se siguen mostrando)
const MEDIOS = ['EFECTIVO', 'TRANSFERENCIA', 'QR'];
const MEDIO_TEXTO = { EFECTIVO: 'Efectivo', TRANSFERENCIA: 'Transferencia', QR: 'QR', PASARELA: 'Pasarela (antigua)' };

// Donaciones en línea que esperan que se confirme el pago en el banco (Contador y Directora).
// Se abre el comprobante que subió el donante, se compara con el extracto del banco y se confirma o rechaza.
function PorVerificar({ onCambio }) {
  const [pendientes, setPendientes] = useState(null);
  const [viendo, setViendo] = useState(null); // { donacion, url, tipo }
  const [rechazando, setRechazando] = useState(null);
  const [motivo, setMotivo] = useState('');
  const [mensaje, setMensaje] = useState(null);
  const [ocupado, setOcupado] = useState(false);

  const cargar = () => donacionesApi.porVerificar().then(r => setPendientes(r.data)).catch(() => setPendientes([]));
  useEffect(() => { cargar(); }, []);
  useEffect(() => () => { if (viendo?.url) URL.revokeObjectURL(viendo.url); }, [viendo]);

  const ver = async (d) => {
    try {
      const r = await donacionesApi.comprobantePago(d.id);
      setViendo({ donacion: d, url: URL.createObjectURL(r.data), tipo: r.data.type });
    } catch { setMensaje({ tipo: 'error', texto: 'No se pudo abrir el comprobante' }); }
  };

  const decidir = async (d, confirmar) => {
    if (confirmar && !window.confirm(`¿Confirmas que ${bs(d.monto)} (operación ${d.referencia}) llegó a la cuenta de la fundación?`)) return;
    setOcupado(true);
    setMensaje(null);
    try {
      await donacionesApi.verificar(d.id, confirmar, confirmar ? null : motivo.trim());
      setMensaje({ tipo: 'success', texto: confirmar
        ? `Pago confirmado. Se envió el comprobante N° ${numeroComprobante(d.numeroComprobante)} al donante.`
        : 'Donación rechazada. Se avisó al donante con el motivo.' });
      setRechazando(null); setMotivo(''); setViendo(null);
      cargar();
      onCambio();
    } catch (err) {
      setMensaje({ tipo: 'error', texto: err.response?.data?.error || 'No se pudo guardar' });
    } finally { setOcupado(false); }
  };

  if (!pendientes) return null;
  return (
    <section className="panel" style={{ marginBottom: 16, borderLeft: `4px solid ${pendientes.length ? '#d97706' : '#16a34a'}` }}>
      <h2 style={{ marginTop: 0 }}>Por verificar en el banco ({pendientes.length})</h2>
      {mensaje && <div className={`${mensaje.tipo}-msg`}>{mensaje.texto}</div>}
      {pendientes.length === 0 ? <p className="muted">No hay donaciones en línea esperando verificación.</p> : (
        <>
          <p className="muted" style={{ fontSize: '0.9rem' }}>
            Abre el comprobante, búscalo en el extracto de la cuenta (monto y N° de operación) y confirma solo si el dinero llegó.
            Mientras tanto no suman en reportes ni campañas.
          </p>
          <div className="tarjetas-verificar">
            {pendientes.map(d => (
              <article key={d.id} className="tarjeta-verificar">
                <div className="tv-monto">{bs(d.monto)}</div>
                <div><strong>{d.donanteNombre || 'Sin nombre'}</strong>{d.donanteEmail && <span className="muted"> · {d.donanteEmail}</span>}</div>
                <div className="muted" style={{ fontSize: '0.85rem' }}>
                  {MEDIO_TEXTO[d.medio] || d.medio} · Op. <code>{d.referencia}</code> · {fechaHora(d.fecha)}
                  {d.campana && <> · {d.campana}</>}
                </div>
                <div className="tv-acciones">
                  <button type="button" className="btn-secondary" onClick={() => ver(d)} disabled={!d.tipoComprobante}>Ver comprobante</button>
                  <button type="button" className="btn-primary" onClick={() => decidir(d, true)} disabled={ocupado}>Confirmar pago</button>
                  <button type="button" className="btn-secondary" style={{ color: '#991b1b' }} onClick={() => { setRechazando(d); setMotivo(''); }} disabled={ocupado}>Rechazar</button>
                </div>
                {rechazando?.id === d.id && (
                  <div className="form-stack" style={{ marginTop: 8 }}>
                    <label htmlFor={`mot-${d.id}`}>Motivo (se le envía al donante)</label>
                    <textarea id={`mot-${d.id}`} rows="2" maxLength={300} value={motivo} onChange={e => setMotivo(e.target.value)}
                              placeholder="Ej.: No encontramos el depósito con ese número de operación" />
                    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                      <button type="button" className="btn-danger" disabled={ocupado || motivo.trim().length < 5} onClick={() => decidir(d, false)}>Rechazar donación</button>
                      <button type="button" className="btn-secondary" onClick={() => setRechazando(null)}>Cancelar</button>
                    </div>
                  </div>
                )}
              </article>
            ))}
          </div>
        </>
      )}
      {viendo && (
        <div className="modal-fondo" onClick={() => setViendo(null)}>
          <div className="modal modal-comprobante" role="dialog" aria-modal="true" aria-label="Comprobante de pago" onClick={e => e.stopPropagation()}>
            <button className="modal-cerrar" onClick={() => setViendo(null)} aria-label="Cerrar">×</button>
            <h2>Comprobante de {viendo.donacion.donanteNombre || 'donante'} · {bs(viendo.donacion.monto)}</h2>
            <p className="muted">Operación <code>{viendo.donacion.referencia}</code></p>
            {viendo.tipo === 'application/pdf'
              ? <iframe src={viendo.url} title="Comprobante en PDF" className="comprobante-vista" />
              : <img src={viendo.url} alt="Comprobante de pago" className="comprobante-vista" />}
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 10 }}>
              <a href={viendo.url} download={`comprobante-pago-${numeroComprobante(viendo.donacion.numeroComprobante)}`} className="btn-secondary">Descargar</a>
              <button type="button" className="btn-primary" onClick={() => decidir(viendo.donacion, true)} disabled={ocupado}>Confirmar pago</button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
const ultimoDia = (anio, mes) => new Date(anio, mes, 0).getDate();
const dosDigitos = (n) => String(n).padStart(2, '0');

// FUJ-125: corregir o anular una donación (Contador y Directora). Queda en la bitácora de auditoría.
function EditorDonacion({ donacion, modo, onGuardado, onCerrar }) {
  const anular = modo === 'anular';
  const [form, setForm] = useState({ monto: donacion.monto, medio: donacion.medio, referencia: donacion.referencia ?? '', motivo: '' });
  const [error, setError] = useState(null);
  const [enviando, setEnviando] = useState(false);

  const guardar = async (e) => {
    e.preventDefault();
    setError(null);
    setEnviando(true);
    try {
      const { data } = anular
        ? await donacionesApi.anular(donacion.id, form.motivo)
        : await donacionesApi.editar(donacion.id, {
            monto: parseFloat(form.monto), medio: form.medio, referencia: form.referencia.trim() || null,
            campanaId: donacion.campanaId ?? null, motivo: form.motivo.trim() || null,
          });
      onGuardado(data, anular);
    } catch (err) {
      setError(err.response?.data?.error || 'No se pudo guardar el cambio');
      setEnviando(false);
    }
  };

  return (
    <div className="modal-fondo" onClick={onCerrar}>
      <div className="modal" role="dialog" aria-modal="true" aria-label={anular ? 'Anular donación' : 'Corregir donación'}
           onClick={e => e.stopPropagation()}>
        <button className="modal-cerrar" onClick={onCerrar} aria-label="Cerrar">×</button>
        <h2>{anular ? 'Anular' : 'Corregir'} donación N° {numeroComprobante(donacion.numeroComprobante)}</h2>
        <p className="muted" style={{ marginBottom: 12 }}>{bs(donacion.monto)} · {fechaHora(donacion.fecha)}</p>
        <form onSubmit={guardar} className="form-stack">
          {!anular && (
            <>
              <div><label htmlFor="ed-monto">Monto (Bs.)</label>
                <input id="ed-monto" type="number" min="0.01" step="0.01" required value={form.monto}
                       onChange={e => setForm({ ...form, monto: e.target.value })} /></div>
              <div><label htmlFor="ed-medio">Medio</label>
                <select id="ed-medio" value={form.medio} onChange={e => setForm({ ...form, medio: e.target.value })}>
                  {[...new Set([...MEDIOS, donacion.medio])].map(m => <option key={m} value={m}>{MEDIO_TEXTO[m] || m}</option>)}
                </select></div>
              <div><label htmlFor="ed-ref">Referencia / recibo</label>
                <input id="ed-ref" maxLength={100} value={form.referencia} onChange={e => setForm({ ...form, referencia: e.target.value })} /></div>
            </>
          )}
          <div><label htmlFor="ed-motivo">{anular ? 'Motivo de la anulación' : 'Motivo de la corrección (opcional)'}</label>
            <textarea id="ed-motivo" rows="2" maxLength={300} required={anular} minLength={anular ? 5 : undefined}
                      value={form.motivo} onChange={e => setForm({ ...form, motivo: e.target.value })} /></div>
          <div className={anular ? 'error-msg' : 'info-msg'} style={{ marginBottom: 0 }}>
            {anular
              ? 'La donación dejará de sumar en el reporte, las campañas y la transparencia. El cambio queda en la bitácora de auditoría.'
              : 'El valor anterior y el nuevo quedan guardados en la bitácora de auditoría con tu nombre y la hora.'}
          </div>
          {error && <div className="error-msg" role="alert">{error}</div>}
          <div style={{ display: 'flex', gap: 8 }}>
            <button type="submit" className={anular ? 'btn-danger' : 'btn-primary'} disabled={enviando}>
              {enviando ? 'Guardando...' : anular ? 'Anular donación' : 'Guardar corrección'}
            </button>
            <button type="button" className="btn-secondary" onClick={onCerrar} disabled={enviando}>Volver</button>
          </div>
        </form>
      </div>
    </div>
  );
}

const MESES = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto',
               'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

function TablaDonaciones({ donaciones, vacio, onEditar }) {
  if (donaciones.length === 0) return <p className="muted" style={{ textAlign: 'center', padding: 24 }}>{vacio}</p>;
  return (
    <div className="table-wrap">
      <table className="table">
        <thead><tr><th>N°</th><th>Fecha</th><th>Monto</th><th>Medio</th><th>Ref.</th><th>Estado</th><th /></tr></thead>
        <tbody>
          {donaciones.map(d => (
            <tr key={d.id}>
              <td><code>{numeroComprobante(d.numeroComprobante)}</code></td>
              <td>{fechaHora(d.fecha)}</td>
              <td style={{ fontWeight: 'bold' }}>{bs(d.monto)}</td>
              <td>{MEDIO_TEXTO[d.medio] || d.medio}</td>
              <td className="muted">{d.referencia || '-'}</td>
              <td><span className={`badge ${BADGE_ESTADO[d.estado] || 'badge-ok'}`}>{ETIQUETA_ESTADO[d.estado] || d.estado}</span></td>
              <td>
                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                  {d.estado === 'CONFIRMADA' && <BotonComprobante donacion={d} />}
                  {onEditar && d.estado === 'CONFIRMADA' && (
                    <>
                      <button type="button" className="btn-secondary" style={{ padding: '4px 10px' }} onClick={() => onEditar(d, 'editar')}>Corregir</button>
                      <button type="button" className="btn-secondary" style={{ padding: '4px 10px', color: '#991b1b' }} onClick={() => onEditar(d, 'anular')}>Anular</button>
                    </>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default function AdminDonaciones() {
  const { usuario } = useAuth();
  // La directora ve el reporte financiero completo; el encargado de eventos solo lo que él registró
  const verReporte = puede(usuario, 'verReporteFinanciero');
  const puedeCorregir = puede(usuario, 'corregirDonaciones');
  const { valores, cambiar, limpiar } = useFiltros(['q', 'estado', 'accion']);
  const [editando, setEditando] = useState(null); // { donacion, modo }

  const hoy = new Date();
  const [periodo, setPeriodo] = useState({ anio: hoy.getFullYear(), mes: hoy.getMonth() + 1 });
  const [reporte, setReporte] = useState({ total: 0, cantidad: 0, detalle: [] });
  const [misRegistros, setMisRegistros] = useState([]);
  const [loading, setLoading] = useState(true);

  const [monto, setMonto] = useState('');
  const [referencia, setReferencia] = useState('');
  const [donanteNombre, setDonanteNombre] = useState('');
  const [donanteEmail, setDonanteEmail] = useState('');
  const [campanas, setCampanas] = useState([]);
  const [campanaId, setCampanaId] = useState('');
  const [mensaje, setMensaje] = useState(null);

  const cargar = async () => {
    try {
      if (verReporte) {
        const { data } = await donacionesApi.reporte(periodo.anio, periodo.mes);
        setReporte(data);
      } else {
        const { data } = await donacionesApi.misRegistros();
        setMisRegistros(data);
      }
    } catch (error) {
      console.error('Error al cargar donaciones:', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    setLoading(true);
    cargar();
  }, [periodo.anio, periodo.mes]);

  useEffect(() => { campanasApi.vigentes().then(r => setCampanas(r.data)).catch(() => {}); }, []);

  const registrarDonacionManual = async (e) => {
    e.preventDefault();
    setMensaje(null);
    try {
      const { data } = await donacionesApi.registrar({
        monto: parseFloat(monto),
        medio: 'EFECTIVO',
        referencia: referencia || 'RECIBO-OFICINA',
        donanteId: null,
        donanteNombre: donanteNombre.trim() || null,
        donanteEmail: donanteEmail.trim() || null,
        campanaId: campanaId || null,
      });
      const aviso = !donanteEmail.trim() ? '' : data.comprobanteEnviadoEn
        ? ' y comprobante enviado por correo'
        : ', pero no se pudo enviar el comprobante por correo';
      setMensaje({ tipo: 'success', texto: `✅ Donación registrada${aviso}` });
      setMonto('');
      setReferencia('');
      setDonanteNombre('');
      setDonanteEmail('');
      setCampanaId('');
      cargar();
    } catch (err) {
      setMensaje({ tipo: 'error', texto: `❌ ${err.response?.data?.error || 'Error al registrar la donación'}` });
    }
  };

  const anios = Array.from({ length: 4 }, (_, i) => hoy.getFullYear() - i);

  // Mismos filtros en pantalla y en el archivo exportado (el mes elegido es el rango de fechas)
  const lista = verReporte ? reporte.detalle : misRegistros;
  const visibles = aplicarFiltros(lista, valores,
    { texto: d => [numeroComprobante(d.numeroComprobante), d.referencia], estado: d => d.estado })
    .filter(d => !valores.accion || d.medio === valores.accion);
  const filtrosExportar = verReporte
    ? { ...valores, desde: `${periodo.anio}-${dosDigitos(periodo.mes)}-01`, hasta: `${periodo.anio}-${dosDigitos(periodo.mes)}-${ultimoDia(periodo.anio, periodo.mes)}` }
    : valores;

  const alGuardar = (data, anulada) => {
    setEditando(null);
    setMensaje({ tipo: 'success', texto: anulada
      ? `Donación N° ${numeroComprobante(data.numeroComprobante)} anulada. Quedó registrada en la bitácora.`
      : `Donación N° ${numeroComprobante(data.numeroComprobante)} corregida. Quedó registrada en la bitácora.` });
    cargar();
  };

  return (
    <div className="container page">
      <div className="page-header">
        <div>
          <h1>{verReporte ? 'Reporte de Donaciones' : 'Registro de Donaciones'}</h1>
          <p>
            {verReporte
              ? `Reporte financiero de ${MESES[periodo.mes - 1]} ${periodo.anio}`
              : 'Registra el efectivo que recibes en oficina o en eventos.'}
          </p>
        </div>
        {verReporte && (
          <div style={{ display: 'flex', gap: 16, alignItems: 'center', flexWrap: 'wrap' }}>
            <div className="form-stack" style={{ flexDirection: 'row' }}>
              <select value={periodo.mes} onChange={e => setPeriodo({ ...periodo, mes: +e.target.value })} aria-label="Mes">
                {MESES.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}
              </select>
              <select value={periodo.anio} onChange={e => setPeriodo({ ...periodo, anio: +e.target.value })} aria-label="Año">
                {anios.map(a => <option key={a} value={a}>{a}</option>)}
              </select>
            </div>
            <div className="stat-box">
              <p>Total recaudado ({reporte.cantidad} aportes)</p>
              <h2>{bs(reporte.total)}</h2>
            </div>
          </div>
        )}
      </div>

      {puedeCorregir && <PorVerificar onCambio={cargar} />}

      <div className="split">
        <section className="panel">
          <h2>{verReporte ? 'Detalle del mes' : 'Mis últimos registros'}</h2>
          <BarraFiltros valores={valores} cambiar={cambiar} limpiar={limpiar} texto="N° de comprobante o referencia"
                        estados={ESTADOS} total={lista.length} visibles={visibles.length}>
            <label className="filtro">
              <span>Medio</span>
              <select value={valores.accion} onChange={e => cambiar('accion', e.target.value)}>
                <option value="">Todos</option>
                {[...MEDIOS, 'PASARELA'].map(m => <option key={m} value={m}>{MEDIO_TEXTO[m]}</option>)}
              </select>
            </label>
            <BotonExportar reporte="donaciones" filtros={filtrosExportar} />
          </BarraFiltros>
          {loading ? (
            <p>Cargando...</p>
          ) : (
            <TablaDonaciones donaciones={visibles} onEditar={puedeCorregir ? (donacion, modo) => setEditando({ donacion, modo }) : null}
                             vacio={lista.length === 0
                               ? (verReporte ? 'No hay donaciones registradas en este mes.' : 'Aún no registraste donaciones.')
                               : 'Ninguna donación coincide con los filtros.'} />
          )}
        </section>

        <section className="panel">
          <h2>Registrar efectivo</h2>
          <p className="muted" style={{ fontSize: '0.9rem', marginBottom: 16 }}>
            Usa este formulario cuando alguien entregue dinero en mano. Las donaciones en línea (QR y transferencia) llegan solas a "Por verificar".
          </p>
          {mensaje && <div className={`${mensaje.tipo}-msg`}>{mensaje.texto}</div>}
          <form onSubmit={registrarDonacionManual} className="form-stack">
            <div>
              <label>Monto (Bs.)</label>
              <input type="number" min="1" step="0.01" value={monto} onChange={e => setMonto(e.target.value)} required />
            </div>
            <div>
              <label>Referencia / Recibo</label>
              <input type="text" value={referencia} onChange={e => setReferencia(e.target.value)} placeholder="Ej: RECIBO-001" />
            </div>
            <div>
              <label htmlFor="d-nombre">Nombre del donante (opcional)</label>
              <input id="d-nombre" type="text" maxLength={150} value={donanteNombre} onChange={e => setDonanteNombre(e.target.value)}
                     required={!!donanteEmail.trim()} />
            </div>
            <div>
              <label htmlFor="d-email">Correo para enviarle el comprobante (opcional)</label>
              <input id="d-email" type="email" maxLength={150} value={donanteEmail} onChange={e => setDonanteEmail(e.target.value)} />
            </div>
            {campanas.length > 0 && (
              <div>
                <label htmlFor="d-campana">Campaña (opcional)</label>
                <select id="d-campana" value={campanaId} onChange={e => setCampanaId(e.target.value)}>
                  <option value="">Donación general</option>
                  {campanas.map(c => <option key={c.id} value={c.id}>{c.nombre}</option>)}
                </select>
              </div>
            )}
            <button type="submit" className="btn-primary">Guardar donación</button>
          </form>
        </section>
      </div>
      {editando && (
        <EditorDonacion donacion={editando.donacion} modo={editando.modo} onGuardado={alGuardar} onCerrar={() => setEditando(null)} />
      )}
    </div>
  );
}
