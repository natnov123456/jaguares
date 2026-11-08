import { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import { puede } from '../../config/permisos';
import { campanasApi, donacionesApi } from '../../services/api';
import { bs, fechaHora, numeroComprobante } from '../../utils/formato';
import BotonComprobante from '../../components/BotonComprobante';

const MESES = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto',
               'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

function TablaDonaciones({ donaciones, vacio }) {
  if (donaciones.length === 0) return <p className="muted" style={{ textAlign: 'center', padding: 24 }}>{vacio}</p>;
  return (
    <div className="table-wrap">
      <table className="table">
        <thead><tr><th>N°</th><th>Fecha</th><th>Monto</th><th>Medio</th><th>Ref.</th><th /></tr></thead>
        <tbody>
          {donaciones.map(d => (
            <tr key={d.id}>
              <td><code>{numeroComprobante(d.numeroComprobante)}</code></td>
              <td>{fechaHora(d.fecha)}</td>
              <td style={{ fontWeight: 'bold' }}>{bs(d.monto)}</td>
              <td>{d.medio}</td>
              <td className="muted">{d.referencia || '-'}</td>
              <td>{d.estado === 'CONFIRMADA' && <BotonComprobante donacion={d} />}</td>
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

      <div className="split">
        <section className="panel">
          <h2>{verReporte ? 'Detalle del mes' : 'Mis últimos registros'}</h2>
          {loading ? (
            <p>Cargando...</p>
          ) : verReporte ? (
            <TablaDonaciones donaciones={reporte.detalle} vacio="No hay donaciones registradas en este mes." />
          ) : (
            <TablaDonaciones donaciones={misRegistros} vacio="Aún no registraste donaciones." />
          )}
        </section>

        <section className="panel">
          <h2>Registrar efectivo</h2>
          <p className="muted" style={{ fontSize: '0.9rem', marginBottom: 16 }}>
            Usa este formulario cuando alguien entregue dinero en mano. Las donaciones en línea se registran solas.
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
    </div>
  );
}
